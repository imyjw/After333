using System;
using System.Threading;
using System.Threading.Tasks;
using Project333.Runtime.Application.Ads;
using Project333.Runtime.Presentation;
using UnityEngine;

namespace Project333.Runtime.Application.Accounts
{
    // Resolves saved attempts across scenes. SDK events only update pending metadata.
    public sealed class RewardedAdRecovery : MonoBehaviour
    {
        public static event Action<string, string, RewardedAdAttemptDto> Resolved;
        private static RewardedAdRecovery s_instance;
        private static IRewardedAdService s_observed;
        private static Action s_rewarded, s_closed;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _busy;
        private float _nextAttempt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            StopObserving();
            Resolved = null;
            var host = new GameObject("RewardedAdRecovery");
            DontDestroyOnLoad(host);
            s_instance = host.AddComponent<RewardedAdRecovery>();
        }

        internal static void ObserveShow(IRewardedAdService service, string url, string owner, string id)
        {
            StopObserving();
            s_observed = service;
            s_rewarded = () => { PendingRewardedAd.MarkRewarded(url, owner, id); RequestCheck(); };
            s_closed = () => { PendingRewardedAd.MarkClosed(url, owner, id); RequestCheck(); };
            service.Rewarded += s_rewarded;
            service.Closed += s_closed;
        }

        private static void StopObserving()
        {
            if (s_observed != null) { s_observed.Rewarded -= s_rewarded; s_observed.Closed -= s_closed; }
            s_observed = null;
        }

        public static void RequestCheck() { if (s_instance != null) s_instance._nextAttempt = 0f; }
        private void OnDestroy()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            if (s_instance == this) { StopObserving(); s_instance = null; }
        }
        private void Update()
        {
            if (_busy || Time.unscaledTime < _nextAttempt || !AccountSessionState.IsAuthenticated ||
                UnityEngine.Application.internetReachability == NetworkReachability.NotReachable) return;
            _nextAttempt = Time.unscaledTime + 5f;
            _ = RecoverAsync();
        }

        internal async Task RecoverAsync()
        {
            if (_busy || !AccountSessionState.IsAuthenticated || (s_observed?.IsShowing ?? false)) return;
            _busy = true;
            var url = Project333ServerEndpointSettings.ResolveHttpUrl(Project333ServerEndpointSettings.LocalHttpUrl);
            var owner = AccountSessionState.AccountId;
            var token = AccountSessionState.SessionToken;
            var ct = _lifetime.Token;
            try
            {
                var pending = PendingRewardedAd.Load(url, owner);
                if (pending == null) return;
                var response = await new RewardedTicketClient(url).GetAttemptAsync(token, pending.AttemptId, ct);
                ct.ThrowIfCancellationRequested();
                if (AccountSessionState.AccountId != owner || AccountSessionState.SessionToken != token ||
                    Project333ServerEndpointSettings.ResolveHttpUrl(Project333ServerEndpointSettings.LocalHttpUrl) != url ||
                    PendingRewardedAd.Load(url, owner)?.AttemptId != pending.AttemptId) return;
                if (response == null || response.ResolvedAttemptId != pending.AttemptId) return;
                var status = response.ResolvedStatus;
                if (status == "granted")
                {
                    if (response.ResolvedWallet == null) return;
                    AccountSessionState.ApplyRewardedAdWallet(response.ResolvedWallet);
                }
                else if (status != "expired" && status != "rejected") return;
                PendingRewardedAd.Clear(url, owner, pending.AttemptId);
                Resolved?.Invoke(url, owner, response);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { /* Keep the saved attempt; retry quietly on the next interval. */ }
            finally { _busy = false; }
        }
    }
}