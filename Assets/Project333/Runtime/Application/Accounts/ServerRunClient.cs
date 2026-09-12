using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project333.Runtime.Application.Accounts
{
    public sealed class ServerRunClient
    {
        private readonly string _baseUrl;

        public ServerRunClient(string baseUrl)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? "http://127.0.0.1:7333"
                : baseUrl.TrimEnd('/');
        }

        public async Task<StartRunResponse> StartDraftRunAsync(
            string sessionToken,
            string mode,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            var request = new StartRunRequest
            {
                Mode = string.IsNullOrWhiteSpace(mode) ? "pve" : mode
            };
            var response = await PostJsonAsync<StartRunRequest, StartRunResponse>(
                "/runs/start",
                request,
                sessionToken,
                cancellationToken);
            if (response == null || response.ResolvedActiveRun == null)
            {
                throw new InvalidOperationException("Server did not return a started run.");
            }

            return response;
        }

        public async Task<PurchaseTicketResponse> PurchaseTicketAsync(
            string sessionToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            using var operation = PendingAccountOperation.Begin(_baseUrl, "ticket_purchase");
            var request = new PurchaseTicketRequest { TicketCount = 1, RequestId = operation.RequestId };
            var response = await operation.PostAsync<PurchaseTicketRequest, PurchaseTicketResponse>(
                _baseUrl, "/wallet/purchase-ticket", sessionToken, request, BuildErrorMessage, cancellationToken);
            if (response == null || response.ResolvedWallet == null)
            {
                throw new InvalidOperationException("Server did not return an updated wallet.");
            }
            operation.Complete(response.ResolvedRequestId, response.ResolvedAccount?.ResolvedId);
            return response;
        }

        public async Task<CompleteDraftResponse> CompleteDraftAsync(
            string sessionToken,
            string runId,
            IReadOnlyList<string> cardIds,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new InvalidOperationException("Server draft run id is missing.");
            }

            if (cardIds == null || cardIds.Count != 33)
            {
                throw new InvalidOperationException("Completed draft deck must contain exactly 33 cards.");
            }

            var request = new CompleteDraftRequest
            {
                RunId = runId,
                CardIds = ToArray(cardIds)
            };
            var response = await PostJsonAsync<CompleteDraftRequest, CompleteDraftResponse>(
                "/runs/complete-draft",
                request,
                sessionToken,
                cancellationToken);
            if (response == null || response.ResolvedDeck == null)
            {
                throw new InvalidOperationException("Server did not return a saved draft deck.");
            }

            return response;
        }

        public async Task<SaveDraftPicksResponse> SaveDraftPicksAsync(
            string sessionToken,
            string runId,
            IReadOnlyList<string> cardIds,
            IReadOnlyList<string> currentOfferCardIds,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new InvalidOperationException("Server draft run id is missing.");
            }

            if (cardIds == null || cardIds.Count >= 33)
            {
                throw new InvalidOperationException("In-progress draft picks must contain fewer than 33 cards.");
            }

            var request = new SaveDraftPicksRequest
            {
                RunId = runId,
                CardIds = ToArray(cardIds),
                CurrentOfferCardIds = ToArray(currentOfferCardIds ?? Array.Empty<string>())
            };
            var response = await PostJsonAsync<SaveDraftPicksRequest, SaveDraftPicksResponse>(
                "/runs/save-draft-picks",
                request,
                sessionToken,
                cancellationToken);
            if (response == null || response.ResolvedActiveRun == null)
            {
                throw new InvalidOperationException("Server did not return saved draft picks.");
            }

            return response;
        }

        public async Task<DraftStateResponse> GetDraftStateAsync(
            string sessionToken,
            string runId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new InvalidOperationException("Server draft run id is missing.");
            }

            var response = await PostJsonAsync<DraftStateRequest, DraftStateResponse>(
                "/runs/draft-state",
                new DraftStateRequest { RunId = runId },
                sessionToken,
                cancellationToken);
            if (response == null || response.ResolvedActiveRun == null)
            {
                throw new InvalidOperationException("Server did not return an authoritative draft state.");
            }

            return response;
        }

        public async Task<SelectDraftCardResponse> SelectDraftCardAsync(
            string sessionToken,
            string runId,
            string cardId,
            int pickIndex,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new InvalidOperationException("Server draft run id is missing.");
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new InvalidOperationException("Selected draft card id is missing.");
            }

            if (pickIndex < 0 || pickIndex >= 33)
            {
                throw new InvalidOperationException("Draft pick index must be between 0 and 32.");
            }

            var request = new SelectDraftCardRequest
            {
                RunId = runId,
                CardId = cardId,
                PickIndex = pickIndex
            };
            var response = await PostJsonAsync<SelectDraftCardRequest, SelectDraftCardResponse>(
                "/runs/select-draft-card",
                request,
                sessionToken,
                cancellationToken);
            if (response == null || response.ResolvedActiveRun == null)
            {
                throw new InvalidOperationException("Server did not return the advanced draft state.");
            }

            return response;
        }

        public async Task<ClaimRunRewardsResponse> ClaimRunRewardsAsync(
            string sessionToken,
            string runId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new InvalidOperationException("Completed server draft run id is missing.");
            }

            var request = new ClaimRunRewardsRequest
            {
                RunId = runId
            };
            var response = await PostJsonAsync<ClaimRunRewardsRequest, ClaimRunRewardsResponse>(
                "/runs/claim-rewards",
                request,
                sessionToken,
                cancellationToken);
            if (response == null || response.ResolvedRun == null)
            {
                throw new InvalidOperationException("Server did not return a claimed reward run.");
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
            await AccountHttpTransport.SendAsync(webRequest, cancellationToken);

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
                        if (resolvedError.ResolvedCode == "insufficient_tickets")
                        {
                            return "서버 티켓이 부족합니다.";
                        }

                        if (resolvedError.ResolvedCode == "insufficient_resource_gold")
                        {
                            return "서버 골드가 부족합니다.";
                        }

                        if (resolvedError.ResolvedCode == "reward_already_claimed")
                        {
                            return "이미 보상을 받았습니다.";
                        }

                        if (resolvedError.ResolvedCode == "run_not_completed")
                        {
                            return "런이 아직 완료되지 않았습니다.";
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

        private static string[] ToArray(IReadOnlyList<string> cardIds)
        {
            var result = new string[cardIds.Count];
            for (var i = 0; i < cardIds.Count; i++)
            {
                result[i] = cardIds[i];
            }

            return result;
        }
    }

    [Serializable]
    public sealed class StartRunRequest
    {
        public string Mode;
    }

    [Serializable]
    public sealed class CompleteDraftRequest
    {
        public string RunId;
        public string[] CardIds;
    }

    [Serializable]
    public sealed class SaveDraftPicksRequest
    {
        public string RunId;
        public string[] CardIds;
        public string[] CurrentOfferCardIds;
    }

    [Serializable]
    public sealed class DraftStateRequest
    {
        public string RunId;
    }

    [Serializable]
    public sealed class SelectDraftCardRequest
    {
        public string RunId;
        public string CardId;
        public int PickIndex;
    }

    [Serializable]
    public sealed class ClaimRunRewardsRequest
    {
        public string RunId;
    }

    [Serializable]
    public sealed class PurchaseTicketRequest
    {
        public int TicketCount;
        public string RequestId;
    }

    [Serializable]
    public sealed class StartRunResponse
    {
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public RunSummaryDto ActiveRun;
        public string[] DraftPickCardIds;
        public string[] CurrentOfferCardIds;

        public AuthAccountDto account;
        public WalletDto wallet;
        public RunSummaryDto activeRun;
        public string[] draftPickCardIds;
        public string[] currentOfferCardIds;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public RunSummaryDto ResolvedActiveRun => ActiveRun ?? activeRun;
        public IReadOnlyList<string> ResolvedDraftPickCardIds => DraftPickCardIds ?? draftPickCardIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResolvedCurrentOfferCardIds => CurrentOfferCardIds ?? currentOfferCardIds ?? Array.Empty<string>();
    }

    [Serializable]
    public sealed class PurchaseTicketResponse
    {
        public string RequestId;
        public string requestId;
        public bool Replayed;
        public bool replayed;
        public string ResolvedRequestId => RequestId ?? requestId;
        public bool ResolvedReplayed => Replayed || replayed;
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public int TicketCount;
        public long ResourceGoldCost;

        public AuthAccountDto account;
        public WalletDto wallet;
        public int ticketCount;
        public long resourceGoldCost;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public int ResolvedTicketCount => TicketCount != 0 ? TicketCount : ticketCount;
        public long ResolvedResourceGoldCost => ResourceGoldCost != 0 ? ResourceGoldCost : resourceGoldCost;
    }

    [Serializable]
    public sealed class CompleteDraftResponse
    {
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public RunSummaryDto ActiveRun;
        public DeckSummaryDto Deck;

        public AuthAccountDto account;
        public WalletDto wallet;
        public RunSummaryDto activeRun;
        public DeckSummaryDto deck;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public RunSummaryDto ResolvedActiveRun => ActiveRun ?? activeRun;
        public DeckSummaryDto ResolvedDeck => Deck ?? deck;
    }

    [Serializable]
    public sealed class SaveDraftPicksResponse
    {
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public RunSummaryDto ActiveRun;
        public string[] DraftPickCardIds;
        public string[] CurrentOfferCardIds;

        public AuthAccountDto account;
        public WalletDto wallet;
        public RunSummaryDto activeRun;
        public string[] draftPickCardIds;
        public string[] currentOfferCardIds;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public RunSummaryDto ResolvedActiveRun => ActiveRun ?? activeRun;
        public IReadOnlyList<string> ResolvedDraftPickCardIds => DraftPickCardIds ?? draftPickCardIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResolvedCurrentOfferCardIds => CurrentOfferCardIds ?? currentOfferCardIds ?? Array.Empty<string>();
    }

    [Serializable]
    public sealed class DraftStateResponse
    {
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public RunSummaryDto ActiveRun;
        public string[] DraftPickCardIds;
        public string[] CurrentOfferCardIds;

        public AuthAccountDto account;
        public WalletDto wallet;
        public RunSummaryDto activeRun;
        public string[] draftPickCardIds;
        public string[] currentOfferCardIds;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public RunSummaryDto ResolvedActiveRun => ActiveRun ?? activeRun;
        public IReadOnlyList<string> ResolvedDraftPickCardIds => DraftPickCardIds ?? draftPickCardIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResolvedCurrentOfferCardIds => CurrentOfferCardIds ?? currentOfferCardIds ?? Array.Empty<string>();
    }

    [Serializable]
    public sealed class SelectDraftCardResponse
    {
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public RunSummaryDto ActiveRun;
        public bool IsComplete;
        public string[] DraftPickCardIds;
        public string[] CurrentOfferCardIds;
        public DeckSummaryDto Deck;

        public AuthAccountDto account;
        public WalletDto wallet;
        public RunSummaryDto activeRun;
        public bool isComplete;
        public string[] draftPickCardIds;
        public string[] currentOfferCardIds;
        public DeckSummaryDto deck;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public RunSummaryDto ResolvedActiveRun => ActiveRun ?? activeRun;
        public bool ResolvedIsComplete => IsComplete || isComplete;
        public IReadOnlyList<string> ResolvedDraftPickCardIds => DraftPickCardIds ?? draftPickCardIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResolvedCurrentOfferCardIds => CurrentOfferCardIds ?? currentOfferCardIds ?? Array.Empty<string>();
        public DeckSummaryDto ResolvedDeck => Deck ?? deck;
    }

    [Serializable]
    public sealed class ClaimRunRewardsResponse
    {
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public RunSummaryDto Run;
        public RewardGrantDto Reward;

        public AuthAccountDto account;
        public WalletDto wallet;
        public RunSummaryDto run;
        public RewardGrantDto reward;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public RunSummaryDto ResolvedRun => Run ?? run;
        public RewardGrantDto ResolvedReward => Reward ?? reward;
    }

    [Serializable]
    public sealed class RewardGrantDto
    {
        public string Id;
        public string SourceType;
        public string SourceId;
        public long ResourceGoldDelta;
        public int TicketDelta;
        public RewardItemDto[] CardRewardItems;
        public RewardItemDto[] PackRewardItems;
        public string CreatedAt;

        public string id;
        public string sourceType;
        public string sourceId;
        public long resourceGoldDelta;
        public int ticketDelta;
        public RewardItemDto[] cardRewardItems;
        public RewardItemDto[] packRewardItems;
        public string createdAt;

        public string ResolvedId => string.IsNullOrWhiteSpace(Id) ? id : Id;
        public string ResolvedSourceType => string.IsNullOrWhiteSpace(SourceType) ? sourceType : SourceType;
        public string ResolvedSourceId => string.IsNullOrWhiteSpace(SourceId) ? sourceId : SourceId;
        public long ResolvedResourceGoldDelta => ResourceGoldDelta != 0 ? ResourceGoldDelta : resourceGoldDelta;
        public int ResolvedTicketDelta => TicketDelta != 0 ? TicketDelta : ticketDelta;
        public RewardItemDto[] ResolvedCardRewardItems => ResolveRewardItems(CardRewardItems, cardRewardItems);
        public RewardItemDto[] ResolvedPackRewardItems => ResolveRewardItems(PackRewardItems, packRewardItems);
        public string ResolvedCreatedAt => string.IsNullOrWhiteSpace(CreatedAt) ? createdAt : CreatedAt;

        private static RewardItemDto[] ResolveRewardItems(RewardItemDto[] pascalItems, RewardItemDto[] camelItems)
        {
            if (pascalItems != null && pascalItems.Length > 0)
            {
                return pascalItems;
            }

            return camelItems != null && camelItems.Length > 0
                ? camelItems
                : Array.Empty<RewardItemDto>();
        }
    }

    [Serializable]
    public sealed class RewardItemDto
    {
        public string Id;
        public int Count;

        public string id;
        public int count;

        public string ResolvedId => string.IsNullOrWhiteSpace(Id) ? id : Id;
        public int ResolvedCount => Count != 0 ? Count : count;
    }

    [Serializable]
    public sealed class RunSummaryDto
    {
        public string Id;
        public string Status;
        public string Mode;
        public int Wins;
        public int Losses;
        public int TicketCostPaid;
        public string StartedAt;
        public string RewardClaimedAt;

        public string id;
        public string status;
        public string mode;
        public int wins;
        public int losses;
        public int ticketCostPaid;
        public string startedAt;
        public string rewardClaimedAt;

        public string ResolvedId => string.IsNullOrWhiteSpace(Id) ? id : Id;
        public string ResolvedStatus => string.IsNullOrWhiteSpace(Status) ? status : Status;
        public string ResolvedMode => string.IsNullOrWhiteSpace(Mode) ? mode : Mode;
        public int ResolvedWins => Wins != 0 ? Wins : wins;
        public int ResolvedLosses => Losses != 0 ? Losses : losses;
        public int ResolvedTicketCostPaid => TicketCostPaid != 0 ? TicketCostPaid : ticketCostPaid;
        public string ResolvedStartedAt => string.IsNullOrWhiteSpace(StartedAt) ? startedAt : StartedAt;
        public string ResolvedRewardClaimedAt => string.IsNullOrWhiteSpace(RewardClaimedAt) ? rewardClaimedAt : RewardClaimedAt;
    }

    [Serializable]
    public sealed class DeckSummaryDto
    {
        public string Id;
        public string Name;
        public string DeckType;
        public string SourceRunId;
        public int CardCount;
        public string CardDefinitionVersion;

        public string id;
        public string name;
        public string deckType;
        public string sourceRunId;
        public int cardCount;
        public string cardDefinitionVersion;

        public string ResolvedId => string.IsNullOrWhiteSpace(Id) ? id : Id;
        public string ResolvedName => string.IsNullOrWhiteSpace(Name) ? name : Name;
        public string ResolvedDeckType => string.IsNullOrWhiteSpace(DeckType) ? deckType : DeckType;
        public string ResolvedSourceRunId => string.IsNullOrWhiteSpace(SourceRunId) ? sourceRunId : SourceRunId;
        public int ResolvedCardCount => CardCount != 0 ? CardCount : cardCount;
        public string ResolvedCardDefinitionVersion => string.IsNullOrWhiteSpace(CardDefinitionVersion) ? cardDefinitionVersion : CardDefinitionVersion;
    }

    [Serializable]
    public sealed class ApiErrorEnvelope
    {
        public ApiErrorDto Error;
        public ApiErrorDto error;

        public ApiErrorDto ResolvedError => Error ?? error;
    }

    [Serializable]
    public sealed class ApiErrorDto
    {
        public string Code;
        public string Message;

        public string code;
        public string message;

        public string ResolvedCode => string.IsNullOrWhiteSpace(Code) ? code : Code;
        public string ResolvedMessage => string.IsNullOrWhiteSpace(Message) ? message : Message;
    }
}
