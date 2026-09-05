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
        public void QueueBattleStart_WhenDraftDeckReady_PreservesLaunchModeUntilConsumed()
        {
            var draftedDeck = CreateDeck(33);
            DraftRunSessionState.SetDraftDeck(draftedDeck);

            DraftRunSessionState.QueueBattleStart(DraftBattleLaunchMode.OnlineMatchmaking);

            Assert.That(DraftRunSessionState.HasPendingBattleStart, Is.True);
            Assert.That(
                DraftRunSessionState.TryConsumePendingBattleStart(out var consumedDeck, out var launchMode),
                Is.True);
            Assert.That(launchMode, Is.EqualTo(DraftBattleLaunchMode.OnlineMatchmaking));
            Assert.That(consumedDeck, Is.EqualTo(draftedDeck));
            Assert.That(DraftRunSessionState.HasPendingBattleStart, Is.False);
        }

        [Test]
        public void QueueBattleStart_WhenDraftDeckIsIncomplete_DoesNotQueueBattle()
        {
            DraftRunSessionState.SetDraftDeck(CreateDeck(32));

            DraftRunSessionState.QueueBattleStart(DraftBattleLaunchMode.OnlineMatchmaking);

            Assert.That(DraftRunSessionState.HasPendingBattleStart, Is.False);
            Assert.That(DraftRunSessionState.PendingBattleLaunchMode, Is.EqualTo(DraftBattleLaunchMode.Local));
            Assert.That(
                DraftRunSessionState.TryConsumePendingBattleStart(out var consumedDeck, out var launchMode),
                Is.False);
            Assert.That(consumedDeck, Is.Empty);
            Assert.That(launchMode, Is.EqualTo(DraftBattleLaunchMode.Local));
        }

        [Test]
        public void RecordBattleResult_WhenDeckWasCompleted_KeepsDeckAvailableForDraftReturn()
        {
            var draftedDeck = CreateDeck(33);
            DraftRunSessionState.SetDraftDeck(draftedDeck);

            DraftRunSessionState.RecordBattleResult(playerWon: true);

            Assert.That(DraftRunSessionState.Wins, Is.EqualTo(1));
            Assert.That(DraftRunSessionState.Losses, Is.EqualTo(0));
            Assert.That(DraftRunSessionState.LastBattleOutcomeText, Is.EqualTo("Victory"));
            Assert.That(DraftRunSessionState.HasDraftedDeckReady, Is.True);
            Assert.That(DraftRunSessionState.CurrentDraftDeckCardIds, Is.EqualTo(draftedDeck));
            Assert.That(DraftRunSessionState.LastCompletedDraftDeckCardIds, Is.EqualTo(draftedDeck));
        }

        [Test]
        public void RecordBattleResult_WhenCurrentDeckWasCleared_RestoresCompletedDeckForDraftReturn()
        {
            var draftedDeck = CreateDeck(33);
            DraftRunSessionState.SetDraftDeck(draftedDeck);
            DraftRunSessionState.SetDraftDeck(new string[0]);

            DraftRunSessionState.RecordBattleResult(playerWon: false);

            Assert.That(DraftRunSessionState.Wins, Is.EqualTo(0));
            Assert.That(DraftRunSessionState.Losses, Is.EqualTo(1));
            Assert.That(DraftRunSessionState.LastBattleOutcomeText, Is.EqualTo("Defeat"));
            Assert.That(DraftRunSessionState.HasDraftedDeckReady, Is.True);
            Assert.That(DraftRunSessionState.CurrentDraftDeckCardIds, Is.EqualTo(draftedDeck));
            Assert.That(DraftRunSessionState.LastCompletedDraftDeckCardIds, Is.EqualTo(draftedDeck));
        }

        [Test]
        public void RecordBattleDraw_DoesNotChangeWinsOrLossesAndShowsDraw()
        {
            var draftedDeck = CreateDeck(33);
            DraftRunSessionState.SetDraftDeck(draftedDeck);
            DraftRunSessionState.ApplyServerRunRecord(2, 1);

            DraftRunSessionState.RecordBattleDraw();

            Assert.That(DraftRunSessionState.Wins, Is.EqualTo(2));
            Assert.That(DraftRunSessionState.Losses, Is.EqualTo(1));
            Assert.That(DraftRunSessionState.LastBattleOutcomeText, Is.EqualTo("Draw"));
            Assert.That(DraftRunSessionState.HasDraftedDeckReady, Is.True);
        }

        [Test]
        public void TryApplyAuthoritativeServerRunRecord_WhenRunCompleted_OverridesStaleLocalRecord()
        {
            DraftRunSessionState.ApplyServerRunRecord(3, 2);

            var applied = DraftRunSessionState.TryApplyAuthoritativeServerRunRecord(
                3,
                3,
                isCompletedRun: true);

            Assert.That(applied, Is.True);
            Assert.That(DraftRunSessionState.Wins, Is.EqualTo(3));
            Assert.That(DraftRunSessionState.Losses, Is.EqualTo(3));
            Assert.That(DraftRunSessionState.HasRunEnded, Is.True);
        }

        [Test]
        public void TryApplyAuthoritativeServerRunRecord_WhenActiveServerRecordIsBehind_KeepsLocalRecord()
        {
            DraftRunSessionState.ApplyServerRunRecord(3, 2);

            var applied = DraftRunSessionState.TryApplyAuthoritativeServerRunRecord(
                3,
                1,
                isCompletedRun: false);

            Assert.That(applied, Is.False);
            Assert.That(DraftRunSessionState.Wins, Is.EqualTo(3));
            Assert.That(DraftRunSessionState.Losses, Is.EqualTo(2));
        }

        private static string[] CreateDeck(int count)
        {
            var deck = new string[count];
            for (var i = 0; i < deck.Length; i++)
            {
                deck[i] = $"card-{i:D2}";
            }

            return deck;
        }
    }
}
