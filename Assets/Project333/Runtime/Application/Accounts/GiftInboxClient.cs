using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project333.Runtime.Application.Accounts
{
    /// <summary>Only the server can award inbox attachments; retries use the same gift ID.</summary>
    public sealed class GiftInboxClient
    {
        private readonly string _baseUrl;
        public GiftInboxClient(string baseUrl) => _baseUrl = baseUrl.TrimEnd('/');

        public Task<GiftInboxResponse> GetAsync(string sessionToken, string cursor, CancellationToken cancellationToken) =>
            SendAsync<GiftInboxResponse>("/gifts" + (string.IsNullOrEmpty(cursor) ? string.Empty : "?cursor=" + Uri.EscapeDataString(cursor)),
                "GET", sessionToken, cancellationToken);

        public async Task<GiftClaimResponse> ClaimAsync(string sessionToken, string giftId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(giftId)) throw new ArgumentException("선물이 선택되지 않았습니다.", nameof(giftId));
            var result = await SendAsync<GiftClaimResponse>("/gifts/" + Uri.EscapeDataString(giftId) + "/claim", "POST", sessionToken, cancellationToken);
            if (!string.Equals(result.ResolvedGiftId, giftId, StringComparison.Ordinal) || result.ResolvedWallet == null)
                throw new GiftInboxException("invalid_response", "선물 정보를 불러오지 못했습니다.", true);
            return result;
        }

        private async Task<T> SendAsync<T>(string route, string method, string sessionToken, CancellationToken cancellationToken) where T : class
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new GiftInboxException("unauthorized", "로그인 후 선물함을 이용할 수 있습니다.", false);
            using var request = new UnityWebRequest(_baseUrl + route, method) { downloadHandler = new DownloadHandlerBuffer() };
            request.SetRequestHeader("Authorization", "Bearer " + sessionToken);
            request.SetRequestHeader("Accept", "application/json");
            await AccountHttpTransport.SendAsync(request, cancellationToken);
            if (request.result != UnityWebRequest.Result.Success)
            {
                string code = null;
                try
                {
                    if (TryGetRootField(request.downloadHandler.text, "Error", "error", out var errorJson))
                        code = JsonUtility.FromJson<ApiErrorDto>(errorJson)?.ResolvedCode;
                }
                catch (ArgumentException) { }
                throw new GiftInboxException(code ?? "request_failed", ErrorMessage(code, request.responseCode),
                    request.responseCode == 0 || request.responseCode >= 500 || request.responseCode == 408 || request.responseCode == 429);
            }
            try
            {
                if (typeof(T) == typeof(GiftClaimResponse))
                    return (T)(object)ParseClaimResponse(request.downloadHandler.text);
                var response = JsonUtility.FromJson<T>(request.downloadHandler.text);
                if (response == null || response is GiftInboxResponse inbox && inbox.Gifts == null && inbox.gifts == null)
                    throw new ArgumentException("Invalid gift response.");
                return response;
            }
            catch (ArgumentException) { throw new GiftInboxException("invalid_response", "선물 정보를 불러오지 못했습니다.", true); }
        }

        public static GiftClaimResponse ParseClaimResponse(string json)
        {
            try
            {
                var response = JsonUtility.FromJson<GiftClaimResponse>(json);
                // JsonUtility may instantiate absent nested DTOs. Inspect actual root fields,
                // then parse the two authoritative integer values without zero-as-missing heuristics.
                if (response == null || string.IsNullOrWhiteSpace(response.ResolvedGiftId) ||
                    !TryGetRootField(json, "Wallet", "wallet", out var walletJson) ||
                    !TryGetRootField(walletJson, "ResourceGold", "resourceGold", out var goldJson) ||
                    !TryGetRootField(walletJson, "Tickets", "tickets", out var ticketsJson) ||
                    !long.TryParse(goldJson, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gold) || gold < 0 ||
                    !int.TryParse(ticketsJson, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tickets) || tickets < 0)
                    throw new ArgumentException("Invalid gift wallet response.");
                response.Wallet = new WalletDto { ResourceGold = gold, Tickets = tickets };
                response.wallet = null;
                return response;
            }
            catch (ArgumentException) { throw new GiftInboxException("invalid_response", "선물 정보를 불러오지 못했습니다.", true); }
        }

        private static bool TryGetRootField(string json, string pascalName, string camelName, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            var index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index++] != '{') return false;
            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == '}') return value != null;
                var keyStart = index;
                if (!SkipJsonString(json, ref index)) return false;
                var key = json.Substring(keyStart + 1, index - keyStart - 2);
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index++] != ':') return false;
                SkipWhitespace(json, ref index);
                var valueStart = index;
                if (!SkipJsonValue(json, ref index)) return false;
                if (key == pascalName || key == camelName)
                {
                    // Conflicting duplicate casing must never choose an arbitrary wallet.
                    if (value != null) return false;
                    value = json.Substring(valueStart, index - valueStart).Trim();
                }
                SkipWhitespace(json, ref index);
                if (index >= json.Length) return false;
                if (json[index] == '}') return value != null;
                if (json[index++] != ',') return false;
            }
            return false;
        }

        private static bool SkipJsonValue(string json, ref int index)
        {
            if (index >= json.Length) return false;
            if (json[index] == '"') return SkipJsonString(json, ref index);
            if (json[index] == '{' || json[index] == '[')
            {
                var depth = 0;
                while (index < json.Length)
                {
                    var character = json[index];
                    if (character == '"') { if (!SkipJsonString(json, ref index)) return false; continue; }
                    ++index;
                    if (character == '{' || character == '[') ++depth;
                    if (character == '}' || character == ']')
                    {
                        if (--depth == 0) return true;
                    }
                }
                return false;
            }
            var start = index;
            while (index < json.Length && json[index] != ',' && json[index] != '}') ++index;
            return index > start;
        }

        private static bool SkipJsonString(string json, ref int index)
        {
            if (index >= json.Length || json[index++] != '"') return false;
            while (index < json.Length)
            {
                var character = json[index++];
                if (character == '"') return true;
                if (character == '\\')
                {
                    if (index >= json.Length) return false;
                    ++index;
                }
            }
            return false;
        }
        private static void SkipWhitespace(string json, ref int index)
        { while (index < json.Length && char.IsWhiteSpace(json[index])) ++index; }
        private static string ErrorMessage(string code, long status)
        {
            if (status == 401) return "로그인 후 선물함을 이용할 수 있습니다.";
            if (code == "gift_expired") return "수령 기간이 지난 선물입니다.";
            if (status == 404 || code == "gift_not_found") return "선물을 찾을 수 없습니다.";
            if (code == "gift_wallet_limit") return "보유 한도를 초과하여 선물을 받을 수 없습니다.";
            if (code == "gift_wallet_unavailable") return "계정 재화 정보를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.";
            return "선물 정보를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.";
        }
    }

    public sealed class GiftInboxException : Exception
    {
        public string Code { get; }
        public bool IsRetryable { get; }
        public GiftInboxException(string code, string message, bool isRetryable) : base(message)
        { Code = code; IsRetryable = isRetryable; }
    }

    // An account switch, token replacement or endpoint switch invalidates every earlier response.
    public readonly struct GiftInboxAccountScope
    {
        public readonly string AccountId;
        public readonly string SessionToken;
        public readonly string ServerUrl;
        public GiftInboxAccountScope(string accountId, string sessionToken, string serverUrl)
        { AccountId = accountId; SessionToken = sessionToken; ServerUrl = serverUrl; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccountId) && !string.IsNullOrWhiteSpace(SessionToken);
        public bool Matches(string accountId, string sessionToken, string serverUrl) =>
            IsAuthenticated && string.Equals(AccountId, accountId, StringComparison.Ordinal) &&
            string.Equals(SessionToken, sessionToken, StringComparison.Ordinal) &&
            string.Equals(ServerUrl, serverUrl, StringComparison.Ordinal);
    }

    [Serializable]
    public sealed class GiftInboxResponse
    {
        public GiftInboxItemDto[] Gifts;
        public long UnclaimedCount;
        public string NextCursor;
        public GiftInboxItemDto[] gifts;
        public long unclaimedCount;
        public string nextCursor;
        public GiftInboxItemDto[] ResolvedGifts => Gifts ?? gifts ?? Array.Empty<GiftInboxItemDto>();
        public long ResolvedUnclaimedCount => Math.Max(0, UnclaimedCount != 0 ? UnclaimedCount : unclaimedCount);
        public string ResolvedNextCursor => NextCursor ?? nextCursor;
    }

    [Serializable]
    public sealed class GiftInboxItemDto
    {
        public string GiftId, Title, Message, CreatedAtUtc, ExpiresAtUtc, ClaimedAtUtc, Status;
        public int Tickets;
        public long ResourceGold;
        public string giftId, title, message, createdAtUtc, expiresAtUtc, claimedAtUtc, status;
        public int tickets;
        public long resourceGold;
        public string ResolvedGiftId => GiftId ?? giftId;
        public string ResolvedTitle => Title ?? title ?? string.Empty;
        public string ResolvedMessage => Message ?? message ?? string.Empty;
        public string ResolvedCreatedAtUtc => CreatedAtUtc ?? createdAtUtc;
        public string ResolvedExpiresAtUtc => ExpiresAtUtc ?? expiresAtUtc;
        public string ResolvedClaimedAtUtc => ClaimedAtUtc ?? claimedAtUtc;
        public string ResolvedStatus => Status ?? status ?? string.Empty;
        public int ResolvedTickets => Tickets != 0 ? Tickets : tickets;
        public long ResolvedResourceGold => ResourceGold != 0 ? ResourceGold : resourceGold;
        public bool CanClaim => string.Equals(ResolvedStatus, "unclaimed", StringComparison.OrdinalIgnoreCase);
        public bool IsClaimed => string.Equals(ResolvedStatus, "claimed", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public sealed class GiftClaimResponse
    {
        public string GiftId;
        public bool AlreadyClaimed;
        public WalletDto Wallet;
        public string giftId;
        public bool alreadyClaimed;
        public WalletDto wallet;
        public string ResolvedGiftId => GiftId ?? giftId;
        public bool ResolvedAlreadyClaimed => AlreadyClaimed || alreadyClaimed;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
    }
}
