#nullable enable
using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Application.Services
{
    internal sealed class AiPositionEvaluator
    {
        private const double ProducerSurvivalValue = 50;
        private const double ProducerHpValue = 0.45;
        private const double RangedSurvivalValue = 18;
        private const double RangedHpValue = 0.30;
        private readonly TargetingService _targeting = new TargetingService();
        public double Evaluate(BattleState state) => EvaluateCore(state, null, null);

        // The immediate score must not reward occupants that the real turn transition removes.
        // Presence preserves Demon King revival even when its projected HP is zero.
        public double EvaluateBeforeTurnStart(BattleState state, BattleState projected) =>
            EvaluateCore(state, projected, null);

        // Only forecast continued AI board value. The opponent's unplayed turn is not a known outcome.
        public double EvaluateWithSurvivalForecast(BattleState state, BattleState projected, BoardState futureAiBoard) =>
            EvaluateCore(state, projected, futureAiBoard);

        private double EvaluateCore(BattleState state, BattleState? projected, BoardState? futureAiBoard)
        {
            if (state.IsEnded) return state.Result.IsDraw ? 0 : state.Result.Winner == PlayerId.AI ? 1000000 : -1000000;
            var value = Side(state, PlayerId.AI, projected, futureAiBoard) - Side(state, PlayerId.Player, projected, futureAiBoard);
            // Finishing the enemy master matters, but not at the cost of a losing counterattack.
            value += (333 - state.Player.Master.CurrentHp) * 0.65;
            return value;
        }

        private double Side(BattleState state, PlayerId owner, BattleState? projected, BoardState? futureAiBoard)
        {
            var projectedAiBoard = projected?.AIBoard;
            var player = state.GetPlayer(owner);
            var board = state.GetBoard(owner);
            double value = player.Master.CurrentHp * 1.3 - 2000.0 / (Math.Max(0, player.Master.CurrentHp) + 10);
            value += ResourceValue(player.Resources);
            foreach (var card in player.Hand.Cards) value += card.IsTemporaryReplicate ? 3 : 7;
            foreach (var unit in board.EnumerateOccupants())
            {
                if (!unit.IsAlive && !unit.IsDemonKingRevivalPending) continue;
                if (!RemainsOnBoard(unit, projectedAiBoard) || !RemainsOnBoard(unit, futureAiBoard)) continue;
                var material = 12 + Math.Max(0, unit.Attack) * 1.35 * unit.EffectiveHitsPerAttack * Math.Min(2, unit.EffectiveMaxAttacksPerTurn)
                    + Math.Max(0, unit.CurrentHp) * 0.55;
                if (unit is MasterState) material = Math.Max(0, unit.Attack - 3) * 1.35;
                // State value, not a per-click reward: only real HP loss/removal earns target priority.
                // A ranged producer uses the higher producer role rather than stacking both bonuses.
                if (IsResourceProducer(unit)) material += ProducerSurvivalValue + Math.Max(0, unit.CurrentHp) * ProducerHpValue;
                else if (IsRangedThreat(unit)) material += RangedSurvivalValue + Math.Max(0, unit.CurrentHp) * RangedHpValue;
                if (unit.IsSealbound) material *= 0.45;
                else if (unit.IsDrained) material *= 0.3;
                if (!unit.EffectsSuppressed)
                {
                    material += GainValue(unit.TurnStartResourceGain) * 3;
                    material += unit.CanTriggerEndure ? 7 : 0;
                    material += unit.HasLifeSteal ? Math.Min(15, unit.Attack * 0.5) : 0;
                    material += unit.HasActiveShielder ? 7 : 0;
                    material += unit.HasActiveFlying ? 6 : 0;
                    material += unit.IsHiding ? 4 : 0;
                    material += unit.HasActivePiercing ? 6 : 0;
                    material += unit.IsInvincible ? 12 : 0;
                    material += unit.EffectiveSpellPower * 4;
                    if (unit.CardId == "RobotFactory" || unit.CardId == "GaebangBranch") material += 14;
                    if (unit.CardId == "PowerPlant") material += player.Resources.Gold > 0 ? 7 : 1;
                    if (unit.CardId == "Hero" || unit.CardId == "DemonKing") material += 18;
                }
                if (unit.IsUnderHuanShu) material *= 0.6;
                value += material;
                value += owner == PlayerId.AI && projectedAiBoard != null
                    ? BackRowProtectionValue(projectedAiBoard.GetOccupant(unit.Position), projectedAiBoard)
                    : BackRowProtectionValue(unit, board);
            }
            foreach (var effect in state.PersistentEffects)
            {
                if (effect.IsExpired || effect.OwnerId != owner) continue;
                value += GainValue(effect.TurnStartResourceGain) * Math.Min(3, Math.Max(1, effect.OwnerTurnStartsRemaining));
                if (effect.EffectDamage <= 0) continue;
                var targetBoard = effect.TargetsOwnerBoard ? board : state.GetOpponentBoard(owner);
                double pressure = 0;
                foreach (var target in targetBoard.EnumerateOccupants())
                {
                    if (!target.CanBeAffected || !target.IsAlive || !RemainsOnBoard(target, projectedAiBoard)) continue;
                    if (effect.TargetRow >= 0 && target.Position.Row != effect.TargetRow) continue;
                    if (effect.TargetStartColumn >= 0 && !BiochemicalBombRules.ContainsColumn(effect.TargetStartColumn, target.Position.Column)) continue;
                    pressure += Math.Min(target.CurrentHp, effect.EffectDamage) * 0.16;
                }
                // Future occupancy is uncertain. Immediate triggers are resolved by the simulator, not estimated here.
                value += (effect.TargetsOwnerBoard ? -1 : 1) * pressure * Math.Min(2, Math.Max(1, effect.RemainingTriggers));
            }
            // Use the resolved board for threat geometry: a doomed guard cannot protect the master.
            var threatState = projected ?? state;
            var enemyId = threatState.GetOpponent(owner).Id;
            double exposure = 0;
            foreach (var enemy in threatState.GetOpponentBoard(owner).EnumerateOccupants())
            {
                if (!enemy.IsAlive || enemy.CannotAttack || enemy.Attack <= 0) continue;
                // Enemy attacks and our guards matter during the imminent enemy main phase.
                // Our future attacks must survive its end effects; keep master HP at the near horizon.
                if (!CanAttackAfterForecast(enemy, futureAiBoard)) continue;
                if (_targeting.CanTarget(threatState, enemyId, enemy.Position, threatState.GetPlayer(owner).Master.Position))
                    exposure += enemy.Attack * enemy.EffectiveHitsPerAttack * Math.Min(2, enemy.EffectiveMaxAttacksPerTurn);
            }
            // A positioning risk heuristic, not a second implementation of damage/armor rules.
            value -= exposure * (0.08 + 8.0 / (Math.Max(1, player.Master.CurrentHp) + 5));
            return value;
        }

        private static bool CanAttackAfterForecast(OccupantState unit, BoardState? futureAiBoard)
        {
            if (futureAiBoard == null || unit.OwnerId != PlayerId.AI || unit is MasterState) return true;
            var futureUnit = futureAiBoard.GetOccupant(unit.Position);
            return futureUnit != null && futureUnit.RuntimeId == unit.RuntimeId &&
                futureUnit.IsAlive && !futureUnit.CannotAttack;
        }

        private static bool RemainsOnBoard(OccupantState unit, BoardState? projectedAiBoard)
        {
            if (projectedAiBoard == null || unit.OwnerId != PlayerId.AI || unit is MasterState) return true;
            var projectedUnit = projectedAiBoard.GetOccupant(unit.Position);
            return projectedUnit != null && projectedUnit.RuntimeId == unit.RuntimeId;
        }

        internal static bool IsResourceProducer(OccupantState unit)
        {
            if (unit.Kind == OccupantKind.Master || unit.IsErasure) return false;
            var gain = unit.TurnStartResourceGain;
            return gain.Mana > 0 || gain.Qi > 0 || gain.Power > 0 || gain.Gold > 0 ||
                unit.CardId == PowerPlantRules.CardId;
        }

        internal static bool IsRangedThreat(OccupantState unit)
        {
            return unit.Kind != OccupantKind.Master && unit.AttackType == AttackType.Ranged &&
                unit.Attack > 0 && unit.EffectiveMaxAttacksPerTurn > 0;
        }

        private static double BackRowProtectionValue(OccupantState? unit, BoardState board)
        {
            if (unit == null || !unit.IsAlive || unit.IsSealbound || unit.Position.Row != 1) return 0;
            var producer = IsResourceProducer(unit);
            var ranged = IsRangedThreat(unit);
            var wounded = AiMovementPolicy.IsWounded(unit);
            var rearValue = producer ? 6 : ranged ? 5 : wounded ? 3 : 0;
            var front = board.GetOccupant(new TileCoord(unit.Position.Column, 0));
            if (front == null || !front.IsAlive || front.DoesNotBlockFrontRow) return rearValue;
            // This is ground-melee cover, not immunity to ranged/Flying/Piercing attacks.
            var coverValue = producer ? (unit.Kind == OccupantKind.Building ? 24 : 20) :
                ranged ? 12 : wounded ? 8 : unit.Kind == OccupantKind.Master ? 3 : 1;
            return rearValue + coverValue;
        }

        private static double ResourceValue(ResourceSet r)
        {
            return Currency(r.Gold, 4.0) + Currency(r.Mana, 3.0) + Currency(r.Qi, 3.0) + Currency(r.Power, 2.0);
        }
        private static double Currency(int amount, double weight) => weight * (Math.Min(12, amount) + Math.Max(0, amount - 12) * 0.2);
        private static double GainValue(ResourceSet r) => r.Mana * 3 + r.Qi * 3 + r.Power * 3 + r.Gold * 3.5;
    }
}
