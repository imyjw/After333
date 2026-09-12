using System;
using System.Threading;
using UnityEngine;
using Project333.Runtime.Presentation;

namespace Project333.Runtime.Application.Accounts
{
    // Lives across scene changes. Never retries a purchase/upgrade POST.
    public sealed class AccountOperationRecovery : MonoBehaviour
    {
        [Serializable] private sealed class ResolveRequest { public string RequestId; }
        [Serializable] private sealed class Resolution
        {
            public string RequestId, AccountId, Status;
            public string requestId, accountId, status;
            public string Id => RequestId ?? requestId;
            public string Owner => AccountId ?? accountId;
            public string Outcome => Status ?? status;
        }
        public static event Action<string, string, bool, MeResponse> Resolved;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _busy;
        private float _nextAttempt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Resolved = null;
            var host = new GameObject("AccountOperationRecovery");
            DontDestroyOnLoad(host);
            host.AddComponent<AccountOperationRecovery>();
        }
        private void OnDestroy() { _lifetime.Cancel(); _lifetime.Dispose(); }
        private void Update()
        {
            if (_busy || Time.unscaledTime < _nextAttempt || !AccountSessionState.IsAuthenticated ||
                string.IsNullOrWhiteSpace(AccountSessionState.AccountId) ||
                UnityEngine.Application.internetReachability == NetworkReachability.NotReachable) return;
            _nextAttempt = Time.unscaledTime + 5f;
            _ = RecoverAsync();
        }
        internal async System.Threading.Tasks.Task RecoverAsync()
        {
            _busy = true;
            var ct = _lifetime.Token;
            var baseUrl = Project333ServerEndpointSettings.ResolveHttpUrl(Project333ServerEndpointSettings.LocalHttpUrl);
            var owner = AccountSessionState.AccountId;
            var token = AccountSessionState.SessionToken;
            try
            {
                foreach (var card in PendingAccountOperation.PendingCards(baseUrl))
                {
                    var kind = card == "" ? "ticket_purchase" : "card_upgrade";
                    if (!PendingAccountOperation.HasPending(baseUrl, kind, card) ||
                        PendingAccountOperation.IsInFlight(baseUrl, kind, card)) continue;
                    using var operation = PendingAccountOperation.Begin(baseUrl, kind, card);
                    var response = await operation.PostAsync<ResolveRequest, Resolution>(baseUrl,
                        "/account/operations/resolve", token, new ResolveRequest { RequestId = operation.RequestId },
                        _ => "Account operation resolution unavailable.", ct);
                    if (response == null || response.Id != operation.RequestId ||
                        !string.Equals(response.Owner, owner, StringComparison.OrdinalIgnoreCase) ||
                        (response.Outcome != "completed" && response.Outcome != "cancelled"))
                        throw new InvalidOperationException("Invalid operation resolution.");
                    // Keep pending until the current wallet/collection is successfully refreshed.
                    var me = await new GuestAuthClient(baseUrl).GetMeAsync(token, ct);
                    ct.ThrowIfCancellationRequested();
                    if (AccountSessionState.AccountId != owner || AccountSessionState.SessionToken != token ||
                        Project333ServerEndpointSettings.ResolveHttpUrl(Project333ServerEndpointSettings.LocalHttpUrl) != baseUrl)
                        return;
                    if (me?.ResolvedAccount?.ResolvedId != owner || me.ResolvedWallet == null || me.ResolvedCollectionSummary == null)
                        throw new InvalidOperationException("Invalid refreshed account.");
                    AccountSessionState.ApplyMeResponse(me);
                    operation.Complete(response.Id, response.Owner);
                    Resolved?.Invoke(kind, card, response.Outcome == "completed", me);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { /* Retain pending state and retry silently at the next interval. */ }
            finally { _busy = false; }
        }
    }
}
