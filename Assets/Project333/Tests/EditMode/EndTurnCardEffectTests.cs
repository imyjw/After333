using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class EndTurnCardEffectTests
    {
        [Test]
        public void EndTurn_BlueDragon_HealsAlliedUnitsAndMasterOnly()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            var blueDragon = CreateUnit("blue-dragon", "BlueDragon", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 40, 100);
            var ally = CreateUnit("ally", "ally", PlayerId.Player, new TileCoord(1, 0), AttackType.Melee, 10, 20);
            var building = new BuildingState("building", "building", PlayerId.Player, new TileCoord(3, 0), canAttack: false, attack: 0, maxHp: 10);

            blueDragon.CurrentHp = 50;
            ally.CurrentHp = 2;
            building.CurrentHp = 4;
            battleState.Player.Master.CurrentHp = 300;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), blueDragon);
            battleState.PlayerBoard.Place(new TileCoord(1, 0), ally);
            battleState.PlayerBoard.Place(new TileCoord(3, 0), building);

            service.EndTurn(battleState);

            Assert.That(blueDragon.CurrentHp, Is.EqualTo(83));
            Assert.That(ally.CurrentHp, Is.EqualTo(20));
            Assert.That(building.CurrentHp, Is.EqualTo(4));
            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(battleState.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.TurnStart));
        }

        [Test]
        public void EndTurn_RedDragon_DamagesAllEnemyOccupantsAndCanEndBattle()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            var redDragon = CreateUnit("red-dragon", "RedDragon", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 66, 66);
            var frontEnemy = CreateUnit("front-enemy", "front-enemy", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 10, 100);
            var backEnemy = CreateUnit("back-enemy", "back-enemy", PlayerId.AI, new TileCoord(0, 1), AttackType.Ranged, 10, 100);

            battleState.PlayerBoard.Place(new TileCoord(0, 0), redDragon);
            battleState.AIBoard.Place(new TileCoord(0, 0), frontEnemy);
            battleState.AIBoard.Place(new TileCoord(0, 1), backEnemy);
            battleState.AI.Master.CurrentHp = 60;

            service.EndTurn(battleState);

            Assert.That(frontEnemy.CurrentHp, Is.EqualTo(34));
            Assert.That(backEnemy.CurrentHp, Is.EqualTo(34));
            Assert.That(battleState.AI.Master.CurrentHp, Is.LessThanOrEqualTo(0));
            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.Player));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Ended));
        }

        private static UnitState CreateUnit(
            string runtimeId,
            string cardId,
            PlayerId ownerId,
            TileCoord coord,
            AttackType attackType,
            int attack,
            int maxHp)
        {
            return new UnitState(
                runtimeId: runtimeId,
                cardId: cardId,
                ownerId: ownerId,
                position: coord,
                attackType: attackType,
                attack: attack,
                maxHp: maxHp,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);
        }

        private static BattleState CreateBattleStateInMainPhase(PlayerId firstPlayerId)
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var turnStartService = new TurnStartService();
            var battleState = setupService.CreateInitialState(CreateRequest(firstPlayerId));

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
