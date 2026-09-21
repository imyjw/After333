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
        private string _lastDiagnostic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            StopObserving();
            Resolved = null;
            var host = new GameObject("RewardedAdRecovery");
            DontDestroyOnLoad(host);
            s_instance = host.AddComponent<RewardedAdRecovery>();
            s_instance.TraceState("worker_started", false);
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

        private void TraceState(string state, bool warning)
        {
            if (_lastDiagnostic == state) return;
            _lastDiagnostic = state;
            var message = $"After333 rewarded ad recovery: {state}";
            if (warning) Debug.LogWarning(message); else Debug.Log(message);
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
                if (pending == null) { TraceState("no_saved_attempt", false); return; }
                var response = await new RewardedTicketClient(url).GetAttemptAsync(token, pending.AttemptId, ct);
                ct.ThrowIfCancellationRequested();
                if (AccountSessionState.AccountId != owner || AccountSessionState.SessionToken != token ||
                    Project333ServerEndpointSettings.ResolveHttpUrl(Project333ServerEndpointSettings.LocalHttpUrl) != url ||
                    PendingRewardedAd.Load(url, owner)?.AttemptId != pending.AttemptId) return;
                if (response == null || response.ResolvedAttemptId != pending.AttemptId)
                { TraceState("response_identity_mismatch", true); return; }
                var status = response.ResolvedStatus;
                if (status == "granted")
                {
                    if (response.ResolvedWallet == null) { TraceState("granted_without_wallet", true); return; }
                    AccountSessionState.ApplyRewardedAdWallet(response.ResolvedWallet);
                }
                else if (status == "pending")
                {
                    // Process death can prevent Closed from arriving. Once the server confirms
                    // this attempt is pending and no ad is showing, allow an explicit retry.
                    // Keep the attempt for late rewards; a live observer preserves a Rewarded flag
                    // that may have arrived while this request was in flight.
                    if (!(s_observed?.IsShowing ?? false))
                        PendingRewardedAd.MarkClosed(url, owner, pending.AttemptId, previousProcess: s_observed == null);
                    var latest = PendingRewardedAd.Load(url, owner);
                    TraceState($"pending: rewarded={latest?.Rewarded}, replay={latest?.CanReplay}", false);
                    return;
                }
                else if (status != "expired" && status != "rejected") { TraceState("unknown_server_status", true); return; }
                PendingRewardedAd.Clear(url, owner, pending.AttemptId);
                TraceState($"resolved: {status}", false);
                Resolved?.Invoke(url, owner, response);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // Do not log response bodies, account/attempt IDs or session credentials.
                TraceState($"retry_after_error: {ex.GetType().Name}", true);
            }
            finally { _busy = false; }
        }
    }
}