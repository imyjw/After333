using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Ads;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Tutorial;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Project333.Tests.EditMode
{
    public sealed class TutorialRuntimeIsolationTests
    {
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _hosts = new List<GameObject>();
        private readonly List<Component> _workers = new List<Component>();
        private readonly Dictionary<FieldInfo, object> _sessionBefore = new Dictionary<FieldInfo, object>();
        private readonly List<string> _savedKeys = new List<string>();
        private Scene _previousScene;
        private Scene _fixtureScene;
        private string _fixtureSceneName;
        private bool _ownsFixtureScene;
        private bool _sceneRenamed;
        private string _server, _owner;
        private PendingAccountOperation _pendingOperation;

        [SetUp]
        public void SetUp()
        {
            _sceneRenamed = false;
            _ownsFixtureScene = false;
            _fixtureScene = default;
            _previousScene = SceneManager.GetActiveScene();
            Assert.IsTrue(_previousScene.IsValid() && _previousScene.isLoaded,
                "The EditMode fixture requires an existing loaded scene.");
            // Unity permits naming only unsaved scenes and forbids additive creation
            // while an unsaved scene exists, so reuse one before creating our own.
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var candidate = SceneManager.GetSceneAt(index);
                if (!candidate.IsValid() || !candidate.isLoaded || !string.IsNullOrEmpty(candidate.path)) continue;
                _fixtureScene = candidate;
                break;
            }
            if (!_fixtureScene.IsValid())
            {
                _fixtureScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                _ownsFixtureScene = true;
            }
            _fixtureSceneName = _fixtureScene.name;
            SceneManager.SetActiveScene(_fixtureScene);
            // Rename only an unsaved scene in memory; never save or rename a user asset.
            _fixtureScene.name = "TutorialIsolationTest_" + Guid.NewGuid().ToString("N");
            _sceneRenamed = true;
            TutorialRuntimeIsolation.CancelEntry();
            InvokeStatic(typeof(TutorialRuntimeIsolation), "Initialize");
            _server = Project333ServerEndpointSettings.ResolveHttpUrl(Project333ServerEndpointSettings.LocalHttpUrl);
            _owner = Guid.NewGuid().ToString("D");
            SetSession("AccountId", _owner);
            SetSession("SessionToken", "tutorial-isolation-test-token");
            SetSession("Tickets", 17);
            SetSession("ResourceGold", 91L);
            SetSession("ActiveRunId", "tutorial-isolation-existing-run");
            SetSession("ActiveRunWins", 4);
            SetSession("ActiveRunLosses", 1);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                TutorialRuntimeIsolation.CancelEntry();
                InvokeStatic(typeof(RewardedAdRecovery), "StopObserving");
                foreach (var worker in _workers)
                    if (worker != null) worker.GetType().GetMethod("OnDisable", HiddenInstance).Invoke(worker, null);
                _workers.Clear();
                foreach (var host in _hosts) if (host != null) UnityEngine.Object.DestroyImmediate(host);
                _hosts.Clear();
                if (_pendingOperation != null)
                {
                    _pendingOperation.Complete(_pendingOperation.RequestId, _owner);
                    _pendingOperation.Dispose();
                    _pendingOperation = null;
                }
                foreach (var key in _savedKeys) PlayerPrefs.DeleteKey(key);
                if (_savedKeys.Count > 0) PlayerPrefs.Save();
                _savedKeys.Clear();
                foreach (var value in _sessionBefore) value.Key.SetValue(null, value.Value);
                _sessionBefore.Clear();
            }
            finally
            {
                if (_sceneRenamed && _fixtureScene.IsValid() && _fixtureScene.isLoaded)
                    _fixtureScene.name = _fixtureSceneName;
                if (_previousScene.IsValid() && _previousScene.isLoaded)
                {
                    if (SceneManager.GetActiveScene() != _previousScene)
                        SceneManager.SetActiveScene(_previousScene);
                    InvokeStatic(typeof(TutorialRuntimeIsolation), "HandleActiveSceneChanged", _previousScene, _previousScene);
                }
                if (_ownsFixtureScene && _fixtureScene.IsValid() && _fixtureScene.isLoaded)
                    EditorSceneManager.CloseScene(_fixtureScene, true);
                _sceneRenamed = false;
                _ownsFixtureScene = false;
                TutorialRuntimeIsolation.CancelEntry();
            }
        }

        [Test]
        public void EntryIntentImmediatelySuspendsOnceAndFailedEntryReleasesOnce()
        {
            var changes = new List<bool>();
            Action<bool> handler = active => changes.Add(active);
            TutorialRuntimeIsolation.Changed += handler;
            try
            {
                TutorialRuntimeIsolation.RequestEntry();
                TutorialRuntimeIsolation.RequestEntry();
                Assert.IsTrue(TutorialRuntimeIsolation.IsActive);
                TutorialRuntimeIsolation.CancelEntry();
                TutorialRuntimeIsolation.CancelEntry();
                Assert.IsFalse(TutorialRuntimeIsolation.IsActive);
                CollectionAssert.AreEqual(new[] { true, false }, changes);
                AssertRealSessionUnchanged();
            }
            finally { TutorialRuntimeIsolation.Changed -= handler; }
        }

        [Test]
        public void DirectTutorialSceneIsRecognizedAndLeavingClearsEntryIntent()
        {
            var scene = SceneManager.GetActiveScene();
            var neutralName = scene.name;
            try
            {
                scene.name = TutorialRuntimeIsolation.SceneName;
                InvokeStatic(typeof(TutorialRuntimeIsolation), "HandleActiveSceneChanged", scene, scene);
                Assert.IsTrue(TutorialRuntimeIsolation.IsActive);
                TutorialRuntimeIsolation.CancelEntry();
                Assert.IsTrue(TutorialRuntimeIsolation.IsActive, "The actual tutorial scene remains isolated.");
                TutorialRuntimeIsolation.RequestEntry();
                scene.name = neutralName;
                // A name change does not emit activeSceneChanged; simulate the runtime callback.
                InvokeStatic(typeof(TutorialRuntimeIsolation), "HandleActiveSceneChanged", scene, scene);
                Assert.IsFalse(TutorialRuntimeIsolation.IsActive);
                AssertRealSessionUnchanged();
            }
            finally
            {
                scene.name = neutralName;
                InvokeStatic(typeof(TutorialRuntimeIsolation), "HandleActiveSceneChanged", scene, scene);
            }
        }

        [Test]
        public void AccountRecoveryDuringTutorialRetainsTheExactSavedRequestAndWallet()
        {
            var key = GetPendingOperationKey("ticket_purchase", null);
            _savedKeys.Add(key);
            _savedKeys.Add(GetPendingOperationKey("index", null));
            _pendingOperation = PendingAccountOperation.Begin(_server, "ticket_purchase");
            _pendingOperation.Dispose();
            var before = PlayerPrefs.GetString(key);
            var worker = CreateWorker<AccountOperationRecovery>();
            TutorialRuntimeIsolation.RequestEntry();
            RunCompletedRecovery(worker);
            Assert.IsTrue(PendingAccountOperation.HasPending(_server, "ticket_purchase"));
            Assert.IsFalse(PendingAccountOperation.IsInFlight(_server, "ticket_purchase", null));
            Assert.AreEqual(before, PlayerPrefs.GetString(key));
            AssertRealSessionUnchanged();
            TutorialRuntimeIsolation.CancelEntry();
            Assert.AreEqual(before, PlayerPrefs.GetString(key));
        }

        [Test]
        public void RewardedAdRecoveryDuringTutorialRetainsTheExactAttemptAndWallet()
        {
            var type = typeof(RewardedAdRecovery).Assembly.GetType("Project333.Runtime.Application.Accounts.PendingRewardedAd", true);
            var id = Guid.NewGuid().ToString("D");
            InvokeStatic(type, "Remember", _server, _owner, id);
            var key = (string)InvokeStatic(type, "Key", _server, _owner);
            _savedKeys.Add(key);
            var before = PlayerPrefs.GetString(key);
            var worker = CreateWorker<RewardedAdRecovery>();
            TutorialRuntimeIsolation.RequestEntry();
            RunCompletedRecovery(worker);
            Assert.AreEqual(before, PlayerPrefs.GetString(key));
            AssertRealSessionUnchanged();
            TutorialRuntimeIsolation.CancelEntry();
            Assert.AreEqual(before, PlayerPrefs.GetString(key));
        }

        [Test]
        public void AccountHttpRequestIsCancelledBeforeNativeSendDuringTutorial()
        {
            TutorialRuntimeIsolation.RequestEntry();
            using var request = UnityWebRequest.Get("http://127.0.0.1:1/tutorial-isolation-must-not-send");
            var transport = typeof(AccountOperationRecovery).Assembly.GetType("Project333.Runtime.Application.Accounts.AccountHttpTransport", true);
            var task = (Task)InvokeStatic(transport, "SendAsync", request, CancellationToken.None);
            Assert.IsTrue(task.IsCompleted);
            Assert.Catch<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.IsFalse(request.isDone);
            Assert.AreEqual(0, request.responseCode);
            AssertRealSessionUnchanged();
        }

        [Test]
        public void RealAdSdkSignalsAreDeferredUntilExitAndRetainRewardedReplayProtection()
        {
            var type = typeof(RewardedAdRecovery).Assembly.GetType("Project333.Runtime.Application.Accounts.PendingRewardedAd", true);
            var id = Guid.NewGuid().ToString("D");
            InvokeStatic(type, "Remember", _server, _owner, id);
            var key = (string)InvokeStatic(type, "Key", _server, _owner);
            _savedKeys.Add(key);
            var before = PlayerPrefs.GetString(key);
            CreateWorker<RewardedAdRecovery>();
            var service = new TestRewardedAdService();
            InvokeStatic(typeof(RewardedAdRecovery), "ObserveShow", service, _server, _owner, id);
            TutorialRuntimeIsolation.RequestEntry();
            service.EmitRewarded();
            service.EmitClosed();
            Assert.AreEqual(before, PlayerPrefs.GetString(key));
            AssertRealSessionUnchanged();
            TutorialRuntimeIsolation.CancelEntry();
            var record = JsonUtility.FromJson<SavedAdRecord>(PlayerPrefs.GetString(key));
            Assert.AreEqual(id, record.AttemptId);
            Assert.IsTrue(record.Rewarded);
            Assert.IsFalse(record.CanReplay);
            AssertRealSessionUnchanged();
        }

        [Test]
        public void OrdinarySdkSignalsStillPersistOutsideTutorial()
        {
            var type = typeof(RewardedAdRecovery).Assembly.GetType("Project333.Runtime.Application.Accounts.PendingRewardedAd", true);
            var id = Guid.NewGuid().ToString("D");
            InvokeStatic(type, "Remember", _server, _owner, id);
            var key = (string)InvokeStatic(type, "Key", _server, _owner);
            _savedKeys.Add(key);
            var service = new TestRewardedAdService();
            InvokeStatic(typeof(RewardedAdRecovery), "ObserveShow", service, _server, _owner, id);
            service.EmitClosed();
            var closed = JsonUtility.FromJson<SavedAdRecord>(PlayerPrefs.GetString(key));
            Assert.IsTrue(closed.CanReplay);
            service.EmitRewarded();
            var rewarded = JsonUtility.FromJson<SavedAdRecord>(PlayerPrefs.GetString(key));
            Assert.IsTrue(rewarded.Rewarded);
            Assert.IsFalse(rewarded.CanReplay);
        }

        private T CreateWorker<T>() where T : Component
        {
            var host = new GameObject(typeof(T).Name + "TutorialIsolationTest");
            _hosts.Add(host);
            var worker = host.AddComponent<T>();
            _workers.Add(worker);
            // EditMode does not run ordinary MonoBehaviour activation callbacks.
            // Normalize a possible automatic subscription, then enter the normal lifecycle.
            worker.GetType().GetMethod("OnDisable", HiddenInstance).Invoke(worker, null);
            worker.GetType().GetMethod("OnEnable", HiddenInstance).Invoke(worker, null);
            return worker;
        }

        private static void RunCompletedRecovery(Component worker)
        {
            var task = (Task)worker.GetType().GetMethod("RecoverAsync", HiddenInstance).Invoke(worker, null);
            Assert.IsTrue(task.IsCompleted, "A suspended worker must return without starting HTTP work.");
            Assert.DoesNotThrow(() => task.GetAwaiter().GetResult());
        }

        private string GetPendingOperationKey(string operation, string card) =>
            (string)InvokeStatic(typeof(PendingAccountOperation), "Key", _server, _owner, operation, card);

        private static object InvokeStatic(Type type, string method, params object[] args) =>
            type.GetMethod(method, HiddenStatic).Invoke(null, args);

        private void SetSession(string property, object value)
        {
            var field = typeof(AccountSessionState).GetField("<" + property + ">k__BackingField", HiddenStatic);
            _sessionBefore[field] = field.GetValue(null);
            field.SetValue(null, value);
        }

        private void AssertRealSessionUnchanged()
        {
            Assert.AreEqual(_owner, AccountSessionState.AccountId);
            Assert.AreEqual("tutorial-isolation-test-token", AccountSessionState.SessionToken);
            Assert.AreEqual(17, AccountSessionState.Tickets);
            Assert.AreEqual(91L, AccountSessionState.ResourceGold);
            Assert.AreEqual("tutorial-isolation-existing-run", AccountSessionState.ActiveRunId);
            Assert.AreEqual(4, AccountSessionState.ActiveRunWins);
            Assert.AreEqual(1, AccountSessionState.ActiveRunLosses);
        }

        [Serializable] private sealed class SavedAdRecord
        {
            public string AttemptId;
            public bool Rewarded;
            public bool CanReplay;
        }

        private sealed class TestRewardedAdService : IRewardedAdService
        {
            public event Action StateChanged { add { } remove { } }
            public event Action Rewarded;
            public event Action Closed;
            public bool IsReady => true;
            public bool IsShowing => false;
            public string StatusMessage => string.Empty;
            public void Initialize(string appKey, string providerUserId, string rewardedAdUnitId) { }
            public bool TryShow(string placementName, string dynamicUserId, out string errorMessage)
            { errorMessage = string.Empty; return true; }
            public void EmitRewarded() => Rewarded?.Invoke();
            public void EmitClosed() => Closed?.Invoke();
        }
    }
}
