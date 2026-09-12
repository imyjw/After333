using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class OccupantDestructionService
    {
        private static readonly PlayerId[] BoardOrder =
        {
            PlayerId.Player,
            PlayerId.AI,
        };

        public static void ResolveDefeatedOccupants(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            ResolveDestructionQueue(battleState, null);
        }

        public static void DestroyOccupant(
            BattleState battleState,
            PlayerId boardOwnerId,
            OccupantState occupant)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (occupant == null)
            {
                throw new ArgumentNullException(nameof(occupant));
            }

            if (occupant.Kind == OccupantKind.Master)
            {
                throw new InvalidOperationException("Master Units must be defeated through HP damage.");
            }

            var board = battleState.GetBoard(boardOwnerId);
            if (board.GetOccupant(occupant.Position) != occupant)
            {
                return;
            }

            if (TryEnterDemonKingRevivalSeal(battleState, occupant))
            {
                ResolveDestructionQueue(battleState, null);
                return;
            }

            board.Remove(occupant.Position);
            ResolveDestructionQueue(battleState, occupant);
        }

        private static void ResolveDestructionQueue(
            BattleState battleState,
            OccupantState explicitlyDestroyed)
        {
            var pendingExplosions = new Queue<NuclearExplosionSource>();
            var queuedSources = new HashSet<OccupantState>();
            QueueNuclearExplosion(explicitlyDestroyed, pendingExplosions, queuedSources);
            CollectAndRemoveDefeatedOccupants(battleState, pendingExplosions, queuedSources);

            while (pendingExplosions.Count > 0)
            {
                ApplyNuclearExplosion(battleState, pendingExplosions.Dequeue());
                CollectAndRemoveDefeatedOccupants(battleState, pendingExplosions, queuedSources);
            }

            BattleOutcomeResolver.ResolveMasterDefeat(battleState);
        }

        private static void CollectAndRemoveDefeatedOccupants(
            BattleState battleState,
            Queue<NuclearExplosionSource> pendingExplosions,
            ISet<OccupantState> queuedSources)
        {
            foreach (var ownerId in BoardOrder)
            {
                var board = battleState.GetBoard(ownerId);
                for (var column = 0; column < BoardState.ColumnCount; column++)
                {
                    for (var row = 0; row < BoardState.RowCount; row++)
                    {
                        var coord = new TileCoord(column, row);
                        var occupant = board.GetOccupant(coord);
                        if (occupant == null ||
                            occupant.Kind == OccupantKind.Master ||
                            occupant.CurrentHp > 0 ||
                            occupant.IsDemonKingRevivalPending)
                        {
                            continue;
                        }

                        if (TryEnterDemonKingRevivalSeal(battleState, occupant))
                        {
                            continue;
                        }

                        board.Remove(coord);
                        QueueNuclearExplosion(occupant, pendingExplosions, queuedSources);
                    }
                }
            }
        }

        private static bool TryEnterDemonKingRevivalSeal(
            BattleState battleState,
            OccupantState occupant)
        {
            return occupant != null &&
                   occupant.TryEnterDemonKingRevivalSeal(
                       battleState.ActivePlayerId,
                       battleState.TurnNumber);
        }

        // Shared with AI candidate ordering. Destruction resolution calls this after removal,
        // so current HP and board membership must not be part of this predicate.
        public static bool HasActiveDestructionEffect(OccupantState occupant)
        {
            return occupant != null && !occupant.EffectsSuppressed &&
                string.Equals(occupant.CardId, NuclearPowerPlantRules.CardId, StringComparison.Ordinal);
        }

        private static void QueueNuclearExplosion(
            OccupantState occupant,
            Queue<NuclearExplosionSource> pendingExplosions,
            ISet<OccupantState> queuedSources)
        {
            if (!HasActiveDestructionEffect(occupant) ||
                !queuedSources.Add(occupant))
            {
                return;
            }

            pendingExplosions.Enqueue(new NuclearExplosionSource(
                occupant.OwnerId,
                occupant.RuntimeId,
                occupant.CardId));
        }

        private static void ApplyNuclearExplosion(
            BattleState battleState,
            NuclearExplosionSource source)
        {
            foreach (var ownerId in BoardOrder)
            {
                var board = battleState.GetBoard(ownerId);
                var targets = new List<OccupantState>(board.EnumerateOccupants());
                foreach (var target in targets)
                {
                    if (board.GetOccupant(target.Position) != target)
                    {
                        continue;
                    }

                    var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                        target,
                        NuclearPowerPlantRules.DestructionDamage,
                        DamageType.Physical);
                    BattleValuePopupRecorder.RecordDamage(
                        battleState,
                        target,
                        actualDamage,
                        BattleValueChangeCause.NuclearPowerPlant,
                        DamageType.Physical,
                        source.OwnerId,
                        source.RuntimeId,
                        source.CardId);
                }
            }
        }

        private readonly struct NuclearExplosionSource
        {
            public NuclearExplosionSource(
                PlayerId ownerId,
                string runtimeId,
                string cardId)
            {
                OwnerId = ownerId;
                RuntimeId = runtimeId ?? string.Empty;
                CardId = cardId ?? string.Empty;
            }

            public PlayerId OwnerId { get; }

            public string RuntimeId { get; }

            public string CardId { get; }
        }
    }
}
