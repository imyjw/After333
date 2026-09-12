using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project333.Runtime.Application.Accounts
{
    // Non-secret intent metadata only. Persist before sending, keep on uncertain outcome,
    // and scope to server/account/card so relogin and scene changes reuse the same request.
    public sealed class PendingAccountOperation : IDisposable
    {
        private static readonly HashSet<string> InFlight = new HashSet<string>();
        private readonly string _key;
        private readonly string _accountId;
        private readonly SavedIntent _intent;
        public string RequestId => _intent.RequestId;
        public int ExpectedUpgradeLevel => _intent.ExpectedUpgradeLevel;

        [Serializable]
        private sealed class SavedIntent
        {
            public string RequestId;
            public int ExpectedUpgradeLevel;
        }

        private PendingAccountOperation(string key, string accountId, SavedIntent intent)
        {
            _key = key;
            _accountId = accountId;
            _intent = intent;
        }

        public static bool HasPending(string baseUrl, string operation, string cardId = null) =>
            !string.IsNullOrWhiteSpace(AccountSessionState.AccountId) &&
            PlayerPrefs.HasKey(Key(baseUrl, AccountSessionState.AccountId, operation, cardId));


        [Serializable] private sealed class PendingIndex { public List<string> Cards = new List<string>(); }
        public static bool HasAnyPending(string baseUrl)
        {
            foreach (var card in PendingCards(baseUrl))
                if (HasPending(baseUrl, card == "" ? "ticket_purchase" : "card_upgrade", card)) return true;
            return false;
        }
        public static IEnumerable<string> PendingCards(string baseUrl)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "" };
            var key = Key(baseUrl, AccountSessionState.AccountId, "index", null);
            if (PlayerPrefs.HasKey(key))
            {
                var index = JsonUtility.FromJson<PendingIndex>(PlayerPrefs.GetString(key));
                if (index?.Cards != null) foreach (var card in index.Cards) result.Add(card);
            }
            // Previous clients saved per-card requests without an index.
            foreach (var card in AccountSessionState.OwnedCardIds) result.Add(card);
            return result;
        }
        private static void Register(string baseUrl, string cardId)
        {
            var key = Key(baseUrl, AccountSessionState.AccountId, "index", null);
            var index = PlayerPrefs.HasKey(key) ? JsonUtility.FromJson<PendingIndex>(PlayerPrefs.GetString(key)) : new PendingIndex();
            if (index == null) index = new PendingIndex();
            if (index.Cards == null) index.Cards = new List<string>();
            if (!index.Cards.Contains(cardId ?? "")) index.Cards.Add(cardId ?? "");
            PlayerPrefs.SetString(key, JsonUtility.ToJson(index));
            PlayerPrefs.Save();
        }
        public static bool IsInFlight(string baseUrl, string operation, string cardId) =>
            InFlight.Contains(Key(baseUrl, AccountSessionState.AccountId, operation, cardId));

        public static PendingAccountOperation Begin(string baseUrl, string operation, string cardId = null, int expectedLevel = 0)
        {
            var accountId = AccountSessionState.AccountId;
            if (string.IsNullOrWhiteSpace(accountId)) throw new InvalidOperationException("서버 계정 정보가 필요합니다. 다시 로그인해주세요.");
            Register(baseUrl, cardId);
            var key = Key(baseUrl, accountId, operation, cardId);
            if (!InFlight.Add(key)) throw new InvalidOperationException("이미 처리 중인 요청입니다.");
            try
            {
                SavedIntent intent;
                if (PlayerPrefs.HasKey(key))
                {
                    intent = JsonUtility.FromJson<SavedIntent>(PlayerPrefs.GetString(key));
                    if (intent == null || !Guid.TryParseExact(intent.RequestId, "D", out var id) || id == Guid.Empty)
                        throw new InvalidOperationException("이전 요청 정보를 확인할 수 없습니다. 추가 차감을 방지하기 위해 처리를 중단했습니다.");
                }
                else
                {
                    intent = new SavedIntent { RequestId = Guid.NewGuid().ToString("D"), ExpectedUpgradeLevel = expectedLevel };
                    PlayerPrefs.SetString(key, JsonUtility.ToJson(intent));
                    PlayerPrefs.Save();
                }
                return new PendingAccountOperation(key, accountId, intent);
            }
            catch { InFlight.Remove(key); throw; }
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string baseUrl, string path, string sessionToken,
            TRequest payload, Func<UnityWebRequest, string> describeError, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            using var request = new UnityWebRequest(baseUrl.TrimEnd('/') + path, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + sessionToken);
            await AccountHttpTransport.SendAsync(request, ct);
            if (request.result != UnityWebRequest.Result.Success)
            {
                string code = null;
                try { code = JsonUtility.FromJson<ApiErrorEnvelope>(request.downloadHandler?.text)?.ResolvedError?.ResolvedCode; }
                catch { /* An unreadable error is an uncertain outcome. Keep the intent. */ }
                // Only documented pre-commit business rejections are definitive. Never
                // discard on transport errors, cancellation, authentication failure or 5xx.
                if ((request.responseCode == 400 || request.responseCode == 404 || request.responseCode == 409) && IsDefinitiveRejection(code))
                    Clear();
                throw new InvalidOperationException(describeError(request));
            }
            var body = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("이전 요청의 응답을 받지 못했습니다. 다시 눌러 결과를 확인해주세요.");
            return JsonUtility.FromJson<TResponse>(body);
        }

        public void Complete(string responseRequestId, string responseAccountId)
        {
            if (!Guid.TryParse(responseRequestId, out var requestId) || requestId.ToString("D") != RequestId ||
                !string.Equals(responseAccountId, _accountId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(AccountSessionState.AccountId, _accountId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("서버의 요청 처리 결과를 확인하지 못했습니다. 다시 로그인한 뒤 결과를 확인해주세요.");
            Clear();
        }

        private void Clear()
        {
            PlayerPrefs.DeleteKey(_key);
            PlayerPrefs.Save();
        }

        public void Dispose() => InFlight.Remove(_key);

        private static bool IsDefinitiveRejection(string code)
        {
            switch (code)
            {
                case "operation_cancelled":
                case "invalid_ticket_count":
                case "invalid_card_id":
                case "invalid_expected_upgrade_level":
                case "card_definition_not_found":
                case "card_not_upgradeable":
                case "card_not_owned":
                case "max_level_reached":
                case "insufficient_card_copies":
                case "insufficient_resource_gold":
                case "card_upgrade_conflict":
                    return true;
                default: return false;
            }
        }

        private static string Key(string baseUrl, string accountId, string operation, string cardId)
        {
            using var hash = SHA256.Create();
            var scope = baseUrl.TrimEnd('/') + "\n" + accountId.ToLowerInvariant() + "\n" + operation + "\n" + (cardId ?? "").Trim().ToLowerInvariant();
            return "Project333.PendingAccountOperation." + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(scope))).Replace("-", "");
        }
    }
}
