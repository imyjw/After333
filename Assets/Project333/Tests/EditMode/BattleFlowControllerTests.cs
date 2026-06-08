using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class BattleFlowControllerTests
    {
        [Test]
        public void BattleFlowController_CanAdvanceFromBattleStartToNextPlayersTurnStart()
        {
            var controller = new BattleFlowController();

            controller.StartBattle(CreateRequest(PlayerId.Player));
            controller.PassMulligan(PlayerId.Player);
            controller.ResolveTurnStart();
            controller.EndTurn();

            Assert.That(controller.CurrentBattleState, Is.Not.Null);
            Assert.That(controller.CurrentBattleState.Phase, Is.EqualTo(PhaseType.TurnStart));
            Assert.That(controller.CurrentBattleState.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(controller.CurrentBattleState.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void BattleFlowController_WhenBattleHasNotStarted_ThrowsOnTurnActions()
        {
            var controller = new BattleFlowController();

            var exception = Assert.Throws<System.InvalidOperationException>(() => controller.ResolveTurnStart());

            Assert.That(
                exception!.Message,
                Is.EqualTo("Battle flow cannot continue before a battle has been started."));
        }

        [Test]
        public void BattleFlowController_ExecuteCommand_UsesBattleCommandProcessorForMainPhaseActions()
        {
            var controller = CreateController(new CardDefinition[]
            {
                new UnitCardDefinition(
                    cardId: "unit-card",
                    displayName: "Test Unit",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 2),
                    attackType: AttackType.Melee,
                    attack: 3,
                    health: 6,
                    canMove: true,
                    isScience: false,
                    sciencePowerUpkeep: 0),
            });

            controller.StartBattle(CreateRequest(PlayerId.Player));
            controller.PassMulligan(PlayerId.Player);
            controller.ResolveTurnStart();
            controller.CurrentBattleState.Player.Hand.Add("unit-card");
            controller.CurrentBattleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 0, gold: 5));

            controller.ExecuteCommand(
                PlayerId.Player,
                new PlayUnitCardCommand("unit-card", new TileCoord(0, 0)));

            Assert.That(controller.CurrentBattleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.Not.Null);
            Assert.That(controller.CurrentBattleState.Player.Hand.Contains("unit-card"), Is.False);
            Assert.That(controller.CurrentBattleState.Player.Resources.Gold, Is.EqualTo(7));
        }

        private static BattleSetupRequest CreateRequest(PlayerId firstPlayerId)
        {
            return new BattleSetupRequest(
                playerDeckCardIds: CreateDeck("P"),
                aiDeckCardIds: CreateDeck("A"),
                firstPlayerId: firstPlayerId);
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new List<string>
            {
                prefix + "-00",
                prefix + "-01",
                prefix + "-02",
                prefix + "-03",
                prefix + "-04",
                prefix + "-05",
                prefix + "-06",
                prefix + "-07",
                prefix + "-08",
                prefix + "-09",
            };
        }

        private static BattleFlowController CreateController(IEnumerable<CardDefinition> definitions)
        {
            var provider = new InMemoryCardDefinitionProvider(definitions);
            var processor = new BattleCommandProcessor(
                new PlayCardService(provider),
                new SpellService(provider),
                new MoveService(),
                new AttackService(),
                new EndTurnService());

            return new BattleFlowController(
                new BattleSetupService(),
                new MulliganService(),
                new TurnStartService(),
                processor);
        }
    }
}
