using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class PlayCardService
    {
        private readonly ICardDefinitionProvider _cardDefinitionProvider;
        private readonly SummonService _summonService;
        private readonly ICardUpgradeLevelProvider _cardUpgradeLevelProvider;

        internal ICardDefinitionProvider CardDefinitionProvider => _cardDefinitionProvider;
        internal ICardUpgradeLevelProvider CardUpgradeLevelProvider => _cardUpgradeLevelProvider;

        public PlayCardService(ICardDefinitionProvider cardDefinitionProvider)
            : this(cardDefinitionProvider, new SummonService(), ZeroCardUpgradeLevelProvider.Instance)
        {
        }

        public PlayCardService(ICardDefinitionProvider cardDefinitionProvider, SummonService summonService)
            : this(cardDefinitionProvider, summonService, ZeroCardUpgradeLevelProvider.Instance)
        {
        }

        public PlayCardService(
            ICardDefinitionProvider cardDefinitionProvider,
            SummonService summonService,
            ICardUpgradeLevelProvider cardUpgradeLevelProvider)
        {
            _cardDefinitionProvider = cardDefinitionProvider ?? throw new ArgumentNullException(nameof(cardDefinitionProvider));
            _summonService = summonService ?? throw new ArgumentNullException(nameof(summonService));
            _cardUpgradeLevelProvider = cardUpgradeLevelProvider ?? ZeroCardUpgradeLevelProvider.Instance;
        }

        public UnitState PlayUnitCard(
            BattleState battleState,
            PlayerId playerId,
            string cardId,
            TileCoord targetCoord,
            string handCardRuntimeId = null)
        {
            ValidateCommonPlayRequirements(battleState, playerId, cardId, targetCoord, handCardRuntimeId);

            var definition = _cardDefinitionProvider.GetRequired(cardId) as UnitCardDefinition
                ?? throw new InvalidOperationException("The specified card is not a unit card.");

            return PlayUnitCardInternal(battleState, playerId, definition, targetCoord, handCardRuntimeId);
        }

        public BuildingState PlayBuildingCard(
            BattleState battleState,
            PlayerId playerId,
            string cardId,
            TileCoord targetCoord,
            string handCardRuntimeId = null)
        {
            ValidateCommonPlayRequirements(battleState, playerId, cardId, targetCoord, handCardRuntimeId);

            var definition = _cardDefinitionProvider.GetRequired(cardId) as BuildingCardDefinition
                ?? throw new InvalidOperationException("The specified card is not a building card.");

            return PlayBuildingCardInternal(battleState, playerId, definition, targetCoord, handCardRuntimeId);
        }

        private UnitState PlayUnitCardInternal(
            BattleState battleState,
            PlayerId playerId,
            UnitCardDefinition definition,
            TileCoord targetCoord,
            string handCardRuntimeId)
        {
            battleState.ClearSummonedOccupant();
            var playerState = battleState.GetPlayer(playerId);
            EnsureCardCanBeAfforded(playerState, definition);
            EnsureSummonTargetIsValid(battleState, playerId, targetCoord);

            var upgradeLevel = _cardUpgradeLevelProvider.GetUpgradeLevel(playerId, definition.CardId);
            var unit = new UnitState(
                runtimeId: CreateRuntimeId(playerId, definition.CardId),
                cardId: definition.CardId,
                ownerId: playerId,
                position: targetCoord,
                attackType: definition.AttackType,
                attack: CardLevelStatRules.ApplyAttackBonus(definition.CardId, definition.Attack, upgradeLevel),
                maxHp: CardLevelStatRules.ApplyHpBonus(definition.CardId, definition.Health, upgradeLevel),
                canMove: definition.CanMove,
                isScience: definition.IsScience,
                sciencePowerUpkeep: definition.SciencePowerUpkeep,
                turnStartResourceGain: definition.TurnStartResourceGain.Clone(),
                maxAttacksPerTurn: definition.MaxAttacksPerTurn,
                hitsPerAttack: definition.HitsPerAttack,
                hasBerserker: definition.HasBerserker,
                hasEndure: definition.HasEndure,
                hasShielder: definition.HasShielder,
                hasLifeSteal: definition.HasLifeSteal,
                damageType: definition.DamageType,
                physicalDefense: definition.PhysicalDefense,
                magicDefense: definition.MagicDefense,
                races: definition.Races,
                hasRush: definition.HasRush,
                hasHiding: definition.HasHiding,
                hasFlying: definition.HasFlying,
                spellPower: definition.SpellPower,
                hasPiercing: definition.HasPiercing);

            playerState.Resources.Spend(definition.Cost);
            if (!playerState.Hand.Remove(definition.CardId, handCardRuntimeId))
            {
                throw new InvalidOperationException("The selected hand card could not be consumed.");
            }

            // Sealbound summons must already be protected when entering a contaminated board.
            if (definition.SealboundOwnerTurnStarts > 0) unit.EnterSealbound(definition.SealboundOwnerTurnStarts);
            _summonService.Summon(battleState, playerId, unit, targetCoord);

            if (definition.SealboundOwnerTurnStarts > 0)
            {
                unit.EnterSealbound(definition.SealboundOwnerTurnStarts);
            }
            else if (definition.HasRush)
            {
                unit.HasSummoningSickness = false;
            }

            unit.AddInvincibleEffect(
                definition.InvincibleDuration,
                definition.InvincibleOwnerTurns,
                battleState.TurnNumber,
                battleState.ActivePlayerId);

            battleState.RecordSummonedOccupant(unit);
            TrapService.TryResolveAcceptedUnitSummon(battleState, unit);

            return unit;
        }

        private BuildingState PlayBuildingCardInternal(
            BattleState battleState,
            PlayerId playerId,
            BuildingCardDefinition definition,
            TileCoord targetCoord,
            string handCardRuntimeId)
        {
            battleState.ClearSummonedOccupant();
            var playerState = battleState.GetPlayer(playerId);
            EnsureCardCanBeAfforded(playerState, definition);
            EnsureSummonTargetIsValid(battleState, playerId, targetCoord);

            var upgradeLevel = _cardUpgradeLevelProvider.GetUpgradeLevel(playerId, definition.CardId);
            var building = new BuildingState(
                runtimeId: CreateRuntimeId(playerId, definition.CardId),
                cardId: definition.CardId,
                ownerId: playerId,
                position: targetCoord,
                canAttack: definition.CanAttack,
                attack: CardLevelStatRules.ApplyAttackBonus(definition.CardId, definition.Attack, upgradeLevel),
                maxHp: CardLevelStatRules.ApplyHpBonus(definition.CardId, definition.Health, upgradeLevel),
                turnStartResourceGain: definition.TurnStartResourceGain.Clone(),
                damageType: definition.DamageType,
                physicalDefense: definition.PhysicalDefense,
                magicDefense: definition.MagicDefense,
                sciencePowerUpkeep: definition.SciencePowerUpkeep,
                hasFlying: definition.HasFlying,
                spellPower: definition.SpellPower,
                hasPiercing: definition.HasPiercing);

            playerState.Resources.Spend(definition.Cost);
            if (!playerState.Hand.Remove(definition.CardId, handCardRuntimeId))
            {
                throw new InvalidOperationException("The selected hand card could not be consumed.");
            }

            if (definition.SealboundOwnerTurnStarts > 0) building.EnterSealbound(definition.SealboundOwnerTurnStarts);
            _summonService.Summon(battleState, playerId, building, targetCoord);

            if (definition.SealboundOwnerTurnStarts > 0)
            {
                building.EnterSealbound(definition.SealboundOwnerTurnStarts);
            }
            else if (definition.CanAttackOnSummon)
            {
                building.HasSummoningSickness = false;
            }

            building.AddInvincibleEffect(
                definition.InvincibleDuration,
                definition.InvincibleOwnerTurns,
                battleState.TurnNumber,
                battleState.ActivePlayerId);

            return building;
        }

        private static void ValidateCommonPlayRequirements(
            BattleState battleState,
            PlayerId playerId,
            string cardId,
            TileCoord targetCoord,
            string handCardRuntimeId)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Card id is required.", nameof(cardId));
            }

            if (battleState.Phase != PhaseType.Main)
            {
                throw new InvalidOperationException("자신의 행동 턴에만 행동할 수 있습니다.");
            }

            if (battleState.ActivePlayerId != playerId)
            {
                throw new InvalidOperationException("자신의 턴에만 행동할 수 있습니다.");
            }

            battleState.ClearSummonedOccupant();
            var playerState = battleState.GetPlayer(playerId);
            if (!playerState.Hand.Contains(cardId, handCardRuntimeId))
            {
                throw new InvalidOperationException("해당 카드가 핸드에 없습니다.");
            }

            var board = battleState.GetBoard(playerId);
            if (!board.IsInside(targetCoord))
            {
                throw new InvalidOperationException("해당 위치에 카드를 사용할 수 없습니다.");
            }
        }

        private static void EnsureCardCanBeAfforded(PlayerState playerState, CardDefinition definition)
        {
            if (!playerState.Resources.CanAfford(definition.Cost))
            {
                throw new InvalidOperationException("자원이 부족합니다.");
            }
        }

        private static void EnsureSummonTargetIsValid(BattleState battleState, PlayerId playerId, TileCoord targetCoord)
        {
            var board = battleState.GetBoard(playerId);
            if (!board.IsEmpty(targetCoord))
            {
                throw new InvalidOperationException("해당 타일에는 소환물이 이미 존재합니다.");
            }
        }

        private static string CreateRuntimeId(PlayerId playerId, string cardId)
        {
            return $"{playerId}-{cardId}-{Guid.NewGuid():N}";
        }
    }
}
