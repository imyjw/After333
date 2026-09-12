using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project333.Runtime.Application.Accounts
{
    public sealed class RewardedTicketClient
    {
        private readonly string _baseUrl;

        public RewardedTicketClient(string baseUrl)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? "http://127.0.0.1:7333"
                : baseUrl.TrimEnd('/');
        }

        public async Task<RewardedAdAttemptDto> CreateAttemptAsync(
            string sessionToken,
            string providerUserId,
            CancellationToken cancellationToken)
        {
            RequireSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(providerUserId))
            {
                throw new InvalidOperationException("Rewarded ad provider user id is missing.");
            }

            var request = new CreateRewardedAdAttemptDto
            {
                ProviderUserId = providerUserId
            };
            return await SendAsync<RewardedAdAttemptDto>(
                UnityWebRequest.kHttpVerbPOST,
                "/ads/rewarded-ticket/attempt",
                JsonUtility.ToJson(request),
                sessionToken,
                cancellationToken);
        }

        public async Task<RewardedAdAttemptDto> GetAttemptAsync(
            string sessionToken,
            string attemptId,
            CancellationToken cancellationToken)
        {
            RequireSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(attemptId))
            {
                throw new InvalidOperationException("Rewarded ad attempt id is missing.");
            }

            return await SendAsync<RewardedAdAttemptDto>(
                UnityWebRequest.kHttpVerbGET,
                $"/ads/rewarded-ticket/attempt/{UnityWebRequest.EscapeURL(attemptId)}",
                null,
                sessionToken,
                cancellationToken);
        }

        private async Task<TResponse> SendAsync<TResponse>(
            string method,
            string path,
            string jsonBody,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            using var webRequest = new UnityWebRequest($"{_baseUrl}{path}", method)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            if (jsonBody != null)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                webRequest.SetRequestHeader("Content-Type", "application/json");
            }

            webRequest.SetRequestHeader("Accept", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {sessionToken}");

            await AccountHttpTransport.SendAsync(webRequest, cancellationToken);
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(BuildErrorMessage(webRequest));
            }

            var json = webRequest.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Server returned an empty rewarded ad response.");
            }

            var response = JsonUtility.FromJson<TResponse>(json);
            if (response == null)
            {
                throw new InvalidOperationException("Failed to parse rewarded ad response.");
            }

            return response;
        }

        private static string BuildErrorMessage(UnityWebRequest webRequest)
        {
            var body = webRequest.downloadHandler?.text;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var envelope = JsonUtility.FromJson<ApiErrorEnvelope>(body);
                    var error = envelope?.ResolvedError;
                    if (error != null && !string.IsNullOrWhiteSpace(error.ResolvedCode))
                    {
                        return $"Server error [{error.ResolvedCode}]: {error.ResolvedMessage}";
                    }
                }
                catch
                {
                    // Use the raw HTTP response below.
                }
            }

            return string.IsNullOrWhiteSpace(body)
                ? $"HTTP request failed: {webRequest.responseCode} {webRequest.error}"
                : $"HTTP request failed: {webRequest.responseCode} {webRequest.error} {body}";
        }

        private static void RequireSessionToken(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }
        }
    }

    [Serializable]
    public sealed class CreateRewardedAdAttemptDto
    {
        public string ProviderUserId;
    }

    [Serializable]
    public sealed class RewardedAdAttemptDto
    {
        public bool CanShow;
        public string AttemptId;
        public string DynamicUserId;
        public string Provider;
        public string Placement;
        public string Status;
        public string ReasonCode;
        public string Message;
        public int RewardTicketCount;
        public int RemainingDailyRewards;
        public int CooldownSeconds;
        public string ExpiresAtUtc;
        public WalletDto Wallet;

        public bool canShow;
        public string attemptId;
        public string dynamicUserId;
        public string provider;
        public string placement;
        public string status;
        public string reasonCode;
        public string message;
        public int rewardTicketCount;
        public int remainingDailyRewards;
        public int cooldownSeconds;
        public string expiresAtUtc;
        public WalletDto wallet;

        public bool ResolvedCanShow => CanShow || canShow;
        public string ResolvedAttemptId => Resolve(AttemptId, attemptId);
        public string ResolvedDynamicUserId => Resolve(DynamicUserId, dynamicUserId);
        public string ResolvedProvider => Resolve(Provider, provider);
        public string ResolvedPlacement => Resolve(Placement, placement);
        public string ResolvedStatus => Resolve(Status, status);
        public string ResolvedReasonCode => Resolve(ReasonCode, reasonCode);
        public string ResolvedMessage => Resolve(Message, message);
        public int ResolvedRewardTicketCount => RewardTicketCount != 0 ? RewardTicketCount : rewardTicketCount;
        public int ResolvedRemainingDailyRewards => RemainingDailyRewards != 0
            ? RemainingDailyRewards
            : remainingDailyRewards;
        public int ResolvedCooldownSeconds => CooldownSeconds != 0 ? CooldownSeconds : cooldownSeconds;
        public string ResolvedExpiresAtUtc => Resolve(ExpiresAtUtc, expiresAtUtc);
        public WalletDto ResolvedWallet => Wallet ?? wallet;

        private static string Resolve(string primary, string fallback)
        {
            return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
        }
    }
}
