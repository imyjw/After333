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
        private const string FirewallEffectId = "firewall";
        private const int DaehwandanMasterAttackBonus = 30;
        private const int DaehwandanQiGain = 3;
        private const int DaehwandanOwnerTurnStarts = 3;

        private readonly ICardDefinitionProvider _cardDefinitionProvider;
        private readonly ICardUpgradeLevelProvider _cardUpgradeLevelProvider = ZeroCardUpgradeLevelProvider.Instance;

        public SpellService()
        {
        }

        public SpellService(ICardDefinitionProvider cardDefinitionProvider)
            : this(cardDefinitionProvider, ZeroCardUpgradeLevelProvider.Instance)
        {
        }

        public SpellService(
            ICardDefinitionProvider cardDefinitionProvider,
            ICardUpgradeLevelProvider cardUpgradeLevelProvider)
        {
            _cardDefinitionProvider = cardDefinitionProvider ?? throw new ArgumentNullException(nameof(cardDefinitionProvider));
            _cardUpgradeLevelProvider = cardUpgradeLevelProvider ?? ZeroCardUpgradeLevelProvider.Instance;
        }

        public void CastDamageSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            string handCardRuntimeId = null)
        {
            var definition = GetRequiredDefinition<DamageSpellCardDefinition>(cardId);
            CastResolvedDamageSpell(
                battleState,
                casterId,
                cardId,
                targetOwnerId,
                targetCoord,
                CardLevelSpellRules.ApplyDamageBonus(
                    definition.Damage,
                    _cardUpgradeLevelProvider.GetUpgradeLevel(casterId, definition.CardId)),
                definition.DamageType,
                definition.Cost,
                handCardRuntimeId);
        }

        public void CastDamageSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            int damage,
            DamageType damageType = DamageType.Magic)
        {
            CastResolvedDamageSpell(
                battleState,
                casterId,
                cardId,
                targetOwnerId,
                targetCoord,
                damage,
                damageType,
                new ResourceSet(),
                handCardRuntimeId: null);
        }

        public void CastPersistentResourceSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            string handCardRuntimeId = null)
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
                definition.OwnerTurnStartsRemaining,
                handCardRuntimeId);
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
                ownerTurnStartsRemaining,
                handCardRuntimeId: null);
        }

        public void CastScriptedSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            string handCardRuntimeId = null)
        {
            CastResolvedScriptedSpell(
                battleState,
                casterId,
                cardId,
                hasTarget: false,
                PlayerId.Player,
                default(TileCoord),
                handCardRuntimeId);
        }

        public void CastScriptedSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            string handCardRuntimeId = null)
        {
            CastResolvedScriptedSpell(
                battleState,
                casterId,
                cardId,
                hasTarget: true,
                targetOwnerId,
                targetCoord,
                handCardRuntimeId);
        }

        public RobotFusionResult CastScriptedSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            IReadOnlyList<TileCoord> targetCoords)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Spell card id is required.", nameof(cardId));
            }

            var definition = GetRequiredDefinition<ScriptedSpellCardDefinition>(cardId);
            ValidateSpellCastPhase(battleState, casterId);
            if (!string.Equals(definition.EffectId, RobotFusionRules.EffectId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Scripted spell effect '{definition.EffectId}' does not accept ordered Robot targets.");
            }

            var pending = battleState.PendingRobotFusion;
            if (pending == null ||
                pending.OwnerId != casterId ||
                !string.Equals(pending.CardId, cardId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Robot Fusion must enter selection mode before it can resolve.");
            }

            var result = RobotFusionRules.Resolve(battleState, casterId, targetCoords);
            battleState.CompleteRobotFusion(casterId, cardId);
            return result;
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
            DamageType damageType,
            ResourceSet cost,
            string handCardRuntimeId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Spell card id is required.", nameof(cardId));
            }

            ValidateSpellCastPhase(battleState, casterId);

            var caster = battleState.GetPlayer(casterId);
            var targetBoard = battleState.GetBoard(targetOwnerId);
            var declaredTarget = targetBoard.GetOccupant(targetCoord);
            if (declaredTarget == null)
            {
                throw new InvalidOperationException("Damage spell target tile is empty.");
            }

            EnsureSingleTargetSpellCanTarget(casterId, targetOwnerId, declaredTarget);

            var resolvedDamage = SpellPowerRules.ApplyCurrent(
                battleState,
                casterId,
                damage,
                damageType);

            ConsumeSpellCard(caster, cardId, cost, handCardRuntimeId);

            var affectedTargets = new HashSet<OccupantState>();
            ApplyEffectDamageToDeclaredTarget(
                battleState,
                targetBoard,
                targetCoord,
                resolvedDamage,
                damageType,
                casterId,
                cardId,
                affectedTargets);

            OccupantDestructionService.ResolveDefeatedOccupants(battleState);
        }

        private void CastResolvedPersistentResourceSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            string effectId,
            ResourceSet turnStartResourceGain,
            string endConditionText,
            ResourceSet cost,
            int ownerTurnStartsRemaining,
            string handCardRuntimeId)
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
            ConsumeSpellCard(caster, cardId, cost, handCardRuntimeId);

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
            TileCoord targetCoord,
            string handCardRuntimeId)
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

                    CastDaehwandan(battleState, casterId, cardId, definition.Cost, handCardRuntimeId);
                    return;

                case CheonraJimangEffectId:
                    if (!hasTarget)
                    {
                        throw new InvalidOperationException("This scripted spell requires a target.");
                    }

                    CastCheonraJimang(
                        battleState,
                        casterId,
                        cardId,
                        definition.Cost,
                        targetOwnerId,
                        targetCoord,
                        handCardRuntimeId);
                    return;

                case FirewallEffectId:
                    if (!hasTarget)
                    {
                        throw new InvalidOperationException("Firewall requires an enemy row target.");
                    }

                    CastFirewall(
                        battleState,
                        casterId,
                        cardId,
                        definition,
                        targetOwnerId,
                        targetCoord,
                        handCardRuntimeId);
                    return;

                case TimedBombRules.EffectId:
                    if (hasTarget)
                    {
                        throw new InvalidOperationException("Timed Bomb does not use a board target.");
                    }

                    CastTimedBomb(
                        battleState,
                        casterId,
                        cardId,
                        definition,
                        handCardRuntimeId);
                    return;

                case BiochemicalBombRules.EffectId:
                    if (!hasTarget)
                    {
                        throw new InvalidOperationException("Biochemical Bomb requires an enemy 4x2 area target.");
                    }

                    CastBiochemicalBomb(
                        battleState,
                        casterId,
                        cardId,
                        definition,
                        targetOwnerId,
                        targetCoord,
                        handCardRuntimeId);
                    return;

                case PowerBankRules.EffectId:
                    if (hasTarget)
                    {
                        throw new InvalidOperationException("Power Bank does not use a board target.");
                    }

                    CastImmediateResourceSpell(
                        battleState,
                        casterId,
                        cardId,
                        definition.Cost,
                        new ResourceSet(mana: 0, qi: 0, power: PowerBankRules.PowerGain, gold: 0),
                        handCardRuntimeId);
                    return;

                case ManaStoneRules.EffectId:
                    if (hasTarget)
                    {
                        throw new InvalidOperationException("Mana Stone does not use a board target.");
                    }

                    CastImmediateResourceSpell(
                        battleState,
                        casterId,
                        cardId,
                        definition.Cost,
                        new ResourceSet(mana: ManaStoneRules.ManaGain, qi: 0, power: 0, gold: 0),
                        handCardRuntimeId);
                    return;

                case ManaStoneBundleRules.EffectId:
                    if (hasTarget)
                    {
                        throw new InvalidOperationException("Mana Stone Bundle does not use a board target.");
                    }

                    CastImmediateResourceSpell(
                        battleState,
                        casterId,
                        cardId,
                        definition.Cost,
                        new ResourceSet(mana: ManaStoneBundleRules.ManaGain, qi: 0, power: 0, gold: 0),
                        handCardRuntimeId);
                    return;

                case RobotFusionRules.EffectId:
                    if (hasTarget)
                    {
                        throw new InvalidOperationException("Robot Fusion uses ordered friendly Robot targets.");
                    }

                    BeginRobotFusion(battleState, casterId, cardId, definition.Cost, handCardRuntimeId);
                    return;

                case GuRules.EffectId:
                    if (!hasTarget)
                    {
                        throw new InvalidOperationException("Gu requires an enemy Unit target.");
                    }

                    CastGu(
                        battleState,
                        casterId,
                        cardId,
                        definition.Cost,
                        targetOwnerId,
                        targetCoord,
                        handCardRuntimeId);
                    return;

                case HuanShuRules.EffectId:
                    if (!hasTarget)
                    {
                        throw new InvalidOperationException("HuanShu requires an enemy Unit target.");
                    }

                    CastHuanShu(
                        battleState,
                        casterId,
                        cardId,
                        definition.Cost,
                        targetOwnerId,
                        targetCoord,
                        handCardRuntimeId);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported scripted spell effect id '{definition.EffectId}'.");
            }
        }

        private static void BeginRobotFusion(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ResourceSet cost,
            string handCardRuntimeId)
        {
            if (battleState.PendingRobotFusion != null)
            {
                throw new InvalidOperationException("A Robot Fusion selection is already pending.");
            }

            if (!RobotFusionRules.CanBegin(battleState, casterId))
            {
                throw new InvalidOperationException(
                    "Robot Fusion requires at least two living Robot units on the caster's field.");
            }

            ConsumeSpellCard(battleState.GetPlayer(casterId), cardId, cost, handCardRuntimeId);
            battleState.BeginRobotFusion(casterId, cardId);
        }

        private static void CastDaehwandan(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ResourceSet cost,
            string handCardRuntimeId)
        {
            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, cost, handCardRuntimeId);
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
            TileCoord targetCoord,
            string handCardRuntimeId)
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

            EnsureSingleTargetSpellCanTarget(casterId, targetOwnerId, target);

            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, cost, handCardRuntimeId);

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

        private static void CastGu(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ResourceSet cost,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            string handCardRuntimeId)
        {
            if (!GuRules.TryFindDestination(battleState, casterId, out var destination))
            {
                throw new InvalidOperationException("Gu requires an empty tile on the caster's field.");
            }

            if (!GuRules.IsLegalTarget(battleState, casterId, targetOwnerId, targetCoord))
            {
                throw new InvalidOperationException(
                    "Gu can only target a living, non-Sealbound, non-Hiding enemy Unit.");
            }

            var targetBoard = battleState.GetBoard(targetOwnerId);
            var target = targetBoard.GetOccupant(targetCoord);
            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, cost, handCardRuntimeId);

            targetBoard.Remove(targetCoord);
            target.TransferOwnership(casterId);
            target.SetBattleTurnContext(battleState.ActivePlayerId, battleState.TurnNumber);

            var canAttackImmediately = target.HasRush && !target.EffectsSuppressed;
            target.HasSummoningSickness = !canAttackImmediately;
            target.RemainingAttacksThisTurn = canAttackImmediately
                ? target.EffectiveMaxAttacksPerTurn
                : 0;

            battleState.GetBoard(casterId).Place(destination, target);
        }

        private static void CastHuanShu(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ResourceSet cost,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            string handCardRuntimeId)
        {
            if (!HuanShuRules.IsLegalTarget(
                    battleState,
                    casterId,
                    targetOwnerId,
                    targetCoord))
            {
                throw new InvalidOperationException(
                    "HuanShu can only target a living, non-Sealbound, non-Hiding enemy Unit.");
            }

            var target = battleState.GetBoard(targetOwnerId).GetOccupant(targetCoord);
            ConsumeSpellCard(
                battleState.GetPlayer(casterId),
                cardId,
                cost,
                handCardRuntimeId);
            target.ApplyHuanShu();
        }

        private void CastFirewall(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ScriptedSpellCardDefinition definition,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            string handCardRuntimeId)
        {
            var opponentId = battleState.GetOpponent(casterId).Id;
            if (targetOwnerId != casterId && targetOwnerId != opponentId)
            {
                throw new InvalidOperationException("Firewall can only target a row on either player's board.");
            }

            var targetBoard = battleState.GetBoard(targetOwnerId);
            if (!targetBoard.IsInside(targetCoord))
            {
                throw new InvalidOperationException("Firewall target row is outside the opponent board.");
            }

            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, definition.Cost, handCardRuntimeId);

            var upgradeLevel = _cardUpgradeLevelProvider.GetUpgradeLevel(casterId, definition.CardId);
            var effectDamage = CardLevelSpellRules.ApplyDamageBonus(definition.Damage, upgradeLevel);
            var capturedSpellPower = SpellPowerRules.Capture(
                battleState,
                casterId,
                definition.DamageType);
            battleState.PersistentEffects.Add(new PersistentEffectState(
                sourceCardId: cardId,
                ownerId: casterId,
                effectId: FirewallEffectId,
                appliedTurn: battleState.TurnNumber,
                endConditionText: "Triggers at the next two turn starts, then expires.",
                turnStartResourceGain: new ResourceSet(),
                targetRow: targetCoord.Row,
                remainingTriggers: definition.TriggerCount,
                effectDamage: effectDamage,
                effectDamageType: definition.DamageType,
                targetsOwnerBoard: targetOwnerId == casterId,
                capturedSpellPower: capturedSpellPower));
        }

        private void CastTimedBomb(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ScriptedSpellCardDefinition definition,
            string handCardRuntimeId)
        {
            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, definition.Cost, handCardRuntimeId);

            var upgradeLevel = _cardUpgradeLevelProvider.GetUpgradeLevel(casterId, definition.CardId);
            var effectDamage = CardLevelSpellRules.ApplyDamageBonus(definition.Damage, upgradeLevel);
            battleState.PersistentEffects.Add(new PersistentEffectState(
                sourceCardId: cardId,
                ownerId: casterId,
                effectId: TimedBombRules.EffectId,
                appliedTurn: battleState.TurnNumber,
                endConditionText: "Detonates at the third turn start after it is cast.",
                turnStartResourceGain: new ResourceSet(),
                remainingTriggers: definition.TriggerCount,
                effectDamage: effectDamage,
                effectDamageType: definition.DamageType));
        }

        private void CastBiochemicalBomb(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ScriptedSpellCardDefinition definition,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            string handCardRuntimeId)
        {
            if (targetOwnerId != battleState.GetOpponent(casterId).Id)
            {
                throw new InvalidOperationException("Biochemical Bomb can only target the opponent board.");
            }

            if (!BiochemicalBombRules.IsValidTargetStart(targetCoord))
            {
                throw new InvalidOperationException("Biochemical Bomb target must be the left or right 4x2 area.");
            }

            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, definition.Cost, handCardRuntimeId);

            var upgradeLevel = _cardUpgradeLevelProvider.GetUpgradeLevel(casterId, definition.CardId);
            var effectDamage = CardLevelSpellRules.ApplyDamageBonus(definition.Damage, upgradeLevel);
            battleState.PersistentEffects.Add(new PersistentEffectState(
                sourceCardId: cardId,
                ownerId: casterId,
                effectId: BiochemicalBombRules.EffectId,
                appliedTurn: battleState.TurnNumber,
                endConditionText: "Triggers at the next four global turn starts, then expires.",
                turnStartResourceGain: new ResourceSet(),
                remainingTriggers: definition.TriggerCount,
                effectDamage: effectDamage,
                effectDamageType: definition.DamageType,
                targetStartColumn: targetCoord.Column));
        }

        private static void CastImmediateResourceSpell(
            BattleState battleState,
            PlayerId casterId,
            string cardId,
            ResourceSet cost,
            ResourceSet resourceGain,
            string handCardRuntimeId)
        {
            var caster = battleState.GetPlayer(casterId);
            ConsumeSpellCard(caster, cardId, cost, handCardRuntimeId);
            caster.Resources.Add(resourceGain);
            battleState.RecordResourceChange(new BattleResourceChangeEvent(
                casterId,
                cardId,
                gained: resourceGain));
        }

        private static void EnsureSingleTargetSpellCanTarget(
            PlayerId casterId,
            PlayerId targetOwnerId,
            OccupantState target)
        {
            if (target.IsSealbound)
            {
                throw new InvalidOperationException("Sealbound occupants cannot be targeted by spells.");
            }

            if (targetOwnerId != casterId && target.IsHiding)
            {
                throw new InvalidOperationException("Hiding units cannot be targeted by enemy single-target spells.");
            }
        }

        private static void ApplyEffectDamageToDeclaredTarget(
            BattleState battleState,
            BoardState targetBoard,
            TileCoord targetCoord,
            int damage,
            DamageType damageType,
            PlayerId casterId,
            string sourceCardId,
            ISet<OccupantState> affectedTargets)
        {
            var shielderInfo = ShielderService.Resolve(targetBoard, targetCoord);
            if (shielderInfo.IsProtected)
            {
                affectedTargets.Add(shielderInfo.Shielder);
                var shielderHpBefore = Math.Max(0, shielderInfo.Shielder.CurrentHp);
                var resolvedShielderDamage = DamageResolutionRules.ResolveIncomingDamage(
                    shielderInfo.Shielder,
                    damage,
                    damageType);
                var absorbedDamage = DamageResolutionRules.ApplyEffectDamage(
                    shielderInfo.Shielder,
                    damage,
                    damageType);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    shielderInfo.Shielder,
                    absorbedDamage,
                    BattleValueChangeCause.Spell,
                    damageType,
                    casterId,
                    sourceCardId: sourceCardId);
                var remainingDamage = Math.Max(0, resolvedShielderDamage - shielderHpBefore);
                if (remainingDamage > 0)
                {
                    affectedTargets.Add(shielderInfo.OriginalTarget);
                    var overflowDamage = DamageResolutionRules.ApplyEffectDamage(
                        shielderInfo.OriginalTarget,
                        remainingDamage,
                        damageType);
                    BattleValuePopupRecorder.RecordDamage(
                        battleState,
                        shielderInfo.OriginalTarget,
                        overflowDamage,
                        BattleValueChangeCause.Spell,
                        damageType,
                        casterId,
                        sourceCardId: sourceCardId);
                }

                return;
            }

            affectedTargets.Add(shielderInfo.OriginalTarget);
            var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                shielderInfo.OriginalTarget,
                damage,
                damageType);
            BattleValuePopupRecorder.RecordDamage(
                battleState,
                shielderInfo.OriginalTarget,
                actualDamage,
                BattleValueChangeCause.Spell,
                damageType,
                casterId,
                sourceCardId: sourceCardId);
        }

        private static void ConsumeSpellCard(
            PlayerState caster,
            string cardId,
            ResourceSet cost,
            string handCardRuntimeId)
        {
            if (!caster.Hand.Contains(cardId, handCardRuntimeId))
            {
                throw new InvalidOperationException("The spell card is not in the caster's hand.");
            }

            if (!caster.Resources.CanAfford(cost))
            {
                throw new InvalidOperationException("The player cannot afford this card.");
            }

            caster.Resources.Spend(cost);
            if (!caster.Hand.Remove(cardId, handCardRuntimeId))
            {
                throw new InvalidOperationException("The selected spell card could not be consumed.");
            }
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

    }
}
