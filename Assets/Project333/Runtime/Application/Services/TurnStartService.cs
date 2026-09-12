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
    public sealed class TurnStartService
    {
        private const int BaseDrawCount = 1;
        private const int BaseGoldGain = 1;
        private const string CheonraJimangEffectId = "cheonra_jimang";
        private const string FirewallEffectId = "firewall";

        private readonly ScienceUpkeepService _scienceUpkeepService;
        private readonly RobotFactoryService _robotFactoryService;

        public TurnStartService()
            : this(
                new ScienceUpkeepService(),
                new RobotFactoryService(new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>())))
        {
        }

        public TurnStartService(ScienceUpkeepService scienceUpkeepService)
            : this(
                scienceUpkeepService,
                new RobotFactoryService(new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>())))
        {
        }

        public TurnStartService(ICardDefinitionProvider cardDefinitionProvider)
            : this(new ScienceUpkeepService(), new RobotFactoryService(cardDefinitionProvider))
        {
        }

        public TurnStartService(
            ScienceUpkeepService scienceUpkeepService,
            RobotFactoryService robotFactoryService)
        {
            _scienceUpkeepService = scienceUpkeepService ?? throw new ArgumentNullException(nameof(scienceUpkeepService));
            _robotFactoryService = robotFactoryService ?? throw new ArgumentNullException(nameof(robotFactoryService));
        }

        public void ResolveTurnStart(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (battleState.Phase != PhaseType.TurnStart)
            {
                throw new InvalidOperationException("Turn start can only be resolved during the TurnStart phase.");
            }

            if (battleState.IsEnded)
            {
                return;
            }

            battleState.ClearCardGenerationEvents();
            battleState.ClearResourceChangeEvents();
            battleState.ClearAreaSpellEffectEvents();

            ResolveScriptedTurnStartEffects(battleState);
            if (battleState.IsEnded)
            {
                return;
            }

            var activePlayer = battleState.GetPlayer(battleState.ActivePlayerId);

            RefreshTurnStartState(battleState);
            GainBaseResources(battleState, activePlayer);
            GainOccupantResources(battleState);

            if (!battleState.IsEnded)
            {
                ResolvePowerPlantEffects(battleState);
            }

            if (!battleState.IsEnded)
            {
                _scienceUpkeepService.ResolveUpkeep(battleState);
            }

            if (!battleState.IsEnded)
            {
                _robotFactoryService.Resolve(battleState);
            }

            if (!battleState.IsEnded)
            {
                ResolveAreaDamageEffects(battleState);
            }

            if (!battleState.IsEnded)
            {
                BattleDrawService.DrawCards(activePlayer, battleState, BaseDrawCount);
            }

            if (!battleState.IsEnded)
            {
                ResolveSealboundOwnerTurnStarts(battleState);
                battleState.SetPhase(PhaseType.Main);
            }
        }

        private static void ResolveScriptedTurnStartEffects(BattleState battleState)
        {
            var snapshot = new List<PersistentEffectState>(battleState.PersistentEffects);
            foreach (var persistentEffect in snapshot)
            {
                if (persistentEffect.IsExpired)
                {
                    continue;
                }

                if (string.Equals(persistentEffect.EffectId, TimedBombRules.EffectId, StringComparison.Ordinal))
                {
                    ResolveTimedBomb(battleState, persistentEffect);
                    if (battleState.IsEnded)
                    {
                        return;
                    }

                    continue;
                }

                if (persistentEffect.OwnerId == battleState.ActivePlayerId &&
                    string.Equals(persistentEffect.EffectId, CheonraJimangEffectId, StringComparison.Ordinal))
                {
                    ResolveCheonraJimang(battleState, persistentEffect);
                }
            }
        }

        private static void ResolveTimedBomb(BattleState battleState, PersistentEffectState effect)
        {
            if (effect.RemainingTriggers <= 0)
            {
                effect.Expire();
                return;
            }

            if (effect.RemainingTriggers > 1)
            {
                effect.ResolveTrigger();
                return;
            }

            var targetPlayer = battleState.GetOpponent(effect.OwnerId);
            var targetBoard = battleState.GetBoard(targetPlayer.Id);
            var targets = new List<OccupantState>(targetBoard.EnumerateOccupants());

            foreach (var target in targets)
            {
                if (target.IsSealbound || targetBoard.GetOccupant(target.Position) != target)
                {
                    continue;
                }

                var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                    target,
                    effect.EffectDamage,
                    effect.EffectDamageType);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    target,
                    actualDamage,
                    BattleValueChangeCause.Spell,
                    effect.EffectDamageType,
                    effect.OwnerId,
                    sourceCardId: effect.SourceCardId);
            }

            effect.ResolveTrigger();
            OccupantDestructionService.ResolveDefeatedOccupants(battleState);
        }

        private static void ResolveCheonraJimang(BattleState battleState, PersistentEffectState persistentEffect)
        {
            if (!string.IsNullOrWhiteSpace(persistentEffect.TargetRuntimeId) &&
                TryFindOccupantByRuntimeId(battleState.PlayerBoard, persistentEffect.TargetRuntimeId, out var playerTarget))
            {
                if (!playerTarget.IsSealbound)
                {
                    OccupantDestructionService.DestroyOccupant(
                        battleState,
                        PlayerId.Player,
                        playerTarget);
                }
            }
            else if (!string.IsNullOrWhiteSpace(persistentEffect.TargetRuntimeId) &&
                     TryFindOccupantByRuntimeId(battleState.AIBoard, persistentEffect.TargetRuntimeId, out var aiTarget))
            {
                if (!aiTarget.IsSealbound)
                {
                    OccupantDestructionService.DestroyOccupant(
                        battleState,
                        PlayerId.AI,
                        aiTarget);
                }
            }

            persistentEffect.Expire();
        }

        private static bool TryFindOccupantByRuntimeId(BoardState boardState, string runtimeId, out OccupantState occupant)
        {
            foreach (var candidate in boardState.EnumerateOccupants())
            {
                if (candidate.RuntimeId == runtimeId)
                {
                    occupant = candidate;
                    return true;
                }
            }

            occupant = null;
            return false;
        }

        private static void ResolveAreaDamageEffects(BattleState battleState)
        {
            var effects = new List<PersistentEffectState>(battleState.PersistentEffects);
            foreach (var effect in effects)
            {
                if (battleState.IsEnded)
                {
                    return;
                }

                if (effect == null || effect.IsExpired || effect.RemainingTriggers <= 0)
                {
                    continue;
                }

                if (string.Equals(effect.EffectId, FirewallEffectId, StringComparison.Ordinal))
                {
                    ResolveFirewallEffect(battleState, effect);
                }
                else if (string.Equals(effect.EffectId, BiochemicalBombRules.EffectId, StringComparison.Ordinal))
                {
                    ResolveBiochemicalBombEffect(battleState, effect);
                }
            }
        }

        private static void ResolveFirewallEffect(BattleState battleState, PersistentEffectState effect)
        {
            var targetPlayerId = effect.TargetsOwnerBoard
                ? effect.OwnerId
                : battleState.GetOpponent(effect.OwnerId).Id;
            var targetBoard = battleState.GetBoard(targetPlayerId);
            var resolvedEffectDamage = SpellPowerRules.ApplyCaptured(
                effect.EffectDamage,
                effect.EffectDamageType,
                effect.CapturedSpellPower);
            var targets = new List<OccupantState>();
            var targetCoords = new List<TileCoord>(BoardState.ColumnCount);
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                targetCoords.Add(new TileCoord(column, effect.TargetRow));
            }

            battleState.RecordAreaSpellEffect(new BattleAreaSpellEffectEvent(
                FirewallEffectId,
                effect.SourceCardId,
                effect.OwnerId,
                targetPlayerId,
                targetCoords));

            foreach (var occupant in targetBoard.EnumerateOccupants())
            {
                if (occupant.Position.Row == effect.TargetRow)
                {
                    targets.Add(occupant);
                }
            }

            foreach (var target in targets)
            {
                if (target.IsSealbound)
                {
                    continue;
                }

                var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                    target,
                    resolvedEffectDamage,
                    effect.EffectDamageType);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    target,
                    actualDamage,
                    BattleValueChangeCause.Firewall,
                    effect.EffectDamageType,
                    effect.OwnerId,
                    sourceCardId: effect.SourceCardId);
            }

            effect.ResolveTrigger();
            OccupantDestructionService.ResolveDefeatedOccupants(battleState);
        }

        private static void ResolveBiochemicalBombEffect(
            BattleState battleState,
            PersistentEffectState effect)
        {
            if (effect.TargetStartColumn != BiochemicalBombRules.LeftAreaStartColumn &&
                effect.TargetStartColumn != BiochemicalBombRules.RightAreaStartColumn)
            {
                effect.Expire();
                return;
            }

            var targetPlayer = battleState.GetOpponent(effect.OwnerId);
            var targetBoard = battleState.GetBoard(targetPlayer.Id);
            var resolvedEffectDamage = SpellPowerRules.ApplyCaptured(
                effect.EffectDamage, effect.EffectDamageType, effect.CapturedSpellPower);
            var targets = new List<OccupantState>();
            var targetCoords = new List<TileCoord>(BiochemicalBombRules.AreaWidth * BoardState.RowCount);
            for (var column = effect.TargetStartColumn;
                 column < effect.TargetStartColumn + BiochemicalBombRules.AreaWidth;
                 column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    var targetCoord = new TileCoord(column, row);
                    targetCoords.Add(targetCoord);
                    var target = targetBoard.GetOccupant(targetCoord);
                    if (target != null)
                    {
                        targets.Add(target);
                    }
                }
            }

            battleState.RecordAreaSpellEffect(new BattleAreaSpellEffectEvent(
                BiochemicalBombRules.EffectId,
                effect.SourceCardId,
                effect.OwnerId,
                targetPlayer.Id,
                targetCoords));

            foreach (var target in targets)
            {
                if (target.IsSealbound || targetBoard.GetOccupant(target.Position) != target)
                {
                    continue;
                }

                var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                    target,
                    resolvedEffectDamage,
                    effect.EffectDamageType);
                BattleValuePopupRecorder.RecordDamage(
                    battleState,
                    target,
                    actualDamage,
                    BattleValueChangeCause.BiochemicalBomb,
                    effect.EffectDamageType,
                    effect.OwnerId,
                    sourceCardId: effect.SourceCardId);
            }

            effect.ResolveTrigger();
            OccupantDestructionService.ResolveDefeatedOccupants(battleState);
        }

        private static void RefreshTurnStartState(BattleState battleState)
        {
            var activeBoard = battleState.GetBoard(battleState.ActivePlayerId);

            foreach (var occupant in activeBoard.EnumerateOccupants())
            {
                occupant.WasSummonedThisTurn = false;
                if (occupant.IsSealbound)
                {
                    occupant.HasSummoningSickness = true;
                    occupant.RemainingAttacksThisTurn = 0;
                    continue;
                }

                occupant.HasSummoningSickness = false;
                occupant.RemainingAttacksThisTurn = occupant.EffectiveMaxAttacksPerTurn;
            }
        }

        private static void ResolveSealboundOwnerTurnStarts(BattleState battleState)
        {
            ResolveDemonKingRevivalTurnStarts(battleState, battleState.PlayerBoard);
            ResolveDemonKingRevivalTurnStarts(battleState, battleState.AIBoard);

            var activeBoard = battleState.GetBoard(battleState.ActivePlayerId);
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    var occupant = activeBoard.GetOccupant(new TileCoord(column, row));
                    if (occupant?.IsDemonKingRevivalPending != true)
                    {
                        occupant?.ResolveSealboundOwnerTurnStart();
                    }
                }
            }
        }

        private static void ResolveDemonKingRevivalTurnStarts(
            BattleState battleState,
            BoardState board)
        {
            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    var occupant = board.GetOccupant(new TileCoord(column, row));
                    occupant?.ResolveDemonKingRevivalTurnStart(
                        battleState.ActivePlayerId,
                        battleState.TurnNumber);
                }
            }
        }

        private static void GainBaseResources(BattleState battleState, PlayerState playerState)
        {
            // The first player keeps the initial 3 gold on the first battle turn.
            var goldGain = battleState.TurnNumber == 1 ? 0 : BaseGoldGain;
            playerState.Resources.Add(new ResourceSet(
                mana: 0,
                qi: 0,
                power: 0,
                gold: goldGain));
        }

        private static void GainOccupantResources(BattleState battleState)
        {
            var activePlayer = battleState.GetPlayer(battleState.ActivePlayerId);
            var activeBoard = battleState.GetBoard(battleState.ActivePlayerId);

            foreach (var occupant in activeBoard.EnumerateOccupants())
            {
                if (occupant.EffectsSuppressed)
                {
                    continue;
                }

                if (!HasAnyResourceGain(occupant.TurnStartResourceGain))
                {
                    continue;
                }

                var resourceGain = occupant.TurnStartResourceGain.Clone();
                activePlayer.Resources.Add(resourceGain);
                battleState.RecordResourceChange(new BattleResourceChangeEvent(
                    activePlayer.Id,
                    occupant.CardId,
                    gained: resourceGain));
            }

            foreach (var persistentEffect in battleState.PersistentEffects)
            {
                if (persistentEffect.IsExpired || persistentEffect.OwnerId != battleState.ActivePlayerId)
                {
                    continue;
                }

                if (HasAnyResourceGain(persistentEffect.TurnStartResourceGain))
                {
                    var resourceGain = persistentEffect.TurnStartResourceGain.Clone();
                    activePlayer.Resources.Add(resourceGain);
                    battleState.RecordResourceChange(new BattleResourceChangeEvent(
                        activePlayer.Id,
                        persistentEffect.SourceCardId,
                        gained: resourceGain));
                }

                persistentEffect.ResolveOwnerTurnStart();
            }
        }

        private static void ResolvePowerPlantEffects(BattleState battleState)
        {
            var activePlayer = battleState.GetPlayer(battleState.ActivePlayerId);
            var activeBoard = battleState.GetBoard(battleState.ActivePlayerId);
            var triggerCost = new ResourceSet(
                mana: 0,
                qi: 0,
                power: 0,
                gold: PowerPlantRules.TriggerGoldCost);

            for (var column = 0; column < BoardState.ColumnCount; column++)
            {
                for (var row = 0; row < BoardState.RowCount; row++)
                {
                    var occupant = activeBoard.GetOccupant(new TileCoord(column, row));
                    if (occupant == null ||
                        !string.Equals(occupant.CardId, PowerPlantRules.CardId, StringComparison.Ordinal) ||
                        occupant.EffectsSuppressed ||
                        !activePlayer.Resources.CanAfford(triggerCost))
                    {
                        continue;
                    }

                    var resourceGain = new ResourceSet(
                        mana: 0,
                        qi: 0,
                        power: PowerPlantRules.PowerGain,
                        gold: 0);
                    activePlayer.Resources.Spend(triggerCost);
                    activePlayer.Resources.Add(resourceGain);
                    battleState.RecordResourceChange(new BattleResourceChangeEvent(
                        activePlayer.Id,
                        occupant.CardId,
                        gained: resourceGain,
                        spent: triggerCost));
                }
            }
        }

        private static bool HasAnyResourceGain(ResourceSet resourceSet)
        {
            return resourceSet.Mana > 0 ||
                   resourceSet.Qi > 0 ||
                   resourceSet.Power > 0 ||
                   resourceSet.Gold > 0;
        }
    }
}
