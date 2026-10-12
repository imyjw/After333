using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project333.Runtime.Application.Accounts
{
    public sealed class CardPackReceiptException : InvalidOperationException
    {
        public string Code { get; }
        public CardPackReceiptException(string code, string message) : base(message) { Code = code; }
    }

    public sealed class ServerShopClient
    {
        public const string OperationType = "card_pack_purchase";
        public const int CardPackGoldCost = 33;
        public const int CardPackCardCount = 10;
        private readonly string _baseUrl;
        private readonly string _endpointContext;

        public ServerShopClient(string baseUrl)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? Project333.Runtime.Presentation.Project333ServerEndpointSettings.LocalHttpUrl : baseUrl.TrimEnd('/');
            _endpointContext = Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveHttpUrl(
                Project333.Runtime.Presentation.Project333ServerEndpointSettings.LocalHttpUrl).TrimEnd('/');
        }

        public async Task<CardPackPurchaseResponse> PurchaseCardPackAsync(string sessionToken, CancellationToken ct)
        {
            var owner = AccountSessionState.AccountId;
            EnsureContext(owner, sessionToken, ct);
            using var operation = PendingAccountOperation.Begin(_baseUrl, OperationType);
            // Persist before HTTP, closing the response-to-animation crash window.
            PendingCardPackPresentation.Remember(_baseUrl, owner, operation.RequestId);
            try
            {
                var response = await operation.PostAsync<CardPackPurchaseRequest, CardPackPurchaseResponse>(
                    _baseUrl, "/shop/purchase-card-pack", sessionToken,
                    new CardPackPurchaseRequest { RequestId = operation.RequestId }, DescribeError, ct);
                ValidateReceipt(response, operation.RequestId, owner);
                await RefreshCollectionAsync(owner, sessionToken, ct);
                operation.Complete(response.ResolvedRequestId, response.ResolvedAccount.ResolvedId);
                return response;
            }
            catch
            {
                // Documented pre-commit rejection cleared the intent. Uncertain outcomes keep both markers.
                if (ContextMatches(owner, sessionToken) && !PendingAccountOperation.HasPending(_baseUrl, OperationType))
                    PendingCardPackPresentation.Clear(_baseUrl, owner, operation.RequestId);
                throw;
            }
        }

        public async Task<CardPackPurchaseResponse> GetCardPackPurchaseAsync(string sessionToken, string requestId, CancellationToken ct)
        {
            if (!Guid.TryParseExact(requestId, "D", out var id) || id == Guid.Empty)
                throw new ArgumentException("A valid purchase identity is required.", nameof(requestId));
            var owner = AccountSessionState.AccountId;
            EnsureContext(owner, sessionToken, ct);
            using var request = UnityWebRequest.Get(_baseUrl + "/shop/card-pack-purchases/" + requestId);
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + sessionToken);
            await AccountHttpTransport.SendAsync(request, ct);
            if (request.result != UnityWebRequest.Result.Success)
            {
                var code = ErrorCode(request);
                throw new CardPackReceiptException(code, DescribeError(request));
            }
            var response = JsonUtility.FromJson<CardPackPurchaseResponse>(request.downloadHandler.text);
            ValidateReceipt(response, requestId, owner);
            await RefreshCollectionAsync(owner, sessionToken, ct);
            return response;
        }

        private async Task RefreshCollectionAsync(string owner, string token, CancellationToken ct)
        {
            var me = await new GuestAuthClient(_baseUrl).GetMeAsync(token, ct);
            EnsureContext(owner, token, ct);
            if (me?.ResolvedAccount?.ResolvedId == null ||
                !string.Equals(me.ResolvedAccount.ResolvedId, owner, StringComparison.OrdinalIgnoreCase) ||
                me.ResolvedWallet == null || me.ResolvedCollectionSummary == null)
                throw new InvalidOperationException("서버의 보유 카드 정보를 확인하지 못했습니다.");
            AccountSessionState.ApplyMeResponse(me);
        }

        private bool ContextMatches(string owner, string token) => AccountSessionState.IsAuthenticated &&
            string.Equals(AccountSessionState.AccountId, owner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(AccountSessionState.SessionToken, token, StringComparison.Ordinal) &&
            string.Equals(Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveHttpUrl(
                Project333.Runtime.Presentation.Project333ServerEndpointSettings.LocalHttpUrl).TrimEnd('/'), _endpointContext, StringComparison.Ordinal);

        private void EnsureContext(string owner, string token, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(token) || !ContextMatches(owner, token))
                throw new OperationCanceledException("Account context changed.", ct);
        }

        internal static void ValidateReceipt(CardPackPurchaseResponse response, string requestId, string owner)
        {
            if (response?.ResolvedAccount == null || response.ResolvedWallet == null ||
                !string.Equals(response.ResolvedRequestId, requestId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(response.ResolvedAccount.ResolvedId, owner, StringComparison.OrdinalIgnoreCase) ||
                response.ResolvedResourceGoldCost != CardPackGoldCost || response.ResolvedCardIds.Count != CardPackCardCount)
                throw new InvalidOperationException("서버 카드팩 구매 내역을 확인하지 못했습니다.");
            foreach (var card in response.ResolvedCardIds)
                if (string.IsNullOrWhiteSpace(card) || card != card.Trim())
                    throw new InvalidOperationException("서버 카드팩 카드 목록을 확인하지 못했습니다.");
        }

        private static string ErrorCode(UnityWebRequest request)
        {
            try { return JsonUtility.FromJson<ApiErrorEnvelope>(request.downloadHandler?.text)?.ResolvedError?.ResolvedCode; }
            catch { return null; }
        }

        private static string DescribeError(UnityWebRequest request) => ErrorCode(request) switch
        {
            "insufficient_resource_gold" => "골드가 부족합니다. 카드팩 구매에는 골드 33개가 필요합니다.",
            "operation_cancelled" => "카드팩 구매가 취소되었습니다.",
            "card_pack_pool_unavailable" => "카드팩을 구매할 수 없습니다. 잠시 후 다시 시도해주세요.",
            "card_pack_receipt_unavailable" => "카드팩 구매 내역을 확인하지 못했습니다. 잠시 후 다시 시도해주세요.",
            "card_pack_purchase_not_found" => "카드팩 구매 내역이 없습니다.",
            _ => "카드팩 구매 결과를 확인하지 못했습니다."
        };
    }

    [Serializable] public sealed class CardPackPurchaseRequest { public string RequestId; }
    [Serializable] public sealed class CardPackPurchaseResponse
    {
        public string RequestId, requestId;
        public bool Replayed, replayed;
        public long ResourceGoldCost, resourceGoldCost;
        public AuthAccountDto Account, account;
        public WalletDto Wallet, wallet;
        public string[] CardIds, cardIds;
        public string ResolvedRequestId => RequestId ?? requestId;
        public bool ResolvedReplayed => Replayed || replayed;
        public long ResolvedResourceGoldCost => ResourceGoldCost != 0 ? ResourceGoldCost : resourceGoldCost;
        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public IReadOnlyList<string> ResolvedCardIds => CardIds ?? cardIds ?? Array.Empty<string>();
    }
}
