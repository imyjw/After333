using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class SpellService
    {
        private const string DaehwandanEffectId = "daehwandan";
        private const string CheonraJimangEffectId = "cheonra_jimang";
        private const int DaehwandanMasterAttackBonus = 30;
        private const int DaehwandanQiGain = 3;
        private const int DaehwandanOwnerTurnStarts = 3;

        private readonly ICardDefinitionProvider _cardDefinitionProvider;

        public SpellService()
        {
        }

        public SpellService(ICardDefinitionProvider cardDefinitionProvider)
        {
            _cardDefinitionProvider = cardDefinitionProvider ?? throw new ArgumentNullException(nameof(cardDefinitionProvider));
        }

        public void CastDamageSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord)
        {
            var definition = GetRequiredDefinition<DamageSpellCardDefinition>(cardId);
            CastResolvedDamageSpell(
                battleState,
                casterId,
                cardId,
                targetOwnerId,
                targetCoord,
                definition.Damage,
                definition.Cost);
        }

        public void CastDamageSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            int damage)
        {
            CastResolvedDamageSpell(
                battleState,
                casterId,
                cardId,
                targetOwnerId,
                targetCoord,
                damage,
                new ResourceSet());
        }

        public void CastPersistentResourceSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId)
        {
            var definition = GetRequiredDefinition<PersistentResourceSpellCardDefinition>(cardId);
            CastResolvedPersistentResourceSpell(
                battleState,
                casterId,
                cardId,
                definition.EffectId,
                definition.TurnStartResourceGain,
                definition.EndConditionText,
                definition.Cost,
                definition.OwnerTurnStartsRemaining);
        }

        public void CastPersistentResourceSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            string effectId,
            ResourceSet turnStartResourceGain,
            string endConditionText,
            int ownerTurnStartsRemaining = 0)
        {
            CastResolvedPersistentResourceSpell(
                battleState,
                casterId,
                cardId,
                effectId,
                turnStartResourceGain,
                endConditionText,
                new ResourceSet(),
                ownerTurnStartsRemaining);
        }

        public void CastScriptedSpell(BattleState battleState, PlayerId casterId, string cardId)
        {
            CastResolvedScriptedSpell(battleState, casterId, cardId, hasTarget: false, PlayerId.Player, default(TileCoord));
        }

        public void CastScriptedSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord)
        {
            CastResolvedScriptedSpell(battleState, casterId, cardId, hasTarget: true, targetOwnerId, targetCoord);
        }

        private static void ValidateSpellCastPhase(BattleState battleState, PlayerId casterId)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (battleState.Phase != PhaseType.Main)
            {
                throw new InvalidOperationException("Spells can only be cast during the main phase.");
            }

            if (battleState.ActivePlayerId != casterId)
            {
                throw new InvalidOperationException("Only the active player can cast spells.");
            }
        }

        private void CastResolvedDamageSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            int damage,
            ResourceSet cost)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Spell card id is required.", nameof(cardId));
            }

            ValidateSpellCastPhase(battleState, casterId);

            var caster = battleState.GetPlayer(casterId);
            var targetBoard = battleState.GetBoard(targetOwnerId);
            if (targetBoard.GetOccupant(targetCoord) == null)
            {
                throw new InvalidOperationException("Damage spell target tile is empty.");
            }

            ConsumeSpellCard(caster, cardId, cost);

            var affectedTargets = new HashSet<OccupantState>();
            ApplyEffectDamageToDeclaredTarget(battleState, targetBoard, targetCoord, damage, affectedTargets);

            ResolveSpellDefeat(battleState, affectedTargets);
            RemoveDefeatedOccupants(targetBoard, affectedTargets);
        }

        private void CastResolvedPersistentResourceSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            string effectId,
            ResourceSet turnStartResourceGain,
            string endConditionText,
            ResourceSet cost,
            int ownerTurnStartsRemaining)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Spell card id is required.", nameof(cardId));
            }

            if (string.IsNullOrWhiteSpace(effectId))
            {
                throw new ArgumentException("Persistent effect id is required.", nameof(effectId));
            }

            if (turnStartResourceGain == null)
            {
                throw new ArgumentNullException(nameof(turnStartResourceGain));
            }

            if (string.IsNullOrWhiteSpace(endConditionText))
            {
                throw new ArgumentException("Persistent spell end condition text is required.", nameof(endConditionText));
            }

            if (ownerTurnStartsRemaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerTurnStartsRemaining));
            }

            ValidateSpellCastPhase(battleState, casterId);

            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, cost);

            battleState.PersistentEffects.Add(new PersistentEffectState(
                sourceCardId: cardId,
                ownerId: casterId,
                effectId: effectId,
                appliedTurn: battleState.TurnNumber,
                endConditionText: endConditionText,
                turnStartResourceGain: turnStartResourceGain.Clone(),
                ownerTurnStartsRemaining: ownerTurnStartsRemaining));
        }

        private void CastResolvedScriptedSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            bool hasTarget,
            PlayerId targetOwnerId,
            TileCoord targetCoord)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Spell card id is required.", nameof(cardId));
            }

            var definition = GetRequiredDefinition<ScriptedSpellCardDefinition>(cardId);
            ValidateSpellCastPhase(battleState, casterId);

            switch (definition.EffectId)
            {
                case DaehwandanEffectId:
                    if (hasTarget)
                    {
                        throw new InvalidOperationException("This scripted spell does not use a board target.");
                    }

                    CastDaehwandan(battleState, casterId, cardId, definition.Cost);
                    return;

                case CheonraJimangEffectId:
                    if (!hasTarget)
                    {
                        throw new InvalidOperationException("This scripted spell requires a target.");
                    }

                    CastCheonraJimang(battleState, casterId, cardId, definition.Cost, targetOwnerId, targetCoord);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported scripted spell effect id '{definition.EffectId}'.");
            }
        }

        private static void CastDaehwandan(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ResourceSet cost)
        {
            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, cost);
            caster.Master.IncreaseBaseAttack(DaehwandanMasterAttackBonus);

            battleState.PersistentEffects.Add(new PersistentEffectState(
                sourceCardId: cardId,
                ownerId: casterId,
                effectId: DaehwandanEffectId,
                appliedTurn: battleState.TurnNumber,
                endConditionText: "At the start of your next three turns, gain qi +3.",
                turnStartResourceGain: new ResourceSet(mana: 0, qi: DaehwandanQiGain, power: 0, gold: 0),
                ownerTurnStartsRemaining: DaehwandanOwnerTurnStarts));
        }

        private static void CastCheonraJimang(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ResourceSet cost,
            PlayerId targetOwnerId,
            TileCoord targetCoord)
        {
            var targetBoard = battleState.GetBoard(targetOwnerId);
            var target = targetBoard.GetOccupant(targetCoord);
            if (target == null)
            {
                throw new InvalidOperationException("Scripted spell target tile is empty.");
            }

            if (target.Kind != OccupantKind.Unit)
            {
                throw new InvalidOperationException("Cheonra Jimang can only target non-Master units.");
            }

            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, cost);

            battleState.PersistentEffects.Add(new PersistentEffectState(
                sourceCardId: cardId,
                ownerId: casterId,
                effectId: CheonraJimangEffectId,
                appliedTurn: battleState.TurnNumber,
                endConditionText: "Destroy the marked unit at the start of your next turn.",
                turnStartResourceGain: new ResourceSet(),
                ownerTurnStartsRemaining: 0,
                targetRuntimeId: target.RuntimeId));
        }

        private static void ApplyEffectDamageToDeclaredTarget(
            BattleState battleState,
            BoardState targetBoard,
            TileCoord targetCoord,
            int damage,
            ISet<OccupantState> affectedTargets)
        {
            var guardInfo = GuardService.Resolve(targetBoard, targetCoord);
            if (guardInfo.IsProtected)
            {
                affectedTargets.Add(guardInfo.Guard);
                var absorbedDamage = DamageResolutionRules.ApplyEffectDamage(guardInfo.Guard, damage);
                BattleValuePopupRecorder.RecordDamage(battleState, guardInfo.Guard, absorbedDamage);
                var remainingDamage = Math.Max(0, damage - absorbedDamage);
                if (remainingDamage > 0)
                {
                    affectedTargets.Add(guardInfo.OriginalTarget);
                    var overflowDamage = DamageResolutionRules.ApplyEffectDamage(guardInfo.OriginalTarget, remainingDamage);
                    BattleValuePopupRecorder.RecordDamage(battleState, guardInfo.OriginalTarget, overflowDamage);
                }

                return;
            }

            affectedTargets.Add(guardInfo.OriginalTarget);
            var actualDamage = DamageResolutionRules.ApplyEffectDamage(guardInfo.OriginalTarget, damage);
            BattleValuePopupRecorder.RecordDamage(battleState, guardInfo.OriginalTarget, actualDamage);
        }

        private static void RemoveDefeatedOccupants(BoardState board, IEnumerable<OccupantState> occupants)
        {
            foreach (var occupant in occupants)
            {
                if (occupant == null || occupant.Kind == OccupantKind.Master || occupant.CurrentHp > 0)
                {
                    continue;
                }

                if (board.GetOccupant(occupant.Position) == occupant)
                {
                    board.Remove(occupant.Position);
                }
            }
        }

        private static void ConsumeSpellCard(PlayerState caster, string cardId, ResourceSet cost)
        {
            if (!caster.Hand.Contains(cardId))
            {
                throw new InvalidOperationException("The spell card is not in the caster's hand.");
            }

            if (!caster.Resources.CanAfford(cost))
            {
                throw new InvalidOperationException("The player cannot afford this card.");
            }

            caster.Resources.Spend(cost);
            caster.Hand.Remove(cardId);
            caster.Discard.Add(cardId);
        }

        private TDefinition GetRequiredDefinition<TDefinition>(string cardId)
            where TDefinition : CardDefinition
        {
            if (_cardDefinitionProvider == null)
            {
                throw new InvalidOperationException("A card definition provider is required for definition-based spell casting.");
            }

            return _cardDefinitionProvider.GetRequired(cardId) as TDefinition
                ?? throw new InvalidOperationException("The specified spell card does not match the expected definition type.");
        }

        private static void ResolveSpellDefeat(BattleState battleState, IEnumerable<OccupantState> occupants)
        {
            foreach (var occupant in occupants)
            {
                if (occupant == null || occupant.Kind != OccupantKind.Master || occupant.CurrentHp > 0)
                {
                    continue;
                }

                battleState.EndBattle(battleState.GetOpponent(occupant.OwnerId).Id);
                return;
            }
        }
    }
}
