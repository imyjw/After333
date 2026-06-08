using NUnit.Framework;
using Project333.Runtime.Presentation.Draft;

namespace Project333.Tests.EditMode
{
    public sealed class DraftRunSessionStateTests
    {
        [SetUp]
        public void SetUp()
        {
            DraftRunSessionState.ResetSessionState();
        }

        [Test]
        public void ResetSessionState_RestoresDefaultTicketCount()
        {
            Assert.That(DraftRunSessionState.AvailableTickets, Is.EqualTo(DraftRunSessionState.DefaultStartingTickets));
        }

        [Test]
        public void TrySpendTicketsForNewRun_WhenEnoughTickets_SpendsTicketsAndClearsDraftState()
        {
            DraftRunSessionState.SetDraftDeck(new[] { "card-a", "card-b" });
            DraftRunSessionState.RecordBattleResult(playerWon: true);

            var didSpend = DraftRunSessionState.TrySpendTicketsForNewRun(3);

            Assert.That(didSpend, Is.True);
            Assert.That(DraftRunSessionState.AvailableTickets, Is.EqualTo(DraftRunSessionState.DefaultStartingTickets - 3));
            Assert.That(DraftRunSessionState.CurrentDraftDeckCardIds, Is.Empty);
            Assert.That(DraftRunSessionState.Wins, Is.EqualTo(0));
            Assert.That(DraftRunSessionState.Losses, Is.EqualTo(0));
        }

        [Test]
        public void TrySpendTicketsForNewRun_WhenTicketsAreInsufficient_DoesNotSpend()
        {
            Assert.That(DraftRunSessionState.TrySpendTicketsForNewRun(9), Is.True);

            var didSpend = DraftRunSessionState.TrySpendTicketsForNewRun(3);

            Assert.That(didSpend, Is.False);
            Assert.That(DraftRunSessionState.AvailableTickets, Is.EqualTo(0));
        }
    }
}
