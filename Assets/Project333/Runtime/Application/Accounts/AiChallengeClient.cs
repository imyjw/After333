using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Project333.Runtime.Application.Accounts
{
    [Serializable]
    public sealed class AiChallengeAttemptResponse
    {
        public string AttemptId;
        public string MatchId;
        public string RunId;
        public string DeckId;
        public string Status;
        public int RemainingAttempts;
        public string ResetsAtUtc;
        public bool Replayed;
        public int RewardGold;
        public bool? Won;
        public bool IsFinished => string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(Status, "canceled", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(Status, "cancelled", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public sealed class AiChallengeStatusResponse
    {
        public int DailyLimit = 3;
        public int RemainingAttempts;
        public string ResetsAtUtc;
        public AiChallengeAttemptResponse PendingAttempt;
    }

    public sealed class AiChallengeClient
    {
        [Serializable]
        private sealed class StartRequest
        {
            public string RequestId;
            public string RunId;
            public string DeckId;
        }

        private readonly string _baseUrl;
        public AiChallengeClient(string baseUrl) => _baseUrl = baseUrl.TrimEnd('/');

        public Task<AiChallengeStatusResponse> GetStatusAsync(string token, CancellationToken ct) =>
            SendAsync<AiChallengeStatusResponse>("/ai-challenge/status", token, null, ct);

        public Task<AiChallengeAttemptResponse> GetAttemptAsync(string token, string attemptId, CancellationToken ct) =>
            SendAsync<AiChallengeAttemptResponse>("/ai-challenge/attempts/" + Uri.EscapeDataString(attemptId), token, null, ct);

        public async Task<AiChallengeAttemptResponse> StartAsync(string token, string runId, string deckId, CancellationToken ct)
        {
            var accountId = AccountSessionState.AccountId;
            if (string.IsNullOrWhiteSpace(accountId)) throw new InvalidOperationException("서버 로그인이 필요합니다.");
            var key = PendingKey(accountId);
            var intent = PlayerPrefs.HasKey(key)
                ? JsonConvert.DeserializeObject<StartRequest>(PlayerPrefs.GetString(key)) : null;
            if (intent == null)
            {
                intent = new StartRequest { RequestId = Guid.NewGuid().ToString("D"), RunId = runId, DeckId = deckId };
                PlayerPrefs.SetString(key, JsonConvert.SerializeObject(intent));
                PlayerPrefs.Save();
            }

            // Preserve the exact request across uncertain responses, scene changes and app restarts.
            var response = await SendAsync<AiChallengeAttemptResponse>(
                "/ai-challenge/start", token, JsonConvert.SerializeObject(intent), ct,
                () => { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); });
            if (response == null || string.IsNullOrWhiteSpace(response.AttemptId) ||
                string.IsNullOrWhiteSpace(response.MatchId))
                throw new InvalidOperationException("AI 도전 응답을 확인하지 못했습니다.");
            if (!string.Equals(accountId, AccountSessionState.AccountId, StringComparison.OrdinalIgnoreCase))
                throw new OperationCanceledException("The signed-in account changed.");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return response;
        }

        public void AcknowledgePendingAttempt()
        {
            PlayerPrefs.DeleteKey(PendingKey(AccountSessionState.AccountId));
            PlayerPrefs.Save();
        }

        private string PendingKey(string accountId)
        {
            using var hash = SHA256.Create();
            return "Project333.PendingAiChallenge." + BitConverter.ToString(hash.ComputeHash(
                Encoding.UTF8.GetBytes(_baseUrl + "\n" + accountId))).Replace("-", string.Empty);
        }

        private async Task<T> SendAsync<T>(string path, string token, string body, CancellationToken ct,
            Action definitiveRejection = null)
        {
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("서버 로그인이 필요합니다.");
            using var request = new UnityWebRequest(_baseUrl + path,
                body == null ? UnityWebRequest.kHttpVerbGET : UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            await AccountHttpTransport.SendAsync(request, ct);
            if (request.result != UnityWebRequest.Result.Success)
            {
                ApiErrorEnvelope error = null;
                try { error = JsonUtility.FromJson<ApiErrorEnvelope>(request.downloadHandler.text); }
                catch { }
                var code = error?.ResolvedError?.ResolvedCode;
                if (request.responseCode >= 400 && request.responseCode < 500 &&
                    (code == "ai_challenge_daily_limit" || code == "ai_challenge_limit_reached" ||
                     code == "active_run_not_found" || code == "run_not_found" ||
                     code == "run_not_active" || code == "deck_not_found" ||
                     code == "draft_not_complete" || code == "ai_challenge_deck_invalid" ||
                     code == "ai_challenge_request_invalid" || code == "ai_challenge_request_conflict" ||
                     code == "battle_already_active" || code == "battle_reconnect_required" ||
                     code == "ai_challenge_pending" || code == "ai_challenge_not_available"))
                    definitiveRejection?.Invoke();
                throw new InvalidOperationException(error?.ResolvedError?.ResolvedMessage ??
                    "서버에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요.");
            }
            return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
        }
    }
}
