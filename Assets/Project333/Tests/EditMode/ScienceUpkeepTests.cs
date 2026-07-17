using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class ScienceUpkeepTests
    {
        [Test]
        public void ResolveTurnStart_AppliesTurnStartResourceGainBeforeScienceUpkeep()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            var manaUnit = new UnitState(
                runtimeId: "mana-unit",
                cardId: "mana-unit",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 1,
                maxHp: 3,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSet(mana: 2, qi: 0, power: 0, gold: 0));

            var goldBuilding = new BuildingState(
                runtimeId: "gold-building",
                cardId: "gold-building",
                ownerId: PlayerId.Player,
                position: new TileCoord(4, 0),
                canAttack: false,
                attack: 0,
                maxHp: 5,
                turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 1));

            battleState.PlayerBoard.Place(new TileCoord(0, 0), manaUnit);
            battleState.PlayerBoard.Place(new TileCoord(4, 0), goldBuilding);
            battleState.Player.Resources.Add(new ResourceSet(mana: 2, qi: 1, power: 4, gold: 2));

            service.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Mana, Is.EqualTo(4));
            Assert.That(battleState.Player.Resources.Qi, Is.EqualTo(1));
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(4));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(7));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Main));
        }

        [Test]
        public void ResolveTurnStart_DrainedUnitDoesNotContributeResourceGainDuringGainStep()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            var drainedUnit = new UnitState(
                runtimeId: "drained-upkeep",
                cardId: "drained-upkeep",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 1,
                turnStartResourceGain: new ResourceSet(mana: 0, qi: 0, power: 2, gold: 0));

            drainedUnit.IsDrained = true;
            battleState.PlayerBoard.Place(new TileCoord(0, 0), drainedUnit);

            service.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(0));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(3));
            Assert.That(drainedUnit.IsDrained, Is.False);
        }

        [Test]
        public void ResolveTurnStart_PowerUpkeepUsesFixedTileOrderAndDrainsOnlyUnpaidUnits()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 5, gold: 0));

            var upkeepSeven = CreateScienceUnit("science-7", new TileCoord(0, 0), 7);
            var upkeepThree = CreateScienceUnit("science-3", new TileCoord(0, 1), 3);
            var upkeepFour = CreateScienceUnit("science-4", new TileCoord(2, 0), 4);
            var upkeepTwo = CreateScienceUnit("science-2", new TileCoord(4, 1), 2);

            battleState.PlayerBoard.Place(new TileCoord(0, 0), upkeepSeven);
            battleState.PlayerBoard.Place(new TileCoord(0, 1), upkeepThree);
            battleState.PlayerBoard.Place(new TileCoord(2, 0), upkeepFour);
            battleState.PlayerBoard.Place(new TileCoord(4, 1), upkeepTwo);

            service.ResolveTurnStart(battleState);

            Assert.That(upkeepSeven.IsDrained, Is.False);
            Assert.That(upkeepThree.IsDrained, Is.True);
            Assert.That(upkeepFour.IsDrained, Is.True);
            Assert.That(upkeepTwo.IsDrained, Is.False);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(0));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(0));
        }

        [Test]
        public void ResolveTurnStart_PowerUpkeepSpendsPowerFirstAndGoldForTheDeficit()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var upkeepUnit = CreateScienceUnit("gold-backed-upkeep", new TileCoord(0, 0), 3);

            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 1, gold: 0));
            battleState.PlayerBoard.Place(upkeepUnit.Position, upkeepUnit);

            service.ResolveTurnStart(battleState);

            Assert.That(upkeepUnit.IsDrained, Is.False);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(0));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(2));
        }

        [Test]
        public void ResolveTurnStart_NonScienceUnitWithPowerUpkeepCanBecomeDrained()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var upkeepUnit = new UnitState(
                runtimeId: "upkeep-unit",
                cardId: "upkeep-unit",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 99);

            battleState.PlayerBoard.Place(new TileCoord(0, 0), upkeepUnit);

            service.ResolveTurnStart(battleState);

            Assert.That(upkeepUnit.IsDrained, Is.True);
        }

        [Test]
        public void ResolveTurnStart_ErasureOccupantDoesNotPayUpkeepOrBecomeDrained()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var upkeepUnit = CreateScienceUnit("erasure-upkeep", new TileCoord(0, 0), 3);
            upkeepUnit.IsDrained = true;
            upkeepUnit.ApplyErasure();
            battleState.Player.Resources.Add(new ResourceSet(mana: 0, qi: 0, power: 3, gold: 0));
            battleState.PlayerBoard.Place(upkeepUnit.Position, upkeepUnit);

            service.ResolveTurnStart(battleState);

            Assert.That(upkeepUnit.IsErasure, Is.True);
            Assert.That(upkeepUnit.IsDrained, Is.False);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(3));
        }

        private static UnitState CreateScienceUnit(string id, TileCoord coord, int upkeep)
        {
            return new UnitState(
                runtimeId: id,
                cardId: id,
                ownerId: PlayerId.Player,
                position: coord,
                attackType: AttackType.Melee,
                attack: 3,
                maxHp: 5,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: upkeep);
        }

        private static BattleState CreateBattleState(PlayerId firstPlayerId)
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(CreateRequest(firstPlayerId));

            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.RestoreRuntimeState(
                turnNumber: 3,
                activePlayerId: firstPlayerId,
                phase: PhaseType.TurnStart);
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
