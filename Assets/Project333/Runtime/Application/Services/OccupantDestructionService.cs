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

        public static void DestroyUnsealedSummons(BattleState battleState)
        {
            if (battleState == null) throw new ArgumentNullException(nameof(battleState));
            var removed = new List<OccupantState>();
            foreach (var owner in BoardOrder)
            {
                var board = battleState.GetBoard(owner);
                foreach (var occupant in new List<OccupantState>(board.EnumerateOccupants()))
                {
                    if (occupant.Kind == OccupantKind.Master || occupant.IsSealbound) continue;
                    if (TryEnterDemonKingRevivalSeal(battleState, occupant)) continue;
                    board.Remove(occupant.Position);
                    removed.Add(occupant);
                }
            }
            ResolveDestructionQueue(battleState, null, removed);
        }

        private static void ResolveDestructionQueue(
            BattleState battleState,
            OccupantState explicitlyDestroyed,
            IEnumerable<OccupantState> explicitlyDestroyedGroup = null)
        {
            var pendingEffects = new Queue<LegacySource>();
            var queuedSources = new HashSet<OccupantState>();
            QueueLegacyEffect(explicitlyDestroyed, pendingEffects, queuedSources);
            if (explicitlyDestroyedGroup != null)
                foreach (var occupant in explicitlyDestroyedGroup) QueueLegacyEffect(occupant, pendingEffects, queuedSources);
            CollectAndRemoveDefeatedOccupants(battleState, pendingEffects, queuedSources);

            while (pendingEffects.Count > 0)
            {
                var source = pendingEffects.Dequeue();
                if (source.Kind == LegacyEffectKind.DrawCards)
                {
                    // Resolve the whole death-effect batch before deciding simultaneous
                    // master defeat, including fatigue caused by multiple legacies.
                    BattleDrawService.DrawCards(battleState.GetPlayer(source.OwnerId), battleState,
                        source.Amount, source.CardId, resolveMasterDefeat: false);
                }
                else if (source.Kind == LegacyEffectKind.GlobalPhysicalDamage)
                {
                    ApplyNuclearExplosion(battleState, source);
                }
                CollectAndRemoveDefeatedOccupants(battleState, pendingEffects, queuedSources);
            }

            BattleOutcomeResolver.ResolveMasterDefeat(battleState);
        }

        private static void CollectAndRemoveDefeatedOccupants(
            BattleState battleState,
            Queue<LegacySource> pendingEffects,
            ISet<OccupantState> queuedSources)
        {
            // Losing a chieftain's HP aura can defeat a previously scanned unit, including
            // another chieftain. Settle the whole removal group before queued Legacy effects.
            var removedAny = true;
            while (removedAny)
            {
                removedAny = false;
                foreach (var ownerId in BoardOrder)
                {
                    var board = battleState.GetBoard(ownerId);
                    board.RefreshOrcChieftainAuras();
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
                            removedAny = true;
                            QueueLegacyEffect(occupant, pendingEffects, queuedSources);
                        }
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
            return HasActiveLegacyEffect(occupant) &&
                LegacyRules.GetEffectKind(occupant.CardId) == LegacyEffectKind.GlobalPhysicalDamage;
        }

        public static bool HasActiveLegacyEffect(OccupantState occupant)
        {
            return occupant != null && occupant.Kind != OccupantKind.Master &&
                occupant.HasLegacy && !occupant.EffectsSuppressed;
        }

        private static void QueueLegacyEffect(
            OccupantState occupant,
            Queue<LegacySource> pendingEffects,
            ISet<OccupantState> queuedSources)
        {
            if (!HasActiveLegacyEffect(occupant) ||
                !queuedSources.Add(occupant))
            {
                return;
            }

            pendingEffects.Enqueue(new LegacySource(
                occupant.OwnerId,
                occupant.RuntimeId,
                occupant.CardId));
        }

        private static void ApplyNuclearExplosion(
            BattleState battleState,
            LegacySource source)
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
                        source.Amount,
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

        private readonly struct LegacySource
        {
            public LegacySource(
                PlayerId ownerId,
                string runtimeId,
                string cardId)
            {
                OwnerId = ownerId;
                RuntimeId = runtimeId ?? string.Empty;
                CardId = cardId ?? string.Empty;
                Kind = LegacyRules.GetEffectKind(CardId);
                Amount = LegacyRules.GetAmount(CardId);
            }

            public PlayerId OwnerId { get; }

            public string RuntimeId { get; }

            public string CardId { get; }
            public LegacyEffectKind Kind { get; }
            public int Amount { get; }
        }
    }
}
