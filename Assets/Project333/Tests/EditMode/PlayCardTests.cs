using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class PlayCardTests
    {
        [Test]
        public void PlayUnitCard_SpendsResourcesRemovesHandCardAndPlacesUnit()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("unit-card");
            battleState.Player.Resources.Add(new ResourceSet(gold: 5, mana: 0, qi: 0, power: 0));

            var service = CreatePlayCardService(new CardDefinition[]
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

            var unit = service.PlayUnitCard(battleState, PlayerId.Player, "unit-card", new TileCoord(0, 0));

            Assert.That(unit.CardId, Is.EqualTo("unit-card"));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(6));
            Assert.That(battleState.Player.Hand.Contains("unit-card"), Is.False);
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(0, 0)), Is.SameAs(unit));
            Assert.That(unit.HasSummoningSickness, Is.True);
            Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(1));
        }

        [Test]
        public void PlayUnitCard_RobotDefinition_ErasureSuppressesRobotClassification()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("robot-unit");
            var service = CreatePlayCardService(new CardDefinition[]
            {
                new UnitCardDefinition(
                    cardId: "robot-unit",
                    displayName: "Robot Unit",
                    cost: new ResourceSet(),
                    attackType: AttackType.Melee,
                    attack: 3,
                    health: 6,
                    canMove: true,
                    isScience: true,
                    sciencePowerUpkeep: 1,
                    hasRobot: true),
            });

            var unit = service.PlayUnitCard(battleState, PlayerId.Player, "robot-unit", new TileCoord(0, 0));

            Assert.That(unit.HasRobot, Is.True);

            unit.IsDrained = true;
            unit.ApplyErasure();

            Assert.That(unit.HasRobot, Is.True, "The serialized card tag remains available for restoration and UI.");
            Assert.That(unit.HasActiveRobot, Is.False, "Erasure suppresses the Robot classification in battle rules.");
            Assert.That(unit.IsDrained, Is.False, "Applying Erasure immediately clears Drained.");
        }

        [Test]
        public void PlayBuildingCard_SpendsResourcesRemovesHandCardAndPlacesBuilding()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("building-card");
            battleState.Player.Resources.Add(new ResourceSet(mana: 1, qi: 0, power: 0, gold: 5));

            var service = CreatePlayCardService(new CardDefinition[]
            {
                new BuildingCardDefinition(
                    cardId: "building-card",
                    displayName: "Test Tower",
                    cost: new ResourceSet(mana: 1, qi: 0, power: 0, gold: 3),
                    canAttack: true,
                    attack: 2,
                    health: 7),
            });

            var building = service.PlayBuildingCard(battleState, PlayerId.Player, "building-card", new TileCoord(1, 0));

            Assert.That(building.CardId, Is.EqualTo("building-card"));
            Assert.That(building.CanAttackAsBuilding, Is.True);
            Assert.That(battleState.Player.Resources.Mana, Is.EqualTo(0));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(5));
            Assert.That(battleState.Player.Hand.Contains("building-card"), Is.False);
            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(1, 0)), Is.SameAs(building));
            Assert.That(building.HasSummoningSickness, Is.True);
        }

        [Test]
        public void PlayUnitCard_RushUnit_CanAttackOnItsSummonTurn()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("rush-unit");
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 1, power: 0, gold: 4));

            var service = CreatePlayCardService(new CardDefinition[]
            {
                new UnitCardDefinition(
                    cardId: "rush-unit",
                    displayName: "Rush Unit",
                    cost: new ResourceSet(mana: 0, qi: 1, power: 0, gold: 2),
                    attackType: AttackType.Melee,
                    attack: 3,
                    health: 4,
                    canMove: true,
                    isScience: false,
                    sciencePowerUpkeep: 0,
                    maxAttacksPerTurn: 2,
                    hasRush: true),
            });

            var unit = service.PlayUnitCard(battleState, PlayerId.Player, "rush-unit", new TileCoord(0, 0));
            var enemyMasterHpBefore = battleState.AI.Master.CurrentHp;

            Assert.That(unit.HasSummoningSickness, Is.False);
            Assert.That(unit.WasSummonedThisTurn, Is.True);
            Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(2));

            new AttackService().Attack(
                battleState,
                PlayerId.Player,
                new TileCoord(0, 0),
                new TileCoord(2, 1));

            Assert.That(battleState.AI.Master.CurrentHp, Is.LessThan(enemyMasterHpBefore));
            Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(1));

            unit.ApplyErasure();

            Assert.That(unit.HasSummoningSickness, Is.True, "Erasure suppresses Rush on the summon turn.");
            Assert.That(unit.RemainingAttacksThisTurn, Is.Zero, "Erasure suppresses the additional attack effect.");
        }

        [Test]
        public void PlayUnitCard_AppliesOwnerCardUpgradeLevelStatBonus()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("leveled-unit");
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 0, gold: 5));
            var levelProvider = new InMemoryCardUpgradeLevelProvider();
            levelProvider.SetUpgradeLevel(PlayerId.Player, "leveled-unit", 3);

            var service = CreatePlayCardService(
                new CardDefinition[]
                {
                    new UnitCardDefinition(
                        cardId: "leveled-unit",
                        displayName: "Leveled Unit",
                        cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1),
                        attackType: AttackType.Melee,
                        attack: 10,
                        health: 20,
                        canMove: true,
                        isScience: false,
                        sciencePowerUpkeep: 0),
                },
                levelProvider);

            var unit = service.PlayUnitCard(battleState, PlayerId.Player, "leveled-unit", new TileCoord(0, 0));

            Assert.That(unit.BaseAttack, Is.EqualTo(11));
            Assert.That(unit.Attack, Is.EqualTo(11));
            Assert.That(unit.MaxHp, Is.EqualTo(22));
            Assert.That(unit.CurrentHp, Is.EqualTo(22));
        }

        [Test]
        public void PlayUnitCard_WhenResourcesAreInsufficient_ThrowsAndKeepsCardInHand()
        {
            var battleState = CreateBattleStateInMainPhase();
            battleState.Player.Hand.Add("expensive-unit");

            var service = CreatePlayCardService(new CardDefinition[]
            {
                new UnitCardDefinition(
                    cardId: "expensive-unit",
                    displayName: "Expensive Unit",
                    cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 99),
                    attackType: AttackType.Melee,
                    attack: 5,
                    health: 5,
                    canMove: true,
                    isScience: false,
                    sciencePowerUpkeep: 0),
            });

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => service.PlayUnitCard(battleState, PlayerId.Player, "expensive-unit", new TileCoord(0, 0)));

            Assert.That(exception!.Message, Is.EqualTo("The player cannot afford this card."));
            Assert.That(battleState.Player.Hand.Contains("expensive-unit"), Is.True);
            Assert.That(battleState.PlayerBoard.IsEmpty(new TileCoord(0, 0)), Is.True);
        }

        private static PlayCardService CreatePlayCardService(IEnumerable<CardDefinition> definitions)
        {
            return new PlayCardService(new InMemoryCardDefinitionProvider(definitions));
        }

        private static PlayCardService CreatePlayCardService(
            IEnumerable<CardDefinition> definitions,
            ICardUpgradeLevelProvider cardUpgradeLevelProvider)
        {
            return new PlayCardService(
                new InMemoryCardDefinitionProvider(definitions),
                new SummonService(),
                cardUpgradeLevelProvider);
        }

        private static BattleState CreateBattleStateInMainPhase()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var turnStartService = new TurnStartService();
            var battleState = setupService.CreateInitialState(CreateRequest(PlayerId.Player));

            mulliganService.PassMulligan(battleState, PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

            return battleState;
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
    }
}
