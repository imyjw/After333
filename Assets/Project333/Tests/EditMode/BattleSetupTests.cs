using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Tests.EditMode
{
    public sealed class BattleSetupTests
    {
        [Test]
        public void CreateInitialState_PlayerFirst_ConfiguresMastersResourcesAndOpeningHands()
        {
            var service = new BattleSetupService();
            var request = CreateRequest(PlayerId.Player);

            var battleState = service.CreateInitialState(request);

            Assert.That(battleState.ActivePlayerId, Is.EqualTo(PlayerId.Player));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Mulligan));
            Assert.That(battleState.TurnNumber, Is.EqualTo(0));
            Assert.That(battleState.IsEnded, Is.False);

            AssertMasterState(battleState.Player.Master, PlayerId.Player);
            AssertMasterState(battleState.AI.Master, PlayerId.AI);

            Assert.That(battleState.PlayerBoard.GetOccupant(new TileCoord(2, 1)), Is.SameAs(battleState.Player.Master));
            Assert.That(battleState.AIBoard.GetOccupant(new TileCoord(2, 1)), Is.SameAs(battleState.AI.Master));

            AssertResources(battleState.Player.Resources, 3);
            AssertResources(battleState.AI.Resources, 4);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(5));
            Assert.That(battleState.AI.Hand.Count, Is.EqualTo(5));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(5));
            Assert.That(battleState.AI.Deck.Count, Is.EqualTo(5));
            Assert.That(battleState.Player.HasUsedMulligan, Is.False);
            Assert.That(battleState.AI.HasUsedMulligan, Is.True);
            Assert.That(battleState.CardDrawEvents, Is.Empty);
        }

        [Test]
        public void CreateInitialState_AIFirst_GivesFirstPlayerThreeGoldAndSecondPlayerFourGold()
        {
            var service = new BattleSetupService();
            var request = CreateRequest(PlayerId.AI);

            var battleState = service.CreateInitialState(request);

            Assert.That(battleState.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            AssertResources(battleState.AI.Resources, 3);
            AssertResources(battleState.Player.Resources, 4);
            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(5));
            Assert.That(battleState.AI.Hand.Count, Is.EqualTo(5));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(5));
            Assert.That(battleState.AI.Deck.Count, Is.EqualTo(5));
        }

        [Test]
        public void CreateInitialState_WhenAIMulliganIsEnabled_WaitsForBothPlayers()
        {
            var service = new BattleSetupService(new TrackingReverseDeckShuffler());
            var request = new BattleSetupRequest(
                CreateDeck("P"),
                CreateDeck("A"),
                PlayerId.Player,
                aiMulliganEnabled: true);

            var battleState = service.CreateInitialState(request);

            Assert.That(battleState.Player.HasUsedMulligan, Is.False);
            Assert.That(battleState.AI.HasUsedMulligan, Is.False);
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.Mulligan));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void HumanMulligan_PreservesGoldAssignedBeforeEitherConfirmation(PlayerId first)
        {
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                CreateDeck("P"), CreateDeck("A"), first, aiMulliganEnabled: true));
            var second = state.GetOpponent(first).Id;
            var mulligan = new MulliganService();
            AssertResources(state.GetPlayer(first).Resources, 3);
            AssertResources(state.GetPlayer(second).Resources, 4);
            mulligan.PassMulligan(state, second);
            Assert.That(state.Phase, Is.EqualTo(PhaseType.Mulligan));
            AssertResources(state.GetPlayer(first).Resources, 3);
            AssertResources(state.GetPlayer(second).Resources, 4);
            mulligan.ApplyMulligan(state, first,
                new[] { state.GetPlayer(first).Hand.CardIds[0] }, new NoOpDeckShuffler());
            Assert.That(state.Phase, Is.EqualTo(PhaseType.TurnStart));
            AssertResources(state.GetPlayer(first).Resources, 3);
            AssertResources(state.GetPlayer(second).Resources, 4);
        }

        [Test]
        public void CreateInitialState_ShufflesBothDecksBeforeDrawingOpeningHands()
        {
            var shuffler = new TrackingReverseDeckShuffler();
            var service = new BattleSetupService(shuffler);

            var battleState = service.CreateInitialState(CreateRequest(PlayerId.Player));

            Assert.That(shuffler.ShuffleCount, Is.EqualTo(2));
            Assert.That(
                battleState.Player.Hand.CardIds,
                Is.EqualTo(new[] { "P-00", "P-01", "P-02", "P-03", "P-04" }));
            Assert.That(
                battleState.AI.Hand.CardIds,
                Is.EqualTo(new[] { "A-00", "A-01", "A-02", "A-03", "A-04" }));
        }

        [Test]
        public void SystemDeckShuffler_WithSameSeed_ProducesSameOrder()
        {
            var firstDeck = new DeckState(CreateDeck("P"));
            var secondDeck = new DeckState(CreateDeck("P"));

            new SystemDeckShuffler(new Random(333)).Shuffle(firstDeck);
            new SystemDeckShuffler(new Random(333)).Shuffle(secondDeck);

            Assert.That(firstDeck.CardIds, Is.EqualTo(secondDeck.CardIds));
        }

        [Test]
        public void SystemDeckShuffler_WithDifferentSeeds_ProducesDifferentOrder()
        {
            var firstDeck = new DeckState(CreateDeck("P"));
            var secondDeck = new DeckState(CreateDeck("P"));

            new SystemDeckShuffler(new Random(333)).Shuffle(firstDeck);
            new SystemDeckShuffler(new Random(334)).Shuffle(secondDeck);

            Assert.That(firstDeck.CardIds, Is.Not.EqualTo(secondDeck.CardIds));
        }

        [Test]
        public void PassMulligan_PlayerPasses_TransitionsToFirstTurnStart()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(CreateRequest(PlayerId.Player));

            mulliganService.PassMulligan(battleState, PlayerId.Player);

            Assert.That(battleState.Player.HasUsedMulligan, Is.True);
            Assert.That(battleState.AI.HasUsedMulligan, Is.True);
            Assert.That(battleState.ActivePlayerId, Is.EqualTo(PlayerId.Player));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.TurnStart));
            Assert.That(battleState.TurnNumber, Is.EqualTo(1));
        }

        [Test]
        public void ApplyMulligan_ReplacesSelectedCardsAndTransitionsToFirstTurn()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(CreateRequest(PlayerId.Player));

            var selectedCardId = battleState.Player.Hand.CardIds[0];

            mulliganService.ApplyMulligan(
                battleState,
                PlayerId.Player,
                new[] { selectedCardId },
                new ReverseDeckShuffler());

            Assert.That(battleState.Player.HasUsedMulligan, Is.True);
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.TurnStart));
            Assert.That(battleState.TurnNumber, Is.EqualTo(1));
            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(5));
            Assert.That(battleState.Player.Hand.Contains(selectedCardId), Is.False);
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(5));
            Assert.That(battleState.CardDrawEvents, Has.Count.EqualTo(1));
            Assert.That(battleState.CardDrawEvents[0].OwnerId, Is.EqualTo(PlayerId.Player));
        }

        [Test]
        public void ApplyMulligan_DrawsReplacementBeforeReturningSelectedCopyToDeck()
        {
            var setupService = new BattleSetupService(new TrackingReverseDeckShuffler());
            var battleState = setupService.CreateInitialState(CreateRequest(PlayerId.Player));
            var selectedCardId = battleState.Player.Hand.CardIds[0];

            new MulliganService().ApplyMulligan(
                battleState,
                PlayerId.Player,
                new[] { selectedCardId },
                new NoOpDeckShuffler());

            Assert.That(battleState.Player.Hand.Contains(selectedCardId), Is.False);
            Assert.That(battleState.Player.Deck.CardIds, Does.Contain(selectedCardId));
            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(5));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(5));
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void CreateInitialState_ThirtyThreeCardDecks_DealFiveToBothSeatsAndConserveCards(PlayerId first)
        {
            var playerDeck = Enumerable.Range(0, 33).Select(i => "P-" + i).ToArray();
            var opponentDeck = Enumerable.Range(0, 33).Select(i => "A-" + i).ToArray();
            var state = new BattleSetupService(new NoOpDeckShuffler()).CreateInitialState(
                new BattleSetupRequest(playerDeck, opponentDeck, first, aiMulliganEnabled: true));

            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var player = state.GetPlayer(owner);
                Assert.That(player.Hand.Count, Is.EqualTo(5));
                Assert.That(player.Deck.Count, Is.EqualTo(28));
                Assert.That(player.Hand.Cards.Select(card => card.RuntimeId).Distinct().Count(), Is.EqualTo(5));
                CollectionAssert.AreEquivalent(owner == PlayerId.Player ? playerDeck : opponentDeck,
                    player.Hand.CardIds.Concat(player.Deck.CardIds));
                AssertResources(player.Resources, owner == first ? 3 : 4);
                Assert.That(player.HasUsedMulligan, Is.False);
            }
            Assert.That(state.CardDrawEvents, Is.Empty, "Opening cards are not normal turn-start draws.");
            Assert.That(state.ResourceChangeEvents, Is.Empty);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void ApplyMulligan_AllFiveWithDuplicateCards_ReplacesAllCopiesAndConservesDeck(PlayerId owner)
        {
            var opening = new[] { "same", "same", "opening-2", "opening-3", "opening-4" };
            var replacements = new[] { "same", "replacement-1", "replacement-2", "replacement-3", "replacement-4" };
            var deck = replacements.Reverse().Concat(opening.Reverse()).ToArray();
            var state = new BattleSetupService(new NoOpDeckShuffler()).CreateInitialState(
                new BattleSetupRequest(deck, deck, owner, aiMulliganEnabled: true));
            var player = state.GetPlayer(owner);
            Assert.That(player.Hand.CardIds, Is.EqualTo(opening));

            var mulligan = new MulliganService();
            mulligan.ApplyMulligan(state, owner, opening, new NoOpDeckShuffler());

            Assert.That(player.Hand.CardIds, Is.EqualTo(replacements),
                "Only copies left in the deck can replace the five selected copies, including another copy of the same type.");
            Assert.That(player.Hand.Count, Is.EqualTo(5));
            Assert.That(player.Deck.Count, Is.EqualTo(5));
            CollectionAssert.AreEquivalent(opening, player.Deck.CardIds);
            CollectionAssert.AreEquivalent(deck, player.Hand.CardIds.Concat(player.Deck.CardIds));
            Assert.That(state.CardDrawEvents, Has.Count.EqualTo(5));
            Assert.That(state.CardDrawEvents.Select(e => e.OwnerId), Is.All.EqualTo(owner));
            Assert.That(player.HasUsedMulligan, Is.True);
            Assert.That(state.Phase, Is.EqualTo(PhaseType.Mulligan), "The other human seat must still confirm.");
            AssertResources(player.Resources, 3);
            AssertResources(state.GetOpponent(owner).Resources, 4);

            mulligan.PassMulligan(state, state.GetOpponent(owner).Id);
            Assert.That(state.Phase, Is.EqualTo(PhaseType.TurnStart));
            Assert.That(state.TurnNumber, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() =>
                mulligan.ApplyMulligan(state, owner, replacements, new NoOpDeckShuffler()));
            Assert.That(player.Hand.CardIds, Is.EqualTo(replacements));
            Assert.That(player.Deck.Count, Is.EqualTo(5));
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

        private static void AssertMasterState(
            Project333.Runtime.Domain.Cards.MasterState masterState,
            PlayerId ownerId)
        {
            Assert.That(masterState.OwnerId, Is.EqualTo(ownerId));
            Assert.That(masterState.Position, Is.EqualTo(new TileCoord(2, 1)));
            Assert.That(masterState.Attack, Is.EqualTo(3));
            Assert.That(masterState.MaxHp, Is.EqualTo(333));
            Assert.That(masterState.CurrentHp, Is.EqualTo(333));
            Assert.That(masterState.CanMove, Is.True);
            Assert.That(masterState.HasSummoningSickness, Is.False);
        }

        private static void AssertResources(Project333.Runtime.Domain.Resources.ResourceSet resources, int expectedGold)
        {
            Assert.That(resources.Mana, Is.EqualTo(0));
            Assert.That(resources.Qi, Is.EqualTo(0));
            Assert.That(resources.Power, Is.EqualTo(0));
            Assert.That(resources.Gold, Is.EqualTo(expectedGold));
        }

        private sealed class ReverseDeckShuffler : IDeckShuffler
        {
            public void Shuffle(DeckState deckState)
            {
                var topToBottom = new List<string>();
                while (deckState.TryDraw(out var cardId))
                {
                    topToBottom.Add(cardId);
                }

                for (var i = topToBottom.Count - 1; i >= 0; i--)
                {
                    deckState.AddToBottom(topToBottom[i]);
                }
            }
        }

        private sealed class TrackingReverseDeckShuffler : IDeckShuffler
        {
            public int ShuffleCount { get; private set; }

            public void Shuffle(DeckState deckState)
            {
                ShuffleCount++;

                var cards = new List<string>(deckState.CardIds);
                while (deckState.TryDraw(out _))
                {
                }

                for (var i = cards.Count - 1; i >= 0; i--)
                {
                    deckState.AddToTop(cards[i]);
                }
            }
        }

        private sealed class NoOpDeckShuffler : IDeckShuffler
        {
            public void Shuffle(DeckState deckState)
            {
            }
        }
    }
}
