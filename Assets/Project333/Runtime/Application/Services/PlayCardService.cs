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

        public PlayCardService(ICardDefinitionProvider cardDefinitionProvider)
            : this(cardDefinitionProvider, new SummonService())
        {
        }

        public PlayCardService(ICardDefinitionProvider cardDefinitionProvider, SummonService summonService)
        {
            _cardDefinitionProvider = cardDefinitionProvider ?? throw new ArgumentNullException(nameof(cardDefinitionProvider));
            _summonService = summonService ?? throw new ArgumentNullException(nameof(summonService));
        }

        public UnitState PlayUnitCard(BattleState battleState, PlayerId playerId, string cardId, TileCoord targetCoord)
        {
            ValidateCommonPlayRequirements(battleState, playerId, cardId, targetCoord);

            var definition = _cardDefinitionProvider.GetRequired(cardId) as UnitCardDefinition
                ?? throw new InvalidOperationException("The specified card is not a unit card.");

            return PlayUnitCardInternal(battleState, playerId, definition, targetCoord);
        }

        public BuildingState PlayBuildingCard(BattleState battleState, PlayerId playerId, string cardId, TileCoord targetCoord)
        {
            ValidateCommonPlayRequirements(battleState, playerId, cardId, targetCoord);

            var definition = _cardDefinitionProvider.GetRequired(cardId) as BuildingCardDefinition
                ?? throw new InvalidOperationException("The specified card is not a building card.");

            return PlayBuildingCardInternal(battleState, playerId, definition, targetCoord);
        }

        private UnitState PlayUnitCardInternal(BattleState battleState, PlayerId playerId, UnitCardDefinition definition, TileCoord targetCoord)
        {
            var playerState = battleState.GetPlayer(playerId);
            EnsureCardCanBeAfforded(playerState, definition);
            EnsureSummonTargetIsValid(battleState, playerId, targetCoord);

            var unit = new UnitState(
                runtimeId: CreateRuntimeId(playerId, definition.CardId),
                cardId: definition.CardId,
                ownerId: playerId,
                position: targetCoord,
                attackType: definition.AttackType,
                attack: definition.Attack,
                maxHp: definition.Health,
                canMove: definition.CanMove,
                isScience: definition.IsScience,
                sciencePowerUpkeep: definition.SciencePowerUpkeep,
                turnStartResourceGain: definition.TurnStartResourceGain.Clone(),
                maxAttacksPerTurn: definition.MaxAttacksPerTurn,
                hitsPerAttack: definition.HitsPerAttack,
                hasBerserker: definition.HasBerserker,
                hasEndure: definition.HasEndure,
                hasGuard: definition.HasGuard);

            playerState.Resources.Spend(definition.Cost);
            playerState.Hand.Remove(definition.CardId);

            _summonService.Summon(battleState, playerId, unit, targetCoord);

            if (definition.CanAttackOnSummon)
            {
                unit.HasSummoningSickness = false;
            }

            return unit;
        }

        private BuildingState PlayBuildingCardInternal(BattleState battleState, PlayerId playerId, BuildingCardDefinition definition, TileCoord targetCoord)
        {
            var playerState = battleState.GetPlayer(playerId);
            EnsureCardCanBeAfforded(playerState, definition);
            EnsureSummonTargetIsValid(battleState, playerId, targetCoord);

            var building = new BuildingState(
                runtimeId: CreateRuntimeId(playerId, definition.CardId),
                cardId: definition.CardId,
                ownerId: playerId,
                position: targetCoord,
                canAttack: definition.CanAttack,
                attack: definition.Attack,
                maxHp: definition.Health,
                turnStartResourceGain: definition.TurnStartResourceGain.Clone());

            playerState.Resources.Spend(definition.Cost);
            playerState.Hand.Remove(definition.CardId);

            _summonService.Summon(battleState, playerId, building, targetCoord);

            if (definition.CanAttackOnSummon)
            {
                building.HasSummoningSickness = false;
            }

            return building;
        }

        private static void ValidateCommonPlayRequirements(BattleState battleState, PlayerId playerId, string cardId, TileCoord targetCoord)
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
                throw new InvalidOperationException("Cards can only be played during the main phase.");
            }

            if (battleState.ActivePlayerId != playerId)
            {
                throw new InvalidOperationException("Only the active player can play cards.");
            }

            var playerState = battleState.GetPlayer(playerId);
            if (!playerState.Hand.Contains(cardId))
            {
                throw new InvalidOperationException("The specified card is not in the player's hand.");
            }

            var board = battleState.GetBoard(playerId);
            if (!board.IsInside(targetCoord))
            {
                throw new InvalidOperationException("Cannot play a card outside the board.");
            }
        }

        private static void EnsureCardCanBeAfforded(PlayerState playerState, CardDefinition definition)
        {
            if (!playerState.Resources.CanAfford(definition.Cost))
            {
                throw new InvalidOperationException("The player cannot afford this card.");
            }
        }

        private static void EnsureSummonTargetIsValid(BattleState battleState, PlayerId playerId, TileCoord targetCoord)
        {
            var board = battleState.GetBoard(playerId);
            if (!board.IsEmpty(targetCoord))
            {
                throw new InvalidOperationException("Cannot summon onto an occupied tile.");
            }
        }

        private static string CreateRuntimeId(PlayerId playerId, string cardId)
        {
            return $"{playerId}-{cardId}-{Guid.NewGuid():N}";
        }
    }
}
