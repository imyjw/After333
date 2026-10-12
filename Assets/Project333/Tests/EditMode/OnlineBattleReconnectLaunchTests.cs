using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Online;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class OnlineBattleReconnectLaunchTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp() => DraftRunSessionState.ResetSessionState();

        [TearDown]
        public void TearDown() => DraftRunSessionState.ResetSessionState();

        [TestCase("{\"hasReconnectableBattle\":true,\"battleMode\":\"pve\",\"matchId\":\"pve-existing\",\"remainingSeconds\":27}", true, "pve-existing", 27)]
        [TestCase("{\"HasReconnectableBattle\":true,\"BattleMode\":\"PVE\",\"MatchId\":\"pve-existing\",\"RemainingSeconds\":19}", true, "pve-existing", 19)]
        [TestCase("{\"hasReconnectableBattle\":true,\"matchId\":\"pvp-existing\",\"remainingSeconds\":44}", false, "pvp-existing", 44)]
        public void ReconnectResponse_PreservesModeMatchAndServerTime(
            string json, bool serverAi, string matchId, int remainingSeconds)
        {
            var response = JsonUtility.FromJson<PvpReconnectStatusResponse>(json);
            Assert.That(response.ResolvedHasReconnectableBattle, Is.True);
            Assert.That(response.ResolvedIsServerAiBattle, Is.EqualTo(serverAi));
            Assert.That(response.ResolvedMatchId, Is.EqualTo(matchId));
            Assert.That(response.ResolvedRemainingSeconds, Is.EqualTo(remainingSeconds));
        }

        [Test]
        public void PveReconnect_AfterAppRestart_UsesExistingMatchWithoutLocalDeck()
        {
            DraftRunSessionState.QueuePveReconnectBattleStart("existing-server-match", 27);
            Assert.That(DraftRunSessionState.TryConsumePendingBattleStart(out var deck, out var mode), Is.True);
            Assert.That(mode, Is.EqualTo(DraftBattleLaunchMode.OnlineServerAi));
            Assert.That(deck, Is.Empty);
            Assert.That(DraftRunSessionState.TryConsumeLastPveReconnect(out var matchId, out var seconds), Is.True);
            Assert.That(matchId, Is.EqualTo("existing-server-match"));
            Assert.That(seconds, Is.InRange(1, 27));
            Assert.That(DraftRunSessionState.TryConsumeLastPveReconnect(out _, out _), Is.False);
            Assert.That(DraftRunSessionState.TryConsumePendingBattleStart(out _, out _), Is.False);
        }

        [TestCase("", 60)]
        [TestCase("match", 0)]
        [TestCase("match", -1)]
        public void PveReconnect_RequiresServerMatchAndRemainingGrace(string matchId, int seconds)
        {
            Assert.Throws<ArgumentException>(() => DraftRunSessionState.QueuePveReconnectBattleStart(matchId, seconds));
            Assert.That(DraftRunSessionState.HasPendingBattleStart, Is.False);
        }

        [Test]
        public void NewBattle_DoesNotReusePreviousPveReconnectMatch()
        {
            DraftRunSessionState.SetDraftDeck(Enumerable.Repeat("Goblin", 33).ToArray());
            DraftRunSessionState.QueuePveReconnectBattleStart("old-match", 60);
            DraftRunSessionState.QueueBattleStart(DraftBattleLaunchMode.OnlineServerAi);
            Assert.That(DraftRunSessionState.TryConsumePendingBattleStart(out var deck, out var mode), Is.True);
            Assert.That(deck.Count, Is.EqualTo(33));
            Assert.That(mode, Is.EqualTo(DraftBattleLaunchMode.OnlineServerAi));
            Assert.That(DraftRunSessionState.TryConsumeLastPveReconnect(out _, out _), Is.False);
        }

        [Test]
        public void ServerCancellation_ClearsPveReconnectAndPreservesAuthoritativeRecord()
        {
            DraftRunSessionState.ApplyServerRunRecord(7, 2);
            DraftRunSessionState.QueuePveReconnectBattleStart("cancelled-match", 60);
            DraftRunSessionState.TryConsumePendingBattleStart(out _, out _);
            DraftRunSessionState.PrepareForUnavailableBattleReturn();
            Assert.That(DraftRunSessionState.TryConsumeLastPveReconnect(out _, out _), Is.False);
            Assert.That(DraftRunSessionState.Wins, Is.EqualTo(7));
            Assert.That(DraftRunSessionState.Losses, Is.EqualTo(2));
            Assert.That(DraftRunSessionState.LastBattleOutcomeText, Is.Empty);
        }

        [TestCase(DraftBattleLaunchMode.OnlineServerAi, "전투 재접속 (31초)", "전투 재접속 (31초)")]
        [TestCase(DraftBattleLaunchMode.OnlineMatchmaking, "전투 재접속 (31초)", "전투 재접속 (31초)")]
        public void DraftReconnect_UsesCommonBattleLabel(
            DraftBattleLaunchMode reconnectMode, string pveLabel, string pvpLabel)
        {
            var root = new GameObject("ReconnectButtonTest");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<DraftSceneController>();
                typeof(DraftSceneController).GetField("_hasReconnectableBattle", PrivateInstance).SetValue(controller, true);
                typeof(DraftSceneController).GetField("_reconnectLaunchMode", PrivateInstance).SetValue(controller, reconnectMode);
                typeof(DraftSceneController).GetField("_reconnectRemainingSeconds", PrivateInstance).SetValue(controller, 31);
                var buildLabel = typeof(DraftSceneController).GetMethod("BuildBattleButtonReadyLabel", PrivateInstance);
                Assert.That(buildLabel.Invoke(controller, new object[] { DraftBattleLaunchMode.OnlineServerAi, "PVE" }), Is.EqualTo(pveLabel));
                Assert.That(buildLabel.Invoke(controller, new object[] { DraftBattleLaunchMode.OnlineMatchmaking, "PVP" }), Is.EqualTo(pvpLabel));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(23)]
        [TestCase(65)]
        [TestCase(70)]
        public void RelaunchReconnect_KeepsRetryingBeforeFirstStateView_WithoutExtendingServerGrace(int remainingSeconds)
        {
            var root = new GameObject("RelaunchReconnectTest");
            root.SetActive(false);
            try
            {
                var tester = root.AddComponent<OnlineBattleConnectionTester>();
                var testerType = typeof(OnlineBattleConnectionTester);
                var startedAt = UnityEngine.Time.unscaledTime;
                testerType.GetMethod("BeginPveReconnectFromDraft", PrivateInstance).Invoke(tester, new object[] { remainingSeconds });
                testerType.GetField("_nextAutomaticReconnectAttemptRealtime", PrivateInstance).SetValue(tester, float.MaxValue);
                testerType.GetMethod("MonitorAutomaticReconnect", PrivateInstance).Invoke(tester, null);
                Assert.That(testerType.GetField("_currentProjectedBattleState", PrivateInstance).GetValue(tester), Is.Null);
                Assert.That(testerType.GetField("_isAutomaticReconnectActive", PrivateInstance).GetValue(tester), Is.True);
                var deadline = (float)testerType.GetField("_automaticReconnectDeadlineRealtime", PrivateInstance).GetValue(tester);
                Assert.That(deadline - startedAt, Is.EqualTo((float)remainingSeconds).Within(0.1f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}