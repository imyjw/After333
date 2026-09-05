using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

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
        public void EndTurn_ErasureBlueDragon_DoesNotHealAllies()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            var blueDragon = CreateUnit("blue-dragon", "BlueDragon", PlayerId.Player, new TileCoord(0, 0), AttackType.Ranged, 40, 100);
            var ally = CreateUnit("ally", "ally", PlayerId.Player, new TileCoord(1, 0), AttackType.Melee, 10, 20);

            blueDragon.ApplyErasure();
            blueDragon.CurrentHp = 50;
            ally.CurrentHp = 2;
            battleState.Player.Master.CurrentHp = 300;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), blueDragon);
            battleState.PlayerBoard.Place(new TileCoord(1, 0), ally);

            service.EndTurn(battleState);

            Assert.That(blueDragon.CurrentHp, Is.EqualTo(50));
            Assert.That(ally.CurrentHp, Is.EqualTo(2));
            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(300));
        }

        [Test]
        public void EndTurn_RedDragon_DamagesAllEnemyOccupantsAndCanEndBattle()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            var redDragon = CreateUnit("red-dragon", "RedDragon", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 50, 50);
            var frontEnemy = CreateUnit("front-enemy", "front-enemy", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 10, 100);
            var backEnemy = CreateUnit("back-enemy", "back-enemy", PlayerId.AI, new TileCoord(0, 1), AttackType.Ranged, 10, 100);

            battleState.PlayerBoard.Place(new TileCoord(0, 0), redDragon);
            battleState.AIBoard.Place(new TileCoord(0, 0), frontEnemy);
            battleState.AIBoard.Place(new TileCoord(0, 1), backEnemy);
            battleState.AI.Master.CurrentHp = 30;

            service.EndTurn(battleState);

            Assert.That(frontEnemy.CurrentHp, Is.EqualTo(67));
            Assert.That(backEnemy.CurrentHp, Is.EqualTo(67));
            Assert.That(battleState.AI.Master.CurrentHp, Is.LessThanOrEqualTo(0));
            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.Player));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Ended));
        }

        [Test]
        public void EndTurn_DrainedRedDragon_DoesNotDamageEnemies()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            var redDragon = CreateUnit("red-dragon", "RedDragon", PlayerId.Player, new TileCoord(0, 0), AttackType.Melee, 50, 50);
            var enemy = CreateUnit("enemy", "enemy", PlayerId.AI, new TileCoord(0, 0), AttackType.Melee, 10, 100);

            redDragon.IsDrained = true;
            battleState.PlayerBoard.Place(new TileCoord(0, 0), redDragon);
            battleState.AIBoard.Place(new TileCoord(0, 0), enemy);
            battleState.AI.Master.CurrentHp = 60;

            service.EndTurn(battleState);

            Assert.That(enemy.CurrentHp, Is.EqualTo(100));
            Assert.That(battleState.AI.Master.CurrentHp, Is.EqualTo(60));
            Assert.That(battleState.IsEnded, Is.False);
        }

        [Test]
        public void EndTurn_GaebangBranchSummonedThisTurn_DrawsTwoWhenGoldIsZero()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            SpendAllGold(battleState.Player);
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateGaebangBranch("gaebang-branch", new TileCoord(0, 0)));
            var handCountBefore = battleState.Player.Hand.Count;
            var deckCountBefore = battleState.Player.Deck.Count;

            service.EndTurn(battleState);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(handCountBefore + 2));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(deckCountBefore - 2));
        }

        [Test]
        public void EndTurn_GaebangBranch_DoesNotDrawWhenGoldRemains()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateGaebangBranch("gaebang-branch", new TileCoord(0, 0)));
            var handCountBefore = battleState.Player.Hand.Count;
            var deckCountBefore = battleState.Player.Deck.Count;

            Assert.That(battleState.Player.Resources.Gold, Is.GreaterThan(0));

            service.EndTurn(battleState);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(handCountBefore));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(deckCountBefore));
        }

        [Test]
        public void EndTurn_MultipleGaebangBranches_EachDrawTwoCards()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            SpendAllGold(battleState.Player);
            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateGaebangBranch("gaebang-branch-a", new TileCoord(0, 0)));
            battleState.PlayerBoard.Place(
                new TileCoord(0, 1),
                CreateGaebangBranch("gaebang-branch-b", new TileCoord(0, 1)));
            var handCountBefore = battleState.Player.Hand.Count;
            var deckCountBefore = battleState.Player.Deck.Count;

            service.EndTurn(battleState);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(handCountBefore + 4));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(deckCountBefore - 4));
            Assert.That(
                battleState.CardDrawEvents.Count(drawEvent =>
                    drawEvent.SourceCardId == GaebangBranchRules.CardId),
                Is.EqualTo(4));
        }

        [Test]
        public void EndTurn_GaebangBranch_FullHandRemovesExcessDrawsFromDeck()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            SpendAllGold(battleState.Player);
            while (battleState.Player.Hand.Count < battleState.Player.MaxHandSize)
            {
                battleState.Player.Hand.Add($"full-hand-{battleState.Player.Hand.Count}");
            }

            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateGaebangBranch("gaebang-branch", new TileCoord(0, 0)));
            var deckCountBefore = battleState.Player.Deck.Count;

            service.EndTurn(battleState);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(battleState.Player.MaxHandSize));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(deckCountBefore - 2));
        }

        [Test]
        public void EndTurn_GaebangBranch_EmptyDeckAppliesEachCumulativeFixedDrawFailure()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            SpendAllGold(battleState.Player);
            while (battleState.Player.Deck.TryDraw(out _))
            {
            }

            battleState.PlayerBoard.Place(
                new TileCoord(0, 0),
                CreateGaebangBranch("gaebang-branch", new TileCoord(0, 0)));
            var masterHpBefore = battleState.Player.Master.CurrentHp;

            service.EndTurn(battleState);

            Assert.That(battleState.Player.FailedDrawCount, Is.EqualTo(2));
            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(masterHpBefore - 9));
        }

        [Test]
        public void EndTurn_GaebangBranch_SuppressedStatesDoNotDraw()
        {
            AssertSuppressedGaebangBranchDoesNotDraw(building => building.IsDrained = true, "Drained");
            AssertSuppressedGaebangBranchDoesNotDraw(building => building.ApplyErasure(), "Erasure");
            AssertSuppressedGaebangBranchDoesNotDraw(building => building.EnterSealbound(1), "Sealbound");
        }

        private static void AssertSuppressedGaebangBranchDoesNotDraw(
            System.Action<BuildingState> suppress,
            string stateName)
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();
            SpendAllGold(battleState.Player);
            var building = CreateGaebangBranch($"gaebang-{stateName}", new TileCoord(0, 0));
            suppress(building);
            battleState.PlayerBoard.Place(new TileCoord(0, 0), building);
            var handCountBefore = battleState.Player.Hand.Count;
            var deckCountBefore = battleState.Player.Deck.Count;

            service.EndTurn(battleState);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(handCountBefore), stateName);
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(deckCountBefore), stateName);
        }

        private static BuildingState CreateGaebangBranch(string runtimeId, TileCoord coord)
        {
            return new BuildingState(
                runtimeId,
                GaebangBranchRules.CardId,
                PlayerId.Player,
                coord,
                canAttack: false,
                attack: 0,
                maxHp: GaebangBranchRules.BaseHealth,
                damageType: DamageType.None);
        }

        private static void SpendAllGold(PlayerState playerState)
        {
            if (playerState.Resources.Gold > 0)
            {
                playerState.Resources.Spend(new ResourceSet(0, 0, 0, playerState.Resources.Gold));
            }
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
