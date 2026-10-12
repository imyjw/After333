#nullable enable
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    internal static class AiMovementPolicy
    {
        internal const int WoundedHpPercent = 30;
        private const double MoveCost = 4;
        private const double MovedCoverPenalty = 25;

        internal static bool IsWounded(OccupantState unit) =>
            unit.Kind != OccupantKind.Building && unit.IsAlive && !unit.IsSealbound &&
            unit.CurrentHp < unit.MaxHp &&
            (long)unit.CurrentHp * 100 <= (long)unit.MaxHp * WoundedHpPercent;

        internal static double ProtectionValue(OccupantState? unit)
        {
            if (unit == null || !unit.IsAlive || unit.IsSealbound) return 0;
            if (AiPositionEvaluator.IsResourceProducer(unit))
                return unit.Kind == OccupantKind.Building ? 60 : 50;
            if (AiPositionEvaluator.IsRangedThreat(unit)) return 30;
            return IsWounded(unit) ? 20 : 0;
        }

        // Geometry only admits a candidate. Shared turn resolution must confirm a real survival gain.
        internal static bool IsHazardEscapeCandidate(BattleState state, MoveOccupantCommand move)
        {
            var unit = state.AIBoard.GetOccupant(move.From);
            if (unit == null || !unit.IsAlive || !unit.CanMove || unit.CannotMoveDueToState ||
                move.From == move.To || state.AIBoard.GetOccupant(move.To) != null) return false;
            foreach (var effect in state.PersistentEffects)
            {
                if (effect.IsExpired) continue;
                if (effect.EffectId == BiochemicalBombRules.EffectId && effect.OwnerId == PlayerId.Player &&
                    (effect.TargetStartColumn == BiochemicalBombRules.LeftAreaStartColumn ||
                     effect.TargetStartColumn == BiochemicalBombRules.RightAreaStartColumn) &&
                    BiochemicalBombRules.ContainsColumn(effect.TargetStartColumn, move.From.Column) &&
                    !BiochemicalBombRules.ContainsColumn(effect.TargetStartColumn, move.To.Column)) return true;
                if (effect.EffectId == "firewall" &&
                    (effect.TargetsOwnerBoard ? effect.OwnerId == PlayerId.AI : effect.OwnerId == PlayerId.Player) &&
                    move.From.Row == effect.TargetRow && move.To.Row != effect.TargetRow) return true;
            }
            return false;
        }

        internal static bool ImprovesEscapeSurvival(BattleState before, MoveOccupantCommand move,
            BattleState staying, BattleState escaping)
        {
            var unit = before.AIBoard.GetOccupant(move.From);
            var survivor = escaping.AIBoard.GetOccupant(move.To);
            if (unit == null || survivor == null || survivor.RuntimeId != unit.RuntimeId || !survivor.IsAlive)
                return false;
            var original = staying.AIBoard.GetOccupant(move.From);
            return original == null || original.RuntimeId != unit.RuntimeId ||
                !original.IsAlive || survivor.CurrentHp > original.CurrentHp;
        }

        internal static double Score(BattleState state, MoveOccupantCommand move) =>
            ScoreCore(state, move, null);

        // Only reward cover that survives shared turn resolution.
        internal static double ScoreBeforeTurnStart(BattleState state, MoveOccupantCommand move, BattleState projected) =>
            ScoreCore(state, move, projected.AIBoard);

        // The guard still protects during the opponent's main phase, even if its end-turn aura
        // later removes that guard. Only the protected non-Master's survival limits this bonus.
        internal static double ScoreWithSurvivalForecast(BattleState before, MoveOccupantCommand move,
            BattleState near, BoardState futureAiBoard) =>
            ScoreCore(before, move, near.AIBoard, futureAiBoard);

        // Distinct names preserve legacy reflection callers and unknown-card exact scores.
        internal static double ScoreUsingCardCosts(BattleState state, MoveOccupantCommand move,
            AiProducerProtectionPolicy policy) => ScoreCore(state, move, null, null, policy);

        internal static double ScoreForPlanningBeforeTurnStart(BattleState state, MoveOccupantCommand move,
            BattleState projected, AiProducerProtectionPolicy policy) =>
            ScoreCore(state, move, projected.AIBoard, null, policy, true);

        internal static double ScoreForPlanningWithSurvivalForecast(BattleState state, MoveOccupantCommand move,
            BattleState near, BoardState futureAiBoard, AiProducerProtectionPolicy policy) =>
            ScoreCore(state, move, near.AIBoard, futureAiBoard, policy, true);

        private static double ScoreCore(BattleState state, MoveOccupantCommand move,
            BoardState? projectedBoard, BoardState? futureTargetBoard = null,
            AiProducerProtectionPolicy? policy = null, bool knownProducerCreditInState = false)
        {
            var board = state.AIBoard;
            var moving = board.GetOccupant(move.From);
            var swapped = board.GetOccupant(move.To);
            if (moving == null || move.From == move.To) return -MoveCost;
            double score = -MoveCost;
            var movedExistingCover = false;
            foreach (var target in board.EnumerateOccupants())
            {
                var value = policy == null ? ProtectionValue(target) : policy.ProtectionValue(target);
                if (value <= 0) continue;
                var beforeCover = Cover(board, target.Position);
                var afterPosition = ReferenceEquals(target, moving) ? move.To :
                    ReferenceEquals(target, swapped) ? move.From : target.Position;
                var afterFront = afterPosition.Row == 1
                    ? At(new TileCoord(afterPosition.Column, 0)) : null;
                var afterCover = Blocks(afterFront) ? afterFront : null;
                // Reward new cover only, never exchanging one safe slot for another.
                if (beforeCover == null && afterCover != null && ProtectionValue(afterCover) == 0)
                {
                    var coverValue = policy == null ? value : policy.CoverValue(target, afterCover);
                    var gain = projectedBoard == null ? coverValue :
                        SurvivingProtectionValue(target, afterPosition, afterCover, projectedBoard, coverValue,
                            futureTargetBoard, policy);
                    // The evaluator carries known-producer cover as a state value exactly once.
                    // Keep legacy bonuses for all other roles and missing metadata.
                    if (!knownProducerCreditInState || policy == null || !policy.TryGetProducerCost(target, out _))
                        score += gain;
                }
                if (beforeCover != null && afterCover == null)
                {
                    score -= value * 2;
                    movedExistingCover = true;
                }
                // Count both occupants of a swap: moving the rear card can pull its guard out too.
                if (beforeCover != null &&
                    (ReferenceEquals(beforeCover, moving) || ReferenceEquals(beforeCover, swapped)))
                {
                    score -= MovedCoverPenalty;
                    movedExistingCover = true;
                }
            }
            // Expensive new production cannot justify stealing an established protector.
            // A genuine hazard escape can still be simulated with these retained penalties.
            return policy != null && movedExistingCover ? System.Math.Min(0, score) : score;

            OccupantState? At(TileCoord coord) => coord == move.From ? swapped :
                coord == move.To ? moving : board.GetOccupant(coord);
        }

        private static double SurvivingProtectionValue(OccupantState target, TileCoord afterPosition,
            OccupantState guard, BoardState projectedBoard, double originalValue, BoardState? futureTargetBoard,
            AiProducerProtectionPolicy? policy = null)
        {
            var projectedTarget = projectedBoard.GetOccupant(afterPosition);
            var projectedGuard = projectedBoard.GetOccupant(new TileCoord(afterPosition.Column, 0));
            if (projectedTarget == null || projectedTarget.RuntimeId != target.RuntimeId ||
                projectedGuard == null || projectedGuard.RuntimeId != guard.RuntimeId || !Blocks(projectedGuard))
                return 0;
            if (futureTargetBoard != null && target.Kind != OccupantKind.Master)
            {
                var futureTarget = futureTargetBoard.GetOccupant(afterPosition);
                if (futureTarget == null || futureTarget.RuntimeId != target.RuntimeId || !futureTarget.IsAlive)
                    return 0;
            }
            // Recheck the protected role too: a dead, sealed, or no-longer-wounded target earns no bonus.
            return System.Math.Min(originalValue, policy == null ? ProtectionValue(projectedTarget) :
                policy.CoverValue(projectedTarget, projectedGuard));
        }

        private static OccupantState? Cover(BoardState board, TileCoord rear)
        {
            if (rear.Row != 1) return null;
            var front = board.GetOccupant(new TileCoord(rear.Column, 0));
            return Blocks(front) ? front : null;
        }

        private static bool Blocks(OccupantState? unit) =>
            unit != null && unit.IsAlive && !unit.DoesNotBlockFrontRow;
    }
}
