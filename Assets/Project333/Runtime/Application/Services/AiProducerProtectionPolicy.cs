#nullable enable
using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    // Immutable card metadata for this planner, never battle truth or a global lookup.
    internal sealed class AiProducerProtectionPolicy
    {
        private readonly Dictionary<string, long> _costs = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<long, int> _ranks = new Dictionary<long, int>();

        internal AiProducerProtectionPolicy(ICardDefinitionProvider provider)
        {
            var costs = new SortedSet<long>();
            foreach (var card in provider.GetAll())
            {
                if (card == null || (card.CardType != CardType.Unit && card.CardType != CardType.Building)) continue;
                var cost = card.Cost;
                if (cost == null || cost.Mana < 0 || cost.Qi < 0 || cost.Power < 0 || cost.Gold < 0) continue;
                var total = (long)cost.Mana + cost.Qi + cost.Power + cost.Gold;
                _costs[card.CardId] = total;
                costs.Add(total);
            }
            var rank = 0;
            foreach (var cost in costs) _ranks[cost] = rank++;
        }

        internal bool TryGetProducerCost(OccupantState? target, out long cost)
        {
            cost = 0;
            return target != null && target.IsAlive && !target.IsSealbound &&
                AiPositionEvaluator.IsResourceProducer(target) && _costs.TryGetValue(target.CardId, out cost);
        }

        internal double ProtectionValue(OccupantState? target)
        {
            return TryGetProducerCost(target, out var cost) ? 60 + 10 * _ranks[cost] :
                AiMovementPolicy.ProtectionValue(target);
        }

        internal double CoverValue(OccupantState target, OccupantState guard)
        {
            var value = ProtectionValue(target);
            if (!TryGetProducerCost(target, out var cost)) return value;
            var rank = _ranks[cost];
            // Less than one cost step: HP quality cannot reverse printed-cost priority.
            // Multiplying increasing cost and current-HP factors pairs stronger tanks with dearer producers.
            return value + 8.0 * (rank + 1) / (rank + 2) * TankHp(guard) / (TankHp(guard) + 30.0);
        }

        internal static int TankHp(OccupantState guard) =>
            guard.Kind == OccupantKind.Unit && guard.AttackType == AttackType.Melee && guard.CanMove &&
            !guard.CannotMoveDueToState && !guard.DoesNotBlockFrontRow && guard.IsAlive &&
            AiMovementPolicy.ProtectionValue(guard) == 0 ? Math.Max(0, guard.CurrentHp) : 0;

        internal AiProducerProtectionFocus? CreateFocus(BattleState state, MoveOccupantCommand move)
        {
            var board = state.AIBoard;
            var moving = board.GetOccupant(move.From);
            var swapped = board.GetOccupant(move.To);
            if (moving == null) return null;
            AiProducerProtectionFocus? best = null;
            foreach (var target in board.EnumerateOccupants())
            {
                if (!TryGetProducerCost(target, out var cost) || Cover(board, target.Position) != null) continue;
                var position = ReferenceEquals(target, moving) ? move.To :
                    ReferenceEquals(target, swapped) ? move.From : target.Position;
                if (position.Row != 1) continue;
                var coord = new TileCoord(position.Column, 0);
                var guard = coord == move.From ? swapped : coord == move.To ? moving : board.GetOccupant(coord);
                if (guard == null || !guard.IsAlive || guard.DoesNotBlockFrontRow ||
                    AiMovementPolicy.ProtectionValue(guard) > 0) continue;
                var hp = TankHp(guard);
                if (best == null || cost > best.Cost || (cost == best.Cost && hp > best.GuardHp))
                    best = new AiProducerProtectionFocus(cost, hp, target.RuntimeId, guard.RuntimeId, position);
            }
            return best;
        }

        // Keep all non-protection slots and priority groups fixed. Equal pairs keep coordinate insertion order.
        internal void OrderProtectionMoves(BattleState state, List<AiAction> actions)
        {
            for (var start = 0; start < actions.Count;)
            {
                var end = start + 1;
                while (end < actions.Count && actions[end].Priority == actions[start].Priority) end++;
                var slots = new List<int>();
                var moves = new List<ProtectionMoveOrder>();
                for (var index = start; index < end; index++)
                {
                    if (actions[index].Commands[0] is not MoveOccupantCommand move) continue;
                    var focus = CreateFocus(state, move);
                    if (focus == null) continue;
                    slots.Add(index);
                    moves.Add(new ProtectionMoveOrder(index, actions[index], focus));
                }
                moves.Sort((a, b) => a.Focus.Cost != b.Focus.Cost ? b.Focus.Cost.CompareTo(a.Focus.Cost) :
                    a.Focus.GuardHp != b.Focus.GuardHp ? b.Focus.GuardHp.CompareTo(a.Focus.GuardHp) :
                    a.Index.CompareTo(b.Index));
                for (var index = 0; index < slots.Count; index++) actions[slots[index]] = moves[index].Action;
                start = end;
            }
        }

        private sealed class ProtectionMoveOrder
        {
            internal readonly int Index;
            internal readonly AiAction Action;
            internal readonly AiProducerProtectionFocus Focus;
            internal ProtectionMoveOrder(int index, AiAction action, AiProducerProtectionFocus focus)
            { Index = index; Action = action; Focus = focus; }
        }

        internal static bool IsValidFocus(AiProducerProtectionFocus focus, BoardState near, BoardState? future)
        {
            var target = near.GetOccupant(focus.Position);
            var guard = Cover(near, focus.Position);
            if (target == null || !target.IsAlive || target.IsSealbound || !AiPositionEvaluator.IsResourceProducer(target) ||
                target.RuntimeId != focus.TargetId || guard == null || guard.RuntimeId != focus.GuardId) return false;
            if (future == null) return true;
            var futureTarget = future.GetOccupant(focus.Position);
            // Existing policy credits a guard during enemy main even if a later end effect destroys it.
            return futureTarget != null && futureTarget.IsAlive && futureTarget.RuntimeId == focus.TargetId;
        }

        private static OccupantState? Cover(BoardState board, TileCoord position)
        {
            if (position.Row != 1) return null;
            var guard = board.GetOccupant(new TileCoord(position.Column, 0));
            return guard != null && guard.IsAlive && !guard.DoesNotBlockFrontRow ? guard : null;
        }
    }

    internal sealed class AiProducerProtectionFocus
    {
        internal readonly long Cost;
        internal readonly int GuardHp;
        internal readonly string TargetId, GuardId;
        internal readonly TileCoord Position;
        internal AiProducerProtectionFocus(long cost, int guardHp, string targetId, string guardId, TileCoord position)
        { Cost = cost; GuardHp = guardHp; TargetId = targetId; GuardId = guardId; Position = position; }
    }
}
