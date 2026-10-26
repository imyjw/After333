using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Startup;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class GoogleLoginAfterReplacementTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _owner;
        private GameStartSceneController _start;
        private AccountLoginGateView _gate;
        private readonly Dictionary<string, object> _savedState = new Dictionary<string, object>();
        private readonly Dictionary<string, string> _preferences = new Dictionary<string, string>();
        private string _googleKey;
        private bool _hadGoogleKey;
        private int _googleValue;

        [SetUp]
        public void SetUp()
        {
            foreach (var property in typeof(AccountSessionState).GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (property.SetMethod != null) _savedState[property.Name] = property.GetValue(null);
            _googleKey = Project333ClientProfile.ToScopedPlayerPrefsKey("Project333.AutomaticGoogleSignInSuppressed");
            _hadGoogleKey = PlayerPrefs.HasKey(_googleKey);
            _googleValue = PlayerPrefs.GetInt(_googleKey, 0);
            foreach (var raw in new[] { Project333ServerEndpointSettings.HttpUrlPlayerPrefsKey,
                         Project333ServerEndpointSettings.BattleWebSocketUrlPlayerPrefsKey, "Project333.AccountKind" })
            {
                var key = Project333ClientProfile.ToScopedPlayerPrefsKey(raw);
                _preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            }
            Project333ServerEndpointSettings.ClearOverrides();
            AccountCredentialStore.SetStoreForTests(new MemoryStore());
            AccountSessionState.ClearSavedSession();
            AccountSessionState.SetAutomaticGoogleSignInSuppressed(true);
            TutorialRuntimeIsolation.CancelEntry();
            // Normal MonoBehaviours do not auto-run lifecycle in an EditMode fixture.
            // Invoke the actual restore/forced-logout callbacks with native login UI.
            _owner = new GameObject("ReplacementGoogleStart");
            _start = _owner.AddComponent<GameStartSceneController>();
            var gate = new GameObject("AccountLoginGateCanvas", typeof(RectTransform));
            gate.transform.SetParent(_owner.transform, false);
            _gate = gate.AddComponent<AccountLoginGateView>();
            _gate.EnsureEditableHierarchy();
            Set(_start, "_accountLoginGate", _gate);
            Set(_start, "_hasLoadedSavedAccountCredentials", true);
            Set(_start, "_googleDesktopClientId", "synthetic-desktop.apps.googleusercontent.com");
            Set(_start, "_googleAndroidServerClientId", "synthetic-web.apps.googleusercontent.com");
            Set(_start, "_useServerAccount", true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_start != null) Invoke(_start, "OnDisable");
            if (_owner != null) Object.DestroyImmediate(_owner);
            AccountCredentialStore.ResetStoreForTests();
            foreach (var item in _savedState) typeof(AccountSessionState).GetProperty(item.Key).SetValue(null, item.Value);
            _savedState.Clear();
            foreach (var item in _preferences)
                if (item.Value == null) PlayerPrefs.DeleteKey(item.Key); else PlayerPrefs.SetString(item.Key, item.Value);
            _preferences.Clear();
            if (_hadGoogleKey) PlayerPrefs.SetInt(_googleKey, _googleValue); else PlayerPrefs.DeleteKey(_googleKey);
            PlayerPrefs.Save();
            TutorialRuntimeIsolation.CancelEntry();
        }

        [UnityTest]
        public IEnumerator ForcedLogout_DuringInitialStatusRead_EnablesExplicitGoogleWithoutAutomaticLogin()
        {
            using var server = new StatusServer(holdFirst: true);
            Configure(server.Url);
            Invoke(_start, "StartAccountRestoreIfNeeded");
            yield return WaitFor(() => server.RequestCount >= 1);
            _start.HandleSessionReplacementFromServer();
            Assert.That(_gate.gameObject.activeSelf, Is.True, "Forced logout must leave login choices visible.");
            yield return WaitFor(() => GoogleButton.interactable, 3f);
            Assert.That(GoogleButton.interactable, Is.True,
                "Canceled account restore must not leave Google readiness unknown forever.");
            Assert.That(GoogleLabel.text, Is.EqualTo("Google로 로그인"));
            Assert.That(server.RequestCount, Is.EqualTo(2), "A fresh public readiness request must replace the canceled one.");
            AssertNoAuthentication(server);
            Assert.That(Get(_start, "_accountLoginGateStatus"), Is.EqualTo(AccountSessionState.SessionReplacedMessage));
        }

        [UnityTest]
        public IEnumerator KnownReadiness_ForcedLogoutKeepsGoogleReadyWithoutAnotherRequest()
        {
            using var server = new StatusServer();
            Configure(server.Url);
            Set(_start, "_serverStatus", ConfiguredStatus());
            _start.HandleSessionReplacementFromServer();
            yield return null;
            Assert.That(GoogleButton.interactable, Is.True);
            Assert.That(GoogleLabel.text, Is.EqualTo("Google로 로그인"));
            Assert.That(server.RequestCount, Is.Zero);
            AssertNoAuthentication(server);
        }

        [UnityTest]
        public IEnumerator DisabledStart_CancelsReadinessAndDiscardsLateResponse()
        {
            using var server = new StatusServer(holdAll: true);
            Configure(server.Url);
            _start.HandleSessionReplacementFromServer();
            yield return WaitFor(() => server.RequestCount == 1);
            Assert.That(server.RequestCount, Is.EqualTo(1), "Missing Google readiness must be queried.");
            Invoke(_start, "OnDisable");
            server.Release();
            yield return WaitFor(() => Get(_start, "_replacementReadinessCancellation") == null);
            yield return null;
            Assert.That(Get(_start, "_serverStatus"), Is.Null);
            AssertNoAuthentication(server);
        }

        [Test]
        public void TutorialReplacement_DoesNotReadStatusOrChangeLoginUi()
        {
            using var server = new StatusServer();
            Configure(server.Url);
            TutorialRuntimeIsolation.RequestEntry();
            try
            {
                _start.HandleSessionReplacementFromServer();
                Assert.That(server.RequestCount, Is.Zero);
            }
            finally { TutorialRuntimeIsolation.CancelEntry(); }
        }

        private void Configure(string url)
        {
            Set(_start, "_accountServerUrl", url);
            Project333ServerEndpointSettings.SaveHttpUrlOverride(url);
            AccountSessionState.BindServerEndpoint(url);
        }
        private UnityEngine.UI.Button GoogleButton => (UnityEngine.UI.Button)Get(_gate, "_googleButton");
        private UnityEngine.UI.Text GoogleLabel => (UnityEngine.UI.Text)Get(_gate, "_googleButtonLabel");
        private static ServerStatusResponse ConfiguredStatus() => new ServerStatusResponse
        {
            Status = "Ok", Authentication = new ServerAuthenticationStatusDto
            { Google = new ServerGoogleAuthStatusDto { Configured = true, AcceptedClientIdCount = 2, DesktopCodeExchangeConfigured = true } }
        };
        private static void AssertNoAuthentication(StatusServer server)
        {
            Assert.That(AccountSessionState.IsAuthenticated, Is.False);
            Assert.That(AccountSessionState.RefreshToken, Is.Empty);
            Assert.That(AccountSessionState.IsAutomaticGoogleSignInSuppressed, Is.True);
            Assert.That(server.UnexpectedPaths, Is.Empty, "Readiness must not call /auth/google, /auth/refresh or /me.");
        }
        private static IEnumerator WaitFor(Func<bool> ready, float seconds = 5f)
        {
            var limit = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < limit) yield return null;
        }
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
        private static object Get(object owner, string name) => owner.GetType().GetField(name, Hidden).GetValue(owner);
        private static void Invoke(object owner, string name) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, null);
        private sealed class MemoryStore : IAccountCredentialStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string StorageName => "Replacement readiness test";
            public bool IsSecure => false;
            public bool TryGetString(string key, out string value) => _values.TryGetValue(key, out value);
            public bool TrySetString(string key, string value) { _values[key] = value; return true; }
            public bool TryDeleteKey(string key) { _values.Remove(key); return true; }
            public void Flush() { }
        }
        private sealed class StatusServer : IDisposable
        {
            private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
            private readonly bool _holdFirst, _holdAll;
            private int _count;
            public int RequestCount => Volatile.Read(ref _count);
            public ConcurrentQueue<string> UnexpectedPaths { get; } = new ConcurrentQueue<string>();
            public string Url { get; }
            public StatusServer(bool holdFirst = false, bool holdAll = false)
            {
                _holdFirst = holdFirst; _holdAll = holdAll;
                _listener.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (true) { var client = await _listener.AcceptTcpClientAsync(); _ = Serve(client); }
                    }
                    catch (ObjectDisposedException) { }
                    catch (SocketException) { }
                });
            }
            private async Task Serve(TcpClient client)
            {
                try
                {
                    using (client)
                    using (var stream = client.GetStream())
                    {
                        var buffer = new byte[4096]; var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        var firstLine = Encoding.ASCII.GetString(buffer, 0, read).Split('\n')[0];
                        if (!firstLine.StartsWith("GET /server/status ", StringComparison.Ordinal)) UnexpectedPaths.Enqueue(firstLine);
                        var count = Interlocked.Increment(ref _count);
                        if ((_holdAll || _holdFirst && count == 1) && !_release.Wait(TimeSpan.FromSeconds(10))) return;
                        var json = "{\"Status\":\"Ok\",\"Authentication\":{\"Google\":{\"Configured\":true,\"AcceptedClientIdCount\":2,\"DesktopCodeExchangeConfigured\":true}}}";
                        var body = Encoding.UTF8.GetBytes(json);
                        var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header, 0, header.Length); await stream.WriteAsync(body, 0, body.Length);
                    }
                }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
                catch (System.IO.IOException) { }
            }
            public void Release() => _release.Set();
            public void Dispose() { _release.Set(); _listener.Stop(); }
        }
    }
}
