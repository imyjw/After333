using System;
using System.Threading;
using System.Threading.Tasks;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Development;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Startup;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project333.Runtime.Presentation
{
    // One foreground-only lightweight check across scenes; account changes remain server authoritative.
    public sealed class AccountSessionGuard : MonoBehaviour
    {
        public const float PollIntervalSeconds = 5f;
        public const string LoginSceneName = "GameStart_VSlice";
        private static AccountSessionGuard s_instance;
        private CancellationTokenSource _pollCancellation;
        private float _nextPollTime;
        private bool _polling;
        private bool _focused = true;
        private bool _paused;
        private bool _pendingNotice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => s_instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstalled()
        {
            if (s_instance != null) return;
            var owner = new GameObject("AccountSessionGuard");
            DontDestroyOnLoad(owner);
            s_instance = owner.AddComponent<AccountSessionGuard>();
        }

        private void OnEnable()
        {
            AccountSessionState.SessionReplaced += HandleSessionReplacement;
            TutorialRuntimeIsolation.Changed += HandleTutorialChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _nextPollTime = 0f;
        }

        private void OnDisable()
        {
            AccountSessionState.SessionReplaced -= HandleSessionReplacement;
            TutorialRuntimeIsolation.Changed -= HandleTutorialChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            CancelPoll();
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused) CancelPoll(); else _nextPollTime = 0f;
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (paused) CancelPoll(); else _nextPollTime = 0f;
        }

        private void HandleTutorialChanged(bool active)
        {
            if (active) CancelPoll(); else _nextPollTime = 0f;
        }

        public static bool ShouldPoll(bool authenticated, bool foreground, bool tutorialActive, bool inFlight) =>
            authenticated && foreground && !tutorialActive && !inFlight;

        private void Update()
        {
            if (ShouldPoll(AccountSessionState.IsAuthenticated, _focused && !_paused,
                    TutorialRuntimeIsolation.IsActive, _polling) &&
                !string.IsNullOrWhiteSpace(AccountSessionState.ServerUrl) && Time.unscaledTime >= _nextPollTime)
                _ = PollAsync();
        }

        private async Task PollAsync()
        {
            _polling = true;
            var cancellation = new CancellationTokenSource();
            _pollCancellation = cancellation;
            var endpoint = AccountSessionState.ServerUrl;
            var token = AccountSessionState.SessionToken;
            try
            {
                await new GuestAuthClient(endpoint).CheckSessionStatusAsync(token, cancellation.Token);
            }
            catch (OperationCanceledException) { }
            catch (AccountSessionReplacedException) { } // Central transport publishes the scoped transition.
            catch (Exception) { } // Network failure is not proof of replacement. Retry on the next interval.
            finally
            {
                if (_pollCancellation == cancellation) _pollCancellation = null;
                cancellation.Dispose();
                _polling = false;
                _nextPollTime = Time.unscaledTime + PollIntervalSeconds;
            }
        }

        private void CancelPoll() => _pollCancellation?.Cancel();

        private void HandleSessionReplacement()
        {
            CancelPoll();
            if (TutorialRuntimeIsolation.IsActive) return;
            _pendingNotice = true;
            // Discard local navigation intent only, never economy recovery ledgers or a battle result.
            DraftRunSessionState.ResetSessionState();
            var scene = SceneManager.GetActiveScene();
            if (scene.name != LoginSceneName)
            {
                SceneManager.LoadScene(LoginSceneName);
                return;
            }
            PresentReplacementNotice(scene);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_pendingNotice && scene.name == LoginSceneName) PresentReplacementNotice(scene);
        }

        private void PresentReplacementNotice(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var start in root.GetComponentsInChildren<GameStartSceneController>(true))
                    start.HandleSessionReplacementFromServer();
            SimpleNoticeToast.Show("SessionReplacedNoticeCanvas", AccountSessionState.SessionReplacedMessage,
                sortingOrder: AccountLoginGateView.SortingOrder + 1);
            _pendingNotice = false;
        }
    }
}
