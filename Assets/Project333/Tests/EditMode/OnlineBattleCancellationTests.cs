using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Online;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class OnlineBattleCancellationTests
    {
        [TestCase("battle_cancelled", true)]
        [TestCase("battle_reconnect_expired", true)]
        [TestCase("match_full", false)]
        [TestCase("command_rejected", false)]
        public void TerminalSessionErrorsOnly_EndReconnect(string code, bool expected)
        {
            Assert.That(OnlineBattleConnectionTester.IsUnavailableBattleError(code), Is.EqualTo(expected));
        }

        [Test]
        public void UnavailableSession_DoesNotInventDefeatOrDraw()
        {
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                Enumerable.Repeat("Goblin", 33).ToArray(), Enumerable.Repeat("Goblin", 33).ToArray(), PlayerId.Player));
            var go = new GameObject("CancellationTest");
            go.SetActive(false);
            try
            {
                var controller = go.AddComponent<OnlineBattleConnectionTester>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(OnlineBattleConnectionTester).GetField("_currentProjectedBattleState", flags).SetValue(controller, state);
                typeof(OnlineBattleConnectionTester).GetMethod("FinishUnavailableBattleWithoutLocalResult", flags).Invoke(controller, null);
                Assert.That(state.IsEnded, Is.False);
                Assert.That(state.Result.HasWinner, Is.False);
                Assert.That(state.Result.IsDraw, Is.False);
                Assert.That(typeof(OnlineBattleConnectionTester).GetField("_automaticallyReconnectDuringBattle", flags).GetValue(controller), Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReturnPreservesRunRecordAndDeck_ClearsOldOutcomeAndReconnectLaunch()
        {
            DraftRunSessionState.ResetSessionState();
            try
            {
                var cards = Enumerable.Repeat("Goblin", 33).ToArray();
                DraftRunSessionState.SetDraftDeck(cards);
                DraftRunSessionState.SetServerRunDeckMetadata("same-run", "same-deck");
                DraftRunSessionState.ApplyServerRunRecord(7, 2);
                DraftRunSessionState.SetLastBattleOutcome(DraftBattleOutcome.Victory);
                DraftRunSessionState.QueuePvpReconnectBattleStart();
                DraftRunSessionState.PrepareForUnavailableBattleReturn();
                Assert.That(DraftRunSessionState.Wins, Is.EqualTo(7));
                Assert.That(DraftRunSessionState.Losses, Is.EqualTo(2));
                Assert.That(DraftRunSessionState.ServerRunId, Is.EqualTo("same-run"));
                Assert.That(DraftRunSessionState.ServerDeckId, Is.EqualTo("same-deck"));
                Assert.That(DraftRunSessionState.CurrentDraftDeckCardIds, Is.EqualTo(cards));
                Assert.That(DraftRunSessionState.LastBattleOutcomeText, Is.Empty);
                Assert.That(DraftRunSessionState.HasPendingBattleStart, Is.False);
                Assert.That(DraftRunSessionState.TryConsumePendingBattleStart(out _), Is.False);
            }
            finally { DraftRunSessionState.ResetSessionState(); }
        }
    }
}
