using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Online;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;

namespace Project333.Tests.EditMode
{
    public sealed class TutorialOnlineSessionIsolationTests
    {
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Dictionary<FieldInfo, object> _sessionBefore = new Dictionary<FieldInfo, object>();
        private GameObject _host;
        private OnlineBattleConnectionTester _tester;
        private Scene _previousScene;
        private Scene _fixtureScene;
        private string _fixtureSceneName;
        private bool _ownsFixtureScene;

        [SetUp]
        public void SetUp()
        {
            _previousScene = SceneManager.GetActiveScene();
            _fixtureScene = default;
            _ownsFixtureScene = false;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !string.IsNullOrEmpty(scene.path)) continue;
                _fixtureScene = scene;
                break;
            }
            if (!_fixtureScene.IsValid())
            {
                _fixtureScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                _ownsFixtureScene = true;
            }
            _fixtureSceneName = _fixtureScene.name;
            _fixtureScene.name = "OnlineTutorialIsolation_" + Guid.NewGuid().ToString("N");
            SceneManager.SetActiveScene(_fixtureScene);
            TutorialRuntimeIsolation.CancelEntry();
            SaveAndSet(typeof(AccountSessionState), "<AccountId>k__BackingField", string.Empty);
            SaveAndSet(typeof(AccountSessionState), "<Tickets>k__BackingField", 18);
            SaveAndSet(typeof(AccountSessionState), "<ResourceGold>k__BackingField", 42L);
            SaveAndSet(typeof(DraftRunSessionState), "<ActiveBattleGameMode>k__BackingField", "run");
            SaveAndSet(typeof(DraftRunSessionState), "<ActiveAiChallengeAttemptId>k__BackingField", "real-attempt-sentinel");
            _host = new GameObject("OnlineTutorialIsolationTest");
            _host.SetActive(false); // No Awake/Start/socket connection or persistent object is created.
            _tester = _host.AddComponent<OnlineBattleConnectionTester>();
            Set(_tester, "_status", "real-session-sentinel");
            Set(_tester, "_joinRunId", "real-run-sentinel");
            Set(_tester, "_joinDeckId", "real-deck-sentinel");
        }

        [TearDown]
        public void TearDown()
        {
            if (_fixtureScene.IsValid() && _fixtureScene.isLoaded && string.IsNullOrEmpty(_fixtureScene.path))
                _fixtureScene.name = _fixtureSceneName;
            if (_previousScene.IsValid() && _previousScene.isLoaded)
                SceneManager.SetActiveScene(_previousScene);
            TutorialRuntimeIsolation.CancelEntry();
            UnityEngine.Object.DestroyImmediate(_host);
            foreach (var value in _sessionBefore) value.Key.SetValue(null, value.Value);
            _sessionBefore.Clear();
            if (_ownsFixtureScene && _fixtureScene.IsValid() && _fixtureScene.isLoaded)
                EditorSceneManager.CloseScene(_fixtureScene, true);
        }

        [TestCase("command")]
        [TestCase("endTurn")]
        [TestCase("surrender")]
        [TestCase("mulligan")]
        public void TutorialRejectsRealCommandsWithoutChangingExistingSession(string action)
        {
            TutorialRuntimeIsolation.RequestEntry();
            bool sent;
            string message;
            switch (action)
            {
                case "command": sent = _tester.TrySendPlayerCommand(null, out message); break;
                case "endTurn": sent = _tester.TrySendEndTurn(out message); break;
                case "surrender": sent = _tester.TrySendSurrender(out message); break;
                default: sent = _tester.TrySendMulligan(new[] { "Goblin" }, out message); break;
            }
            Assert.IsFalse(sent);
            StringAssert.Contains("튜토리얼", message);
            Assert.IsFalse(_tester.IsReadyForCommands);
            AssertPreservedSession();
        }

        [Test]
        public void NewConnectionsAndReconfigurationAreRejectedBeforeSocketCreation()
        {
            var server = Get<string>(_tester, "_serverUrl");
            var match = _tester.CurrentMatchId;
            TutorialRuntimeIsolation.RequestEntry();
            _tester.ConfigureForLocalTest("ws://127.0.0.1:1/no-tutorial", "tutorial-match", "tutorial-player", PlayerId.AI);
            _tester.Connect();
            _tester.ConnectForServerAiBattle("ws://127.0.0.1:1", "tutorial-match", "tutorial-player", new[] { "Goblin" });
            _tester.ConnectForPvpMatchmaking("ws://127.0.0.1:1", "tutorial-player", new[] { "Goblin" });
            _tester.DriveBattleScreenFromThisClient();
            _tester.AttachToBattlePresentation(null);
            _tester.DetachBattlePresentation();
            var connect = (Task)Invoke(_tester, "ConnectAsync");
            Assert.IsTrue(connect.IsCompleted);
            Assert.IsNull(Get<object>(_tester, "_messageSender"));
            Assert.AreEqual(server, Get<string>(_tester, "_serverUrl"));
            Assert.AreEqual(match, _tester.CurrentMatchId);
            AssertPreservedSession();
        }

        [Test]
        public void TransportMessagesAndReconnectDeadlinesRemainPendingDuringTutorial()
        {
            var envelope = CreateSeatEnvelope();
            Invoke(_tester, "QueueMessage", envelope);
            Set(_tester, "_isAutomaticReconnectActive", true);
            Set(_tester, "_isResumingBattleFromDraft", true);
            Set(_tester, "_automaticReconnectDeadlineRealtime", -1f);
            Set(_tester, "_nextClientHeartbeatRealtime", 88f);
            TutorialRuntimeIsolation.RequestEntry();
            Invoke(_tester, "Update");
            Invoke(_tester, "DrainQueuedMessages");
            Invoke(_tester, "MonitorAutomaticReconnect");
            Invoke(_tester, "SendClientHeartbeatIfNeeded");

            Assert.AreEqual(1, GetQueueCount("_receivedMessages"));
            Assert.IsTrue(Get<bool>(_tester, "_isAutomaticReconnectActive"));
            Assert.AreEqual(-1f, Get<float>(_tester, "_automaticReconnectDeadlineRealtime"));
            Assert.AreEqual(88f, Get<float>(_tester, "_nextClientHeartbeatRealtime"));
            AssertPreservedSession();
        }

        [Test]
        public void SeatAssignmentCannotChangeRealDraftModeUntilTutorialExit()
        {
            var envelope = CreateSeatEnvelope();
            TutorialRuntimeIsolation.RequestEntry();
            Invoke(_tester, "ApplySeatAssignmentIfOwned", envelope);
            Assert.IsFalse(Get<bool>(_tester, "_hasAssignedSeat"));
            AssertPreservedSession();
            TutorialRuntimeIsolation.CancelEntry();
            Invoke(_tester, "ApplySeatAssignmentIfOwned", envelope);
            Assert.IsTrue(Get<bool>(_tester, "_hasAssignedSeat"));
            Assert.AreEqual("ai_challenge", DraftRunSessionState.ActiveBattleGameMode);
            Assert.AreEqual("server-attempt", DraftRunSessionState.ActiveAiChallengeAttemptId);
        }

        [Test]
        public void PresentationCoroutineKeepsQueuedEnvelopeAndResumesAfterExit()
        {
            TutorialRuntimeIsolation.RequestEntry();
            var envelope = new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.KeepAlive };
            Invoke(_tester, "QueuePresentationEnvelope", envelope);
            var routine = (IEnumerator)Invoke(_tester, "ProcessPresentationQueue");
            Assert.IsTrue(routine.MoveNext());
            Assert.IsNull(routine.Current);
            Assert.AreEqual(1, GetQueueCount("_presentationMessages"));
            AssertPreservedSession();
            TutorialRuntimeIsolation.CancelEntry();
            Assert.IsFalse(routine.MoveNext());
            Assert.AreEqual(0, GetQueueCount("_presentationMessages"));
            StringAssert.Contains("KeepAlive", _tester.Status);
        }

        [UnityTest]
        public IEnumerator PreEntryAsyncContinuationWaitsWithoutDiscardingSessionIntent()
        {
            TutorialRuntimeIsolation.RequestEntry();
            var continuation = (Task<bool>)Invoke(_tester, "WaitUntilTutorialEndsAsync");
            Assert.IsFalse(continuation.IsCompleted);
            AssertPreservedSession();
            TutorialRuntimeIsolation.CancelEntry();
            var deadline = Time.realtimeSinceStartup + 2f;
            while (!continuation.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(continuation.IsCompleted);
            Assert.IsTrue(continuation.GetAwaiter().GetResult());
            AssertPreservedSession();
        }

        [Test]
        public void DestroyedConnectionDoesNotResumeDeferredWork()
        {
            Set(_tester, "_isDestroying", true);
            TutorialRuntimeIsolation.RequestEntry();
            var continuation = (Task<bool>)Invoke(_tester, "WaitUntilTutorialEndsAsync");
            Assert.IsTrue(continuation.IsCompleted);
            Assert.IsFalse(continuation.GetAwaiter().GetResult());
            AssertPreservedSession();
        }

        [Test]
        public void TutorialDoesNotCreateCoordinatorOrCancelExistingMatchmaking()
        {
            var existing = OnlineBattleSessionCoordinator.Instance;
            var coordinator = _host.AddComponent<OnlineBattleSessionCoordinator>();
            Set(coordinator, "_phase", OnlineBattleSessionPhase.Matchmaking);
            Set(coordinator, "_lastError", "real-match-sentinel");
            TutorialRuntimeIsolation.RequestEntry();
            Assert.AreSame(existing, OnlineBattleSessionCoordinator.GetOrCreate());
            coordinator.BeginPvpMatchmaking("ws://127.0.0.1:1", "tutorial-player", new[] { "Goblin" }, false);
            Assert.IsFalse(coordinator.TryAttachToBattle(null));
            var cancel = coordinator.CancelMatchmakingAndDisposeAsync();
            Assert.IsTrue(cancel.IsCompleted);
            Assert.IsFalse(cancel.GetAwaiter().GetResult());
            Assert.AreEqual(OnlineBattleSessionPhase.Matchmaking, coordinator.Phase);
            Assert.AreEqual("real-match-sentinel", coordinator.LastError);
            Assert.AreSame(existing, OnlineBattleSessionCoordinator.Instance);
            AssertPreservedSession();
        }

        [Test]
        public void CoordinatorPhaseProjectionResumesWhenTutorialEnds()
        {
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(
                Enumerable.Repeat("Goblin", 33).ToArray(), Enumerable.Repeat("Goblin", 33).ToArray(), PlayerId.Player));
            Set(_tester, "_hasReceivedFirstStateView", true);
            Set(_tester, "_currentProjectedBattleState", state);
            var coordinator = _host.AddComponent<OnlineBattleSessionCoordinator>();
            Set(coordinator, "_connectionTester", _tester);
            Set(coordinator, "_phase", OnlineBattleSessionPhase.Matchmaking);
            TutorialRuntimeIsolation.RequestEntry();
            Invoke(coordinator, "Update");
            Assert.AreEqual(OnlineBattleSessionPhase.Matchmaking, coordinator.Phase);
            Assert.AreSame(state, _tester.CurrentProjectedBattleState);
            TutorialRuntimeIsolation.CancelEntry();
            Invoke(coordinator, "Update");
            Assert.AreEqual(OnlineBattleSessionPhase.BattleReady, coordinator.Phase);
            Assert.AreSame(state, _tester.CurrentProjectedBattleState);
        }

        private OnlineBattleEnvelope CreateSeatEnvelope() => new OnlineBattleEnvelope
        {
            MessageType = OnlineBattleMessageType.KeepAlive,
            PlayerToken = "player-a",
            HasAssignedSeat = true,
            ConnectionId = "server-connection",
            AssignedSeatId = PlayerId.AI,
            AssignedOnlineSeatId = OnlineBattleSeatId.PlayerB,
            GameMode = "ai_challenge",
            AiChallengeAttemptId = "server-attempt"
        };

        private void AssertPreservedSession()
        {
            Assert.AreEqual("real-session-sentinel", _tester.Status);
            Assert.AreEqual("real-run-sentinel", Get<string>(_tester, "_joinRunId"));
            Assert.AreEqual("real-deck-sentinel", Get<string>(_tester, "_joinDeckId"));
            Assert.AreEqual("run", DraftRunSessionState.ActiveBattleGameMode);
            Assert.AreEqual("real-attempt-sentinel", DraftRunSessionState.ActiveAiChallengeAttemptId);
            Assert.AreEqual(18, AccountSessionState.Tickets);
            Assert.AreEqual(42L, AccountSessionState.ResourceGold);
        }

        private int GetQueueCount(string field)
        {
            var queue = Get<object>(_tester, field);
            return (int)queue.GetType().GetProperty("Count").GetValue(queue);
        }

        private static object Invoke(object owner, string method, params object[] args) =>
            owner.GetType().GetMethod(method, HiddenInstance).Invoke(owner, args);

        private static T Get<T>(object owner, string field) =>
            (T)owner.GetType().GetField(field, HiddenInstance).GetValue(owner);

        private static void Set(object owner, string field, object value) =>
            owner.GetType().GetField(field, HiddenInstance).SetValue(owner, value);

        private void SaveAndSet(Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, HiddenStatic);
            if (!_sessionBefore.ContainsKey(field)) _sessionBefore[field] = field.GetValue(null);
            field.SetValue(null, value);
        }
    }
}