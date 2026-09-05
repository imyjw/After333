using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class PowerPlantTests
    {
        [Test]
        public void ResolveTurnStart_ConvertsAfterResourceGainAndBeforePowerUpkeep()
        {
            var battleState = CreateTurnStartBattle();
            ClearStartingGold(battleState.Player);
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreatePowerPlant("plant", new TileCoord(0, 0)));
            battleState.PlayerBoard.Place(
                new TileCoord(0, 1),
                new BuildingState(
                    "gold-source",
                    "GoldSource",
                    PlayerId.Player,
                    new TileCoord(0, 1),
                    canAttack: false,
                    attack: 0,
                    maxHp: 1,
                    turnStartResourceGain: new ResourceSet(0, 0, 0, 1),
                    damageType: DamageType.None));
            var upkeepUnit = new UnitState(
                "upkeep-unit",
                "ScienceUpkeepTwo",
                PlayerId.Player,
                new TileCoord(1, 0),
                AttackType.Melee,
                attack: 1,
                maxHp: 1,
                canMove: true,
                isScience: true,
                sciencePowerUpkeep: 2);
            battleState.PlayerBoard.Place(upkeepUnit.Position, upkeepUnit);

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(upkeepUnit.IsDrained, Is.False);
            Assert.That(battleState.Player.Resources.Gold, Is.Zero);
            Assert.That(battleState.Player.Resources.Power, Is.Zero);
        }

        [Test]
        public void ResolveTurnStart_MultiplePowerPlantsConvertIndependently()
        {
            var battleState = CreateTurnStartBattle();
            ClearStartingGold(battleState.Player);
            battleState.Player.Resources.Add(new ResourceSet(0, 0, 0, 2));
            battleState.PlayerBoard.Place(
                new TileCoord(4, 1),
                CreatePowerPlant("later", new TileCoord(4, 1)));
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreatePowerPlant("earlier", new TileCoord(0, 0)));

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Gold, Is.Zero);
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(4));
            Assert.That(battleState.ResourceChangeEvents, Has.Count.EqualTo(2));
            Assert.That(battleState.ResourceChangeEvents[0].SourceCardId, Is.EqualTo(PowerPlantRules.CardId));
            Assert.That(battleState.ResourceChangeEvents[0].Spent.Gold, Is.EqualTo(1));
            Assert.That(battleState.ResourceChangeEvents[0].Gained.Power, Is.EqualTo(2));
            Assert.That(battleState.ResourceChangeEvents[1].SourceCardId, Is.EqualTo(PowerPlantRules.CardId));
            Assert.That(battleState.ResourceChangeEvents[1].Spent.Gold, Is.EqualTo(1));
            Assert.That(battleState.ResourceChangeEvents[1].Gained.Power, Is.EqualTo(2));
        }

        [Test]
        public void ResolveTurnStart_WithoutGold_PowerPlantDoesNothing()
        {
            var battleState = CreateTurnStartBattle();
            ClearStartingGold(battleState.Player);
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreatePowerPlant("plant", new TileCoord(0, 0)));

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Gold, Is.Zero);
            Assert.That(battleState.Player.Resources.Power, Is.Zero);
        }

        [Test]
        public void ResolveTurnStart_SuppressedPowerPlantsDoNotConvert()
        {
            var battleState = CreateTurnStartBattle();
            ClearStartingGold(battleState.Player);
            battleState.Player.Resources.Add(new ResourceSet(0, 0, 0, 4));

            var active = CreatePowerPlant("active", new TileCoord(0, 0));
            var drained = CreatePowerPlant("drained", new TileCoord(0, 1));
            drained.IsDrained = true;
            var erased = CreatePowerPlant("erased", new TileCoord(1, 0));
            erased.ApplyErasure();
            var sealbound = CreatePowerPlant("sealbound", new TileCoord(1, 1));
            sealbound.EnterSealbound(1);

            battleState.PlayerBoard.Place(active.Position, active);
            battleState.PlayerBoard.Place(drained.Position, drained);
            battleState.PlayerBoard.Place(erased.Position, erased);
            battleState.PlayerBoard.Place(sealbound.Position, sealbound);

            new TurnStartService().ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(3));
            Assert.That(battleState.Player.Resources.Power, Is.EqualTo(2));
        }

        private static BuildingState CreatePowerPlant(string runtimeId, TileCoord position)
        {
            return new BuildingState(
                runtimeId,
                PowerPlantRules.CardId,
                PlayerId.Player,
                position,
                canAttack: false,
                attack: 0,
                maxHp: PowerPlantRules.BaseHealth,
                damageType: DamageType.None);
        }

        private static BattleState CreateTurnStartBattle()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(new BattleSetupRequest(
                CreateDeck("P"),
                CreateDeck("A"),
                PlayerId.Player));
            mulliganService.PassMulligan(battleState, PlayerId.Player);
            battleState.RestoreRuntimeState(1, PlayerId.Player, PhaseType.TurnStart);
            return battleState;
        }

        private static void ClearStartingGold(PlayerState player)
        {
            if (player.Resources.Gold > 0)
            {
                player.Resources.Spend(new ResourceSet(0, 0, 0, player.Resources.Gold));
            }
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            var cards = new List<string>();
            for (var i = 0; i < 20; i++)
            {
                cards.Add($"{prefix}-{i}");
            }

            return cards;
        }
    }
}
