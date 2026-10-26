using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Startup;
using Project333.Runtime.Presentation.Online;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class SingleAccountSessionTests
    {
        private const string Endpoint = "https://single-login-test.invalid";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly Dictionary<string, object> _savedState = new Dictionary<string, object>();
        private string _googleKey;
        private bool _hadGoogleKey;
        private int _googleValue;
        private string _kindKey;
        private bool _hadKindKey;
        private string _kindValue;

        [SetUp]
        public void SetUp()
        {
            // Credentials go only into an in-memory store; preserve the user's pre-test state and preference.
            foreach (var property in typeof(AccountSessionState).GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (property.SetMethod != null) _savedState[property.Name] = property.GetValue(null);
            _googleKey = Project333ClientProfile.ToScopedPlayerPrefsKey("Project333.AutomaticGoogleSignInSuppressed");
            _hadGoogleKey = PlayerPrefs.HasKey(_googleKey);
            _googleValue = PlayerPrefs.GetInt(_googleKey, 0);
            _kindKey = Project333ClientProfile.ToScopedPlayerPrefsKey("Project333.AccountKind");
            _hadKindKey = PlayerPrefs.HasKey(_kindKey);
            _kindValue = PlayerPrefs.GetString(_kindKey, "");
            AccountCredentialStore.SetStoreForTests(new MemoryCredentialStore());
            AccountSessionState.BindServerEndpoint(Endpoint);
            Login("old-session", "old-refresh");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _savedState)
                typeof(AccountSessionState).GetProperty(item.Key).SetValue(null, item.Value);
            _savedState.Clear();
            AccountCredentialStore.ResetStoreForTests();
            if (_hadGoogleKey) PlayerPrefs.SetInt(_googleKey, _googleValue); else PlayerPrefs.DeleteKey(_googleKey);
            if (_hadKindKey) PlayerPrefs.SetString(_kindKey, _kindValue); else PlayerPrefs.DeleteKey(_kindKey);
            PlayerPrefs.Save();
            DraftRunSessionState.ResetSessionState();
            TutorialRuntimeIsolation.CancelEntry();
        }

        private static void Login(string session, string refresh)
        {
            AccountSessionState.ApplyGoogleAuthResponse(new GoogleAuthResponse
            {
                SessionToken = session, RefreshToken = refresh,
                Account = new AuthAccountDto { Id = "single-login-tests", AccountKind = "registered" },
                Wallet = new WalletDto { ResourceGold = 41, Tickets = 7 }
            });
        }

        [Test]
        public void Replacement_ClearsBothSavedCredentialsAndSuppressesAutomaticGoogleOnce()
        {
            var notified = 0;
            Action observer = () => notified++;
            AccountSessionState.SessionReplaced += observer;
            try
            {
                Assert.That(AccountSessionState.TryHandleSessionReplacement(Endpoint, "old-session"), Is.True);
                Assert.That(AccountSessionState.TryHandleSessionReplacement(Endpoint, "old-session"), Is.False);
                Assert.That(notified, Is.EqualTo(1));
                Assert.That(AccountSessionState.WasSessionReplaced, Is.True);
                Assert.That(AccountSessionState.IsAuthenticated, Is.False);
                Assert.That(AccountSessionState.RefreshToken, Is.Empty);
                Assert.That(AccountSessionState.AccountId, Is.Empty);
                Assert.That(AccountSessionState.ResourceGold, Is.Zero);
                Assert.That(AccountSessionState.IsAutomaticGoogleSignInSuppressed, Is.True);
                Assert.That(AccountSessionState.LoadSavedLoginCredentials(), Is.False);
                Assert.That(AccountSessionState.SessionReplacedMessage, Is.EqualTo("다른 기기에서 로그인하여 연결이 종료되었습니다."));
            }
            finally { AccountSessionState.SessionReplaced -= observer; }
        }

        [TestCase("https://other-test.invalid", "old-session", null)]
        [TestCase(Endpoint, "stale-session", null)]
        [TestCase(Endpoint, "", "stale-refresh")]
        [TestCase(Endpoint, "", "")]
        public void Replacement_StaleServerOrCredentialLeavesNewLoginUntouched(string endpoint, string session, string refresh)
        {
            Assert.That(AccountSessionState.TryHandleSessionReplacement(endpoint, session, refresh), Is.False);
            Assert.That(AccountSessionState.SessionToken, Is.EqualTo("old-session"));
            Assert.That(AccountSessionState.RefreshToken, Is.EqualTo("old-refresh"));
            Assert.That(AccountSessionState.WasSessionReplaced, Is.False);
        }

        [Test]
        public void RefreshReplacement_IsBoundToRefreshCredentialAndWsOriginMapsToHttp()
        {
            Assert.That(AccountSessionState.IsCurrentCredential("wss://single-login-test.invalid/battle", "old-session"), Is.True);
            Assert.That(AccountSessionState.TryHandleSessionReplacement(Endpoint, "", "old-refresh"), Is.True);
            Assert.That(AccountSessionState.RefreshToken, Is.Empty);
        }

        [Test]
        public void ExplicitNewLoginAndNormalRefreshRemainUsable()
        {
            AccountSessionState.TryHandleSessionReplacement(Endpoint, "old-session");
            Login("new-session", "new-refresh");
            Assert.That(AccountSessionState.WasSessionReplaced, Is.False);
            Assert.That(AccountSessionState.IsAutomaticGoogleSignInSuppressed, Is.False);
            AccountSessionState.ApplyRefreshAuthResponse(new RefreshAuthResponse
                { SessionToken = "rotated-session", RefreshToken = "rotated-refresh" });
            Assert.That(AccountSessionState.IsCurrentCredential(Endpoint, "rotated-session"), Is.True);
            Assert.That(AccountSessionState.TryHandleSessionReplacement(Endpoint, "old-session"), Is.False);
            Assert.That(AccountSessionState.SessionToken, Is.EqualTo("rotated-session"));
        }

        [Test]
        public void ChangedServerEndpoint_RebindsAlreadyLoadedCredentialsBeforeNextLogin()
        {
            var root = new GameObject("SingleLoginEndpointTest");
            root.SetActive(false);
            try
            {
                var start = root.AddComponent<GameStartSceneController>();
                Set(start, "_hasLoadedSavedAccountCredentials", true);
                Set(start, "_useServerAccount", false); // No real restore or network request in this fixture.
                Set(start, "_showDevelopmentSmokePanel", false);
                Invoke(start, "HandleServerEndpointChanged", Endpoint, "https://new-login-test.invalid", "test");
                Login("new-server-session", "new-server-refresh");
                Assert.That(AccountSessionState.ServerUrl, Is.EqualTo("https://new-login-test.invalid"));
                Assert.That(AccountSessionState.IsCurrentCredential("https://new-login-test.invalid", "new-server-session"), Is.True);
                Assert.That(AccountSessionState.TryHandleSessionReplacement(Endpoint, "new-server-session"), Is.False);
                Assert.That(AccountSessionState.TryHandleSessionReplacement("https://new-login-test.invalid", "new-server-session"), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Replacement_PreservesUncertainPurchaseRequestForReloginRecovery()
        {
            string requestId;
            using (var intent = PendingAccountOperation.Begin(Endpoint, "ticket_purchase")) requestId = intent.RequestId;
            Assert.That(AccountSessionState.TryHandleSessionReplacement(Endpoint, "old-session"), Is.True);
            Login("new-session", "new-refresh");
            using var recovered = PendingAccountOperation.Begin(Endpoint, "ticket_purchase");
            Assert.That(recovered.RequestId, Is.EqualTo(requestId));
            recovered.Complete(requestId, "single-login-tests");
        }

        [TestCase(true, true, false, false, true)]
        [TestCase(false, true, false, false, false)]
        [TestCase(true, false, false, false, false)]
        [TestCase(true, true, true, false, false)]
        [TestCase(true, true, false, true, false)]
        public void GuardPolling_RequiresForegroundAccountAndExcludesTutorialAndConcurrentRequest(
            bool authenticated, bool foreground, bool tutorial, bool pending, bool expected)
        {
            Assert.That(AccountSessionGuard.ShouldPoll(authenticated, foreground, tutorial, pending), Is.EqualTo(expected));
            Assert.That(AccountSessionGuard.PollIntervalSeconds, Is.EqualTo(5f));
        }

        [TestCase("{\"error\":{\"code\":\"session_replaced\"}}", true)]
        [TestCase("{\"Error\":{\"Code\":\"session_replaced\"}}", true)]
        [TestCase("{\"error\":{\"code\":\"invalid_session\"}}", false)]
        [TestCase("<html>error</html>", false)]
        public void HttpReplacement_RequiresStructuredExactCode(string body, bool expected)
        {
            var transport = typeof(AccountSessionState).Assembly.GetType("Project333.Runtime.Application.Accounts.AccountHttpTransport");
            var parse = transport.GetMethod("IsSessionReplacedResponse", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(parse.Invoke(null, new object[] { body }), Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BattleReplacement_ErrorOrCloseStopsRetryWithoutLocalResult(bool close)
        {
            var root = new GameObject("SingleLoginBattleTest");
            root.SetActive(false);
            try
            {
                var battle = root.AddComponent<OnlineBattleConnectionTester>();
                Set(battle, "_connectionSessionToken", "old-session");
                Set(battle, "_connectionServerUrl", "wss://single-login-test.invalid/battle");
                Set(battle, "_presentStateViewsToBattleScreen", false);
                Set(battle, "_isAutomaticReconnectActive", true);
                DraftRunSessionState.ApplyServerRunRecord(8, 2);
                if (close) Invoke(battle, "QueueConnectionClosed", AccountSessionState.SessionReplacedCode);
                else Invoke(battle, "QueueMessage", new OnlineBattleEnvelope
                {
                    MessageType = OnlineBattleMessageType.Error,
                    Error = new OnlineBattleErrorDto { Code = AccountSessionState.SessionReplacedCode }
                });
                Invoke(battle, "DrainQueuedMessages");
                Assert.That(AccountSessionState.IsAuthenticated, Is.False);
                Assert.That(Get(battle, "_automaticallyReconnectDuringBattle"), Is.False);
                Assert.That(Get(battle, "_isAutomaticReconnectActive"), Is.False);
                Assert.That(battle.Status, Is.EqualTo(AccountSessionState.SessionReplacedMessage));
                Assert.That(DraftRunSessionState.Wins, Is.EqualTo(8));
                Assert.That(DraftRunSessionState.Losses, Is.EqualTo(2));
                Assert.That(DraftRunSessionState.LastBattleOutcomeText, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void StaleSocketReplacement_DoesNotLogOutANewerSession()
        {
            var root = new GameObject("StaleLoginBattleTest");
            root.SetActive(false);
            try
            {
                var battle = root.AddComponent<OnlineBattleConnectionTester>();
                Set(battle, "_connectionSessionToken", "old-session");
                Set(battle, "_connectionServerUrl", "wss://single-login-test.invalid/battle");
                Set(battle, "_presentStateViewsToBattleScreen", false);
                Login("new-session", "new-refresh");
                Invoke(battle, "HandleSocketSessionReplacement");
                Assert.That(AccountSessionState.SessionToken, Is.EqualTo("new-session"));
                Assert.That(AccountSessionState.WasSessionReplaced, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator HttpReplacement_RevokedRefreshCannotAutoLoginAgain()
        {
            using var server = new OneResponseServer(401, "{\"error\":{\"code\":\"session_replaced\"}}");
            AccountSessionState.BindServerEndpoint(server.Url);
            var request = new GuestAuthClient(server.Url).RefreshSessionAsync("old-refresh", "test", CancellationToken.None);
            var deadline = Time.realtimeSinceStartup + 10;
            while (!request.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.IsCompleted, Is.True);
            Assert.That(request.Exception?.GetBaseException(), Is.TypeOf<AccountSessionReplacedException>());
            Assert.That(AccountSessionState.RefreshToken, Is.Empty);
            Assert.That(AccountSessionState.IsAutomaticGoogleSignInSuppressed, Is.True);
        }

        [UnityTest]
        public IEnumerator HttpOldSuccess_AfterNewLoginCannotRehydrateTheOldAccount()
        {
            using var server = new OneResponseServer(200,
                "{\"SessionToken\":\"late-old-session\",\"RefreshToken\":\"late-old-refresh\"}", holdResponse: true);
            AccountSessionState.BindServerEndpoint(server.Url);
            var request = new GuestAuthClient(server.Url).RefreshSessionAsync("old-refresh", "test", CancellationToken.None);
            var deadline = Time.realtimeSinceStartup + 10;
            while (!server.RequestReceived && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(server.RequestReceived, Is.True);
            Login("new-session", "new-refresh");
            server.ReleaseResponse();
            while (!request.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.IsCompleted, Is.True);
            Assert.That(request.IsCanceled || request.Exception?.GetBaseException() is OperationCanceledException, Is.True);
            Assert.That(AccountSessionState.SessionToken, Is.EqualTo("new-session"));
        }

        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, PrivateInstance).SetValue(owner, value);
        private static object Get(object owner, string name) => owner.GetType().GetField(name, PrivateInstance).GetValue(owner);
        private static void Invoke(object owner, string name, params object[] values) => owner.GetType().GetMethod(name, PrivateInstance).Invoke(owner, values);

        private sealed class MemoryCredentialStore : IAccountCredentialStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string StorageName => "Single-login isolated test store";
            public bool IsSecure => false;
            public bool TryGetString(string key, out string value) => _values.TryGetValue(key, out value);
            public bool TrySetString(string key, string value) { _values[key] = value; return true; }
            public bool TryDeleteKey(string key) { _values.Remove(key); return true; }
            public void Flush() { }
        }

        private sealed class OneResponseServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly ManualResetEventSlim _release;
            private volatile bool _requestReceived;
            public bool RequestReceived => _requestReceived;
            public string Url { get; }
            public OneResponseServer(int status, string body, bool holdResponse = false)
            {
                _release = new ManualResetEventSlim(!holdResponse);
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var client = await _listener.AcceptTcpClientAsync();
                        using var stream = client.GetStream();
                        var buffer = new byte[4096];
                        await stream.ReadAsync(buffer, 0, buffer.Length);
                        _requestReceived = true;
                        if (!_release.Wait(TimeSpan.FromSeconds(10))) return;
                        var payload = Encoding.UTF8.GetBytes(body);
                        var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Test\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header, 0, header.Length);
                        await stream.WriteAsync(payload, 0, payload.Length);
                    }
                    catch (ObjectDisposedException) { }
                    catch (SocketException) { }
                    catch (System.IO.IOException) { }
                });
            }
            public void ReleaseResponse() => _release.Set();
            public void Dispose() { _release.Set(); _listener.Stop(); }
        }
    }
}
