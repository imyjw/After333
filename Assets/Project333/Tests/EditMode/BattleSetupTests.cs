using System;
using System.Collections.Generic;
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

            AssertResources(battleState.Player.Resources);
            AssertResources(battleState.AI.Resources);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(3));
            Assert.That(battleState.AI.Hand.Count, Is.EqualTo(4));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(7));
            Assert.That(battleState.AI.Deck.Count, Is.EqualTo(6));
            Assert.That(battleState.Player.HasUsedMulligan, Is.False);
            Assert.That(battleState.AI.HasUsedMulligan, Is.True);
        }

        [Test]
        public void CreateInitialState_AIFirst_UsesReversedOpeningHandSizes()
        {
            var service = new BattleSetupService();
            var request = CreateRequest(PlayerId.AI);

            var battleState = service.CreateInitialState(request);

            Assert.That(battleState.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(4));
            Assert.That(battleState.AI.Hand.Count, Is.EqualTo(3));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(6));
            Assert.That(battleState.AI.Deck.Count, Is.EqualTo(7));
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
            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(3));
            Assert.That(battleState.Player.Hand.Contains(selectedCardId), Is.False);
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(7));
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

        private static void AssertResources(Project333.Runtime.Domain.Resources.ResourceSet resources)
        {
            Assert.That(resources.Mana, Is.EqualTo(0));
            Assert.That(resources.Qi, Is.EqualTo(0));
            Assert.That(resources.Power, Is.EqualTo(0));
            Assert.That(resources.Gold, Is.EqualTo(3));
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
    }
}
