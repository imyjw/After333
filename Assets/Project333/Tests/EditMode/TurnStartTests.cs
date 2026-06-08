using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class TurnStartTests
    {
        [Test]
        public void ResolveTurnStart_DrawsOneCardGainsOneGoldAndMovesToMain()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var startingGold = battleState.Player.Resources.Gold;
            var startingHandCount = battleState.Player.Hand.Count;
            var startingDeckCount = battleState.Player.Deck.Count;

            service.ResolveTurnStart(battleState);

            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Main));
            Assert.That(battleState.TurnNumber, Is.EqualTo(1));
            Assert.That(battleState.Player.Resources.Gold, Is.EqualTo(startingGold + 1));
            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(startingHandCount + 1));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(startingDeckCount - 1));
            Assert.That(battleState.Player.FailedDrawCount, Is.EqualTo(0));
        }

        [Test]
        public void ResolveTurnStart_WhenHandIsFull_RemovesDrawnCardAndKeepsHandAtNine()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            battleState.Player.Hand.Add("P-extra-0");
            battleState.Player.Hand.Add("P-extra-1");
            battleState.Player.Hand.Add("P-extra-2");
            battleState.Player.Hand.Add("P-extra-3");
            battleState.Player.Hand.Add("P-extra-4");
            battleState.Player.Hand.Add("P-extra-5");

            var startingDeckCount = battleState.Player.Deck.Count;

            service.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(9));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(startingDeckCount - 1));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Main));
        }

        [Test]
        public void ResolveTurnStart_WhenDeckIsEmpty_DealsFailedDrawDamageToMaster()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            EmptyDeck(battleState.Player.Deck);

            service.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Master.CurrentHp, Is.EqualTo(330));
            Assert.That(battleState.Player.FailedDrawCount, Is.EqualTo(1));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Main));
            Assert.That(battleState.IsEnded, Is.False);
        }

        [Test]
        public void ResolveTurnStart_WhenFailedDrawIsLethal_EndsBattle()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();

            EmptyDeck(battleState.Player.Deck);
            battleState.Player.Master.CurrentHp = 3;

            service.ResolveTurnStart(battleState);

            Assert.That(battleState.IsEnded, Is.True);
            Assert.That(battleState.Result.HasWinner, Is.True);
            Assert.That(battleState.Result.Winner, Is.EqualTo(PlayerId.AI));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Ended));
        }

        [Test]
        public void ResolveTurnStart_ClearsSummoningSicknessForActivePlayerOccupants()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var unit = new UnitState(
                runtimeId: "fresh-unit",
                cardId: "fresh-unit",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);

            unit.HasSummoningSickness = true;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), unit);

            service.ResolveTurnStart(battleState);

            Assert.That(unit.HasSummoningSickness, Is.False);
        }

        [Test]
        public void ResolveTurnStart_RefreshesRemainingAttacksForActivePlayerOccupants()
        {
            var battleState = CreateBattleState(PlayerId.Player);
            var service = new TurnStartService();
            var unit = new UnitState(
                runtimeId: "multi-unit",
                cardId: "multi-unit",
                ownerId: PlayerId.Player,
                position: new TileCoord(0, 0),
                attackType: AttackType.Melee,
                attack: 2,
                maxHp: 5,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                maxAttacksPerTurn: 2);

            unit.RemainingAttacksThisTurn = 0;

            battleState.PlayerBoard.Place(new TileCoord(0, 0), unit);

            service.ResolveTurnStart(battleState);

            Assert.That(unit.RemainingAttacksThisTurn, Is.EqualTo(2));
        }

        private static BattleState CreateBattleState(PlayerId firstPlayerId)
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(CreateRequest(firstPlayerId));

            mulliganService.PassMulligan(battleState, PlayerId.Player);
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

        private static void EmptyDeck(DeckState deckState)
        {
            while (deckState.TryDraw(out _))
            {
            }
        }
    }
}
