using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project333.Runtime.Application.Accounts
{
    public sealed class ServerCardCollectionClient
    {
        private readonly string _baseUrl;

        public ServerCardCollectionClient(string baseUrl)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? "http://127.0.0.1:7333"
                : baseUrl.TrimEnd('/');
        }

        public async Task<UpgradeCardResponse> UpgradeCardAsync(
            string sessionToken,
            string cardId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("서버 계정 로그인이 필요합니다.");
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new InvalidOperationException("강화할 카드가 선택되지 않았습니다.");
            }

            var request = new UpgradeCardRequest
            {
                CardId = cardId
            };
            var response = await PostJsonAsync<UpgradeCardRequest, UpgradeCardResponse>(
                "/cards/upgrade",
                request,
                sessionToken,
                cancellationToken);
            if (response == null || response.ResolvedUpgradedCard == null)
            {
                throw new InvalidOperationException("서버가 강화 결과를 반환하지 않았습니다.");
            }

            return response;
        }

        private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
            string path,
            TRequest request,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var json = JsonUtility.ToJson(request);
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            using var webRequest = new UnityWebRequest($"{_baseUrl}{path}", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bodyRaw),
                downloadHandler = new DownloadHandlerBuffer()
            };
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {sessionToken}");

            await SendAsync(webRequest, cancellationToken);
            return DeserializeResponse<TResponse>(webRequest);
        }

        private static async Task SendAsync(UnityWebRequest webRequest, CancellationToken cancellationToken)
        {
            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(BuildErrorMessage(webRequest));
            }
        }

        private static TResponse DeserializeResponse<TResponse>(UnityWebRequest webRequest)
        {
            var json = webRequest.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Server returned an empty response.");
            }

            var response = JsonUtility.FromJson<TResponse>(json);
            if (response == null)
            {
                throw new InvalidOperationException("Failed to parse server response.");
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
                    var error = JsonUtility.FromJson<ApiErrorEnvelope>(body);
                    var resolvedError = error?.ResolvedError;
                    if (resolvedError != null && !string.IsNullOrWhiteSpace(resolvedError.ResolvedCode))
                    {
                        switch (resolvedError.ResolvedCode)
                        {
                            case "card_not_owned":
                                return "보유하지 않은 카드는 강화할 수 없습니다.";
                            case "max_level_reached":
                                return "이미 최대 레벨입니다.";
                            case "insufficient_card_copies":
                                return "강화에 필요한 카드 수량이 부족합니다.";
                            case "insufficient_resource_gold":
                                return "강화에 필요한 골드가 부족합니다.";
                            case "card_definition_not_found":
                                return "서버 카드 DB에서 카드를 찾지 못했습니다.";
                            case "upgrade_cost_not_found":
                                return "서버 강화 비용 데이터가 없습니다.";
                        }

                        return $"Server error [{resolvedError.ResolvedCode}]: {resolvedError.ResolvedMessage}";
                    }
                }
                catch
                {
                    // Fall back to the raw HTTP error below.
                }
            }

            return string.IsNullOrWhiteSpace(body)
                ? $"HTTP request failed: {webRequest.responseCode} {webRequest.error}"
                : $"HTTP request failed: {webRequest.responseCode} {webRequest.error} {body}";
        }
    }

    [Serializable]
    public sealed class UpgradeCardRequest
    {
        public string CardId;
    }

    [Serializable]
    public sealed class UpgradeCardCostDto
    {
        public int LevelFrom;
        public int LevelTo;
        public int RequiredCopyCount;
        public long RequiredResourceGold;

        public int levelFrom;
        public int levelTo;
        public int requiredCopyCount;
        public long requiredResourceGold;

        public int ResolvedLevelFrom => LevelFrom != 0 ? LevelFrom : levelFrom;
        public int ResolvedLevelTo => LevelTo != 0 ? LevelTo : levelTo;
        public int ResolvedRequiredCopyCount => RequiredCopyCount != 0 ? RequiredCopyCount : requiredCopyCount;
        public long ResolvedRequiredResourceGold => RequiredResourceGold != 0 ? RequiredResourceGold : requiredResourceGold;
    }

    [Serializable]
    public sealed class UpgradeCardResponse
    {
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public CollectionSummaryDto CollectionSummary;
        public OwnedCardDto UpgradedCard;
        public UpgradeCardCostDto UpgradeCost;

        public AuthAccountDto account;
        public WalletDto wallet;
        public CollectionSummaryDto collectionSummary;
        public OwnedCardDto upgradedCard;
        public UpgradeCardCostDto upgradeCost;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public CollectionSummaryDto ResolvedCollectionSummary => CollectionSummary ?? collectionSummary;
        public OwnedCardDto ResolvedUpgradedCard => UpgradedCard ?? upgradedCard;
        public UpgradeCardCostDto ResolvedUpgradeCost => UpgradeCost ?? upgradeCost;
    }
}
