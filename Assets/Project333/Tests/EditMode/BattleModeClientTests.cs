using System;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Startup;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Online;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class BattleModeClientTests
    {
#if UNITY_EDITOR
        [TestCase("StartBattleFromUi")]
        [TestCase("StartPveBattleFromUi")]
        [TestCase("StartPvpBattleFromUi")]
        public void BattleButtonBinding_DisablesSerializedLaunchAndInvokesExactlyOnce(string oldMethod)
        {
            var go = new GameObject("ChallengeButtonTest");
            go.SetActive(false);
            try
            {
                var controller = go.AddComponent<DraftSceneController>();
                var button = go.AddComponent<UnityEngine.UI.Button>();
                var oldAction = (UnityEngine.Events.UnityAction)Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction), controller, oldMethod);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, oldAction);
                button.onClick.AddListener(oldAction);
                var calls = 0;
                var unrelatedCalls = 0;
                UnityEngine.Events.UnityAction action = () => calls++;
                button.onClick.AddListener(() => unrelatedCalls++);
                var bind = typeof(DraftSceneController).GetMethod("BindBattleModeButtonClick", PrivateInstance);
                bind.Invoke(controller, new object[] { button, action });
                bind.Invoke(controller, new object[] { button, action });
                Assert.That(button.onClick.GetPersistentListenerState(0),
                    Is.EqualTo(UnityEngine.Events.UnityEventCallState.Off));
                button.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(unrelatedCalls, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(go); }
        }
