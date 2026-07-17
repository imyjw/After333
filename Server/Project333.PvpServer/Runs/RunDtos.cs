using Project333.PvpServer.Auth;

namespace Project333.PvpServer.Runs;

public sealed record StartRunRequest(
    string? Mode);

public sealed record CompleteDraftRequest(
    string? RunId,
    IReadOnlyList<string>? CardIds);

public sealed record SaveDraftPicksRequest(
    string? RunId,
    IReadOnlyList<string>? CardIds,
    IReadOnlyList<string>? CurrentOfferCardIds);

public sealed record DraftStateRequest(
    string? RunId);

public sealed record SelectDraftCardRequest(
    string? RunId,
    string? CardId,
    int PickIndex);

public sealed record ClaimRunRewardsRequest(
    string? RunId);

public sealed record SyncLocalRunRecordRequest(
    string? RunId,
    string? DeckId,
    int Wins,
    int Losses);

public sealed record RunSummaryDto(
    Guid Id,
    string Status,
    string Mode,
    int Wins,
    int Losses,
    int TicketCostPaid,
    DateTimeOffset StartedAt,
    DateTimeOffset? RewardClaimedAt);

public sealed record DeckSummaryDto(
    Guid Id,
    string Name,
    string DeckType,
    Guid SourceRunId,
    int CardCount,
    string CardDefinitionVersion);

public sealed record ActiveRunStateDto(
    RunSummaryDto? ActiveRun,
    DeckSummaryDto? ActiveDeck,
    IReadOnlyList<string> ActiveDeckCardIds,
    IReadOnlyList<string> ActiveDraftPickCardIds,
    IReadOnlyList<string> ActiveDraftOfferCardIds,
    RunSummaryDto? LatestRun,
    DeckSummaryDto? LatestDeck,
    IReadOnlyList<string> LatestDeckCardIds,
    IReadOnlyList<string> LatestDraftPickCardIds,
    IReadOnlyList<string> LatestDraftOfferCardIds);

public sealed record StartRunResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    RunSummaryDto ActiveRun,
    IReadOnlyList<string> DraftPickCardIds,
    IReadOnlyList<string> CurrentOfferCardIds);

public sealed record CompleteDraftResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    RunSummaryDto ActiveRun,
    DeckSummaryDto Deck);

public sealed record SaveDraftPicksResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    RunSummaryDto ActiveRun,
    IReadOnlyList<string> DraftPickCardIds,
    IReadOnlyList<string> CurrentOfferCardIds);

public sealed record DraftStateResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    RunSummaryDto ActiveRun,
    IReadOnlyList<string> DraftPickCardIds,
    IReadOnlyList<string> CurrentOfferCardIds);

public sealed record SelectDraftCardResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    RunSummaryDto ActiveRun,
    bool IsComplete,
    IReadOnlyList<string> DraftPickCardIds,
    IReadOnlyList<string> CurrentOfferCardIds,
    DeckSummaryDto? Deck);

public sealed record RewardGrantDto(
    Guid Id,
    string SourceType,
    Guid SourceId,
    long ResourceGoldDelta,
    int TicketDelta,
    IReadOnlyDictionary<string, int> CardRewards,
    IReadOnlyDictionary<string, int> PackRewards,
    IReadOnlyList<RewardItemDto> CardRewardItems,
    IReadOnlyList<RewardItemDto> PackRewardItems,
    DateTimeOffset CreatedAt);

public sealed record RewardItemDto(
    string Id,
    int Count);

public sealed record ClaimRunRewardsResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    RunSummaryDto Run,
    RewardGrantDto Reward);

public sealed record SyncLocalRunRecordResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    RunSummaryDto Run);