#endif

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void SetUp() => DraftRunSessionState.ResetSessionState();
        [TearDown] public void TearDown() => DraftRunSessionState.ResetSessionState();

        [TestCase("pvp", "run", "", DraftBattleLaunchMode.OnlineMatchmaking)]
        [TestCase("pve", "run", "", DraftBattleLaunchMode.OnlineServerAi)]
        [TestCase("pve", "ai_challenge", "attempt-1", DraftBattleLaunchMode.OnlineAiChallenge)]
        public void CommonReconnect_PreservesServerModeAndOriginalBattle(
            string opponent, string mode, string attempt, DraftBattleLaunchMode expected)
        {
            DraftRunSessionState.QueueBattleReconnect(new PvpReconnectStatusResponse
            {
                HasReconnectableBattle = true, MatchId = "existing", RemainingSeconds = 29,
                BattleMode = opponent, GameMode = mode, AiChallengeAttemptId = attempt
            });
            Assert.That(DraftRunSessionState.TryConsumePendingBattleStart(out var deck, out var launch), Is.True);
            Assert.That(deck, Is.Empty);
            Assert.That(launch, Is.EqualTo(expected));
            Assert.That(DraftRunSessionState.ActiveBattleGameMode, Is.EqualTo(mode));
            Assert.That(DraftRunSessionState.ActiveAiChallengeAttemptId, Is.EqualTo(attempt));
            Assert.That(DraftRunSessionState.ActiveBattleIsReconnect, Is.True);
            if (opponent == "pve")
            {
                Assert.That(DraftRunSessionState.TryConsumeLastPveReconnect(out var match, out var seconds), Is.True);
                Assert.That(match, Is.EqualTo("existing"));
                Assert.That(seconds, Is.InRange(1, 29));
                Assert.That(DraftRunSessionState.ActiveAiChallengeAttemptId, Is.EqualTo(attempt));
            }
        }

        [Test]
        public void NewChallenge_UsesServerAdmissionWithoutPretendingBattleAlreadyStarted()
        {
            DraftRunSessionState.QueueAiChallengeBattleStart("reserved-match", "attempt-2");
            Assert.That(DraftRunSessionState.TryConsumePendingBattleStart(out _, out var mode), Is.True);
            Assert.That(mode, Is.EqualTo(DraftBattleLaunchMode.OnlineAiChallenge));
            Assert.That(DraftRunSessionState.ActiveBattleIsReconnect, Is.False);
            Assert.That(DraftRunSessionState.TryConsumeLastPveReconnect(out var match, out _), Is.True);
            Assert.That(match, Is.EqualTo("reserved-match"));
            Assert.That(DraftRunSessionState.ActiveAiChallengeAttemptId, Is.EqualTo("attempt-2"));
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void ChallengeOutcomeAndReceipt_NeverChangeRunCounters(bool won, bool draw)
        {
            DraftRunSessionState.ApplyServerRunRecord(9, 2);
            DraftRunSessionState.ApplyAuthoritativeBattleMode("ai_challenge", "attempt-3");
            DraftRunSessionState.RecordAiChallengeOutcome(won, draw);
            Assert.That(DraftRunSessionState.LastBattleOutcomeText, Does.Not.Contain("+10"));
            DraftRunSessionState.ApplyAiChallengeReceipt(new AiChallengeAttemptResponse
            {
                AttemptId = "attempt-3", Status = "completed", Won = won, RewardGold = won ? 10 : 0
            });
            Assert.That(DraftRunSessionState.Wins, Is.EqualTo(9));
            Assert.That(DraftRunSessionState.Losses, Is.EqualTo(2));
            Assert.That(DraftRunSessionState.PendingAiChallengeResultAttemptId, Is.Empty);
            if (won) Assert.That(DraftRunSessionState.LastBattleOutcomeText, Does.Contain("+10"));
        }

        [Test]
        public void ChallengeJoin_RoundTripsAdmissionAndGameMode()
        {
            var json = OnlineBattleMessageSerializer.SerializeJoinMatch(
                "match", "token", null, true, false, "run", "deck",
                gameMode: "ai_challenge", aiChallengeAttemptId: "attempt");
            var result = OnlineBattleMessageSerializer.DeserializeEnvelope(json);
            Assert.That(result.GameMode, Is.EqualTo("ai_challenge"));
            Assert.That(result.AiChallengeAttemptId, Is.EqualTo("attempt"));
            Assert.That(result.UseServerAiOpponent, Is.True);
            Assert.That(result.UseMatchmakingQueue, Is.False);
        }

        [Test]
        public void ServerAiFallback_AdoptsServerSeatAndMode()
        {
            var root = new GameObject("ModeAssignment");
            root.SetActive(false);
            try
            {
                var tester = root.AddComponent<OnlineBattleConnectionTester>();
                tester.ConfigureForLocalTest("ws://localhost", "queue", "owned", PlayerId.AI, false);
                typeof(OnlineBattleConnectionTester).GetMethod("ApplySeatAssignmentIfOwned", PrivateInstance).Invoke(tester,
                    new object[] { new OnlineBattleEnvelope
                    {
                        HasAssignedSeat = true, PlayerToken = "owned", MatchId = "fallback", ConnectionId = "connection",
                        AssignedSeatId = PlayerId.Player, AssignedOnlineSeatId = OnlineBattleSeatId.PlayerB,
                        UseServerAiOpponent = true, GameMode = "run"
                    } });
                Assert.That(tester.LocalPlayerId, Is.EqualTo(PlayerId.Player));
                Assert.That(tester.LocalOnlineSeatId, Is.EqualTo(OnlineBattleSeatId.PlayerB));
                Assert.That(tester.CurrentMatchId, Is.EqualTo("fallback"));
                Assert.That(tester.IsAiChallenge, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("ai_challenge_not_available")]
        [TestCase("ai_challenge_not_found")]
        [TestCase("battle_reconnect_expired")]
        public void UnavailableChallenge_QueuesReceiptWithoutInventingRunResult(string code)
        {
            DraftRunSessionState.ApplyServerRunRecord(8, 1);
            DraftRunSessionState.ApplyAuthoritativeBattleMode("ai_challenge", "missing-attempt");
            var root = new GameObject("UnavailableChallenge");
            root.SetActive(false);
            try
            {
                var tester = root.AddComponent<OnlineBattleConnectionTester>();
                typeof(OnlineBattleConnectionTester).GetField("_joinGameMode", PrivateInstance).SetValue(tester, "ai_challenge");
                typeof(OnlineBattleConnectionTester).GetMethod("HandleEnvelope", PrivateInstance).Invoke(tester,
                    new object[] { new OnlineBattleEnvelope {
                        MessageType = OnlineBattleMessageType.Error,
                        Error = new OnlineBattleErrorDto { Code = code }
                    } });
                DraftRunSessionState.PrepareForUnavailableBattleReturn();
                Assert.That(DraftRunSessionState.PendingAiChallengeResultAttemptId, Is.EqualTo("missing-attempt"));
                Assert.That(DraftRunSessionState.Wins, Is.EqualTo(8));
                Assert.That(DraftRunSessionState.Losses, Is.EqualTo(1));
                Assert.That(DraftRunSessionState.HasPendingBattleStart, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void StartScreenReconnectLabel_IsOpponentAgnostic()
        {
            var root = new GameObject("StartReconnect");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<GameStartSceneController>();
                typeof(GameStartSceneController).GetField("_pvpReconnectRemainingSeconds", PrivateInstance).SetValue(controller, 21);
                var method = typeof(GameStartSceneController).GetMethod("BuildReconnectPvpButtonLabel", PrivateInstance);
                Assert.That(method, Is.Not.Null);
                Assert.That(method.Invoke(controller, null), Is.EqualTo("전투 재접속 (21초)"));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
