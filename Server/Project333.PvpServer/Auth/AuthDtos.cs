using Project333.PvpServer.Runs;

namespace Project333.PvpServer.Auth;

public sealed record GuestAuthRequest(
    string? GuestToken,
    string? ClientVersion);

public sealed record GameIdAuthRequest(
    string? GameId,
    string? Password,
    string? DisplayName,
    string? ClientVersion);

public sealed record LinkGameIdRequest(
    string? GameId,
    string? Password,
    string? DisplayName);

public sealed record GoogleAuthRequest(
    string? IdToken,
    string? ClientVersion);

public sealed record GoogleDesktopCodeExchangeRequest(
    string? ClientId,
    string? AuthorizationCode,
    string? RedirectUri,
    string? CodeVerifier);

public sealed record GoogleDesktopCodeExchangeResponse(
    string IdToken);

public sealed record LinkGoogleRequest(
    string? IdToken);

public sealed record RefreshAuthRequest(
    string? RefreshToken,
    string? ClientVersion);

public sealed record LogoutAuthRequest(
    string? RefreshToken);

public sealed record AuthAccountDto(
    Guid Id,
    string DisplayName,
    string AccountKind);

public sealed record WalletDto(
    long ResourceGold,
    int Tickets);

public sealed record CollectionSummaryDto(
    int OwnedCardKinds,
    IReadOnlyList<OwnedCardDto> OwnedCards);

public sealed record OwnedCardDto(
    string CardId,
    int CopyCount,
    int UpgradeLevel);

public sealed record GuestAuthResponse(
    string SessionToken,
    string RefreshToken,
    string GuestToken,
    AuthAccountDto Account,
    WalletDto Wallet);

public sealed record GameIdAuthResponse(
    string SessionToken,
    string RefreshToken,
    AuthAccountDto Account,
    WalletDto Wallet);

public sealed record LinkGameIdResponse(
    AuthAccountDto Account,
    WalletDto Wallet);

public sealed record GoogleAuthResponse(
    string SessionToken,
    string RefreshToken,
    AuthAccountDto Account,
    WalletDto Wallet);

public sealed record RefreshAuthResponse(
    string SessionToken,
    string RefreshToken,
    AuthAccountDto Account,
    WalletDto Wallet);

public sealed record LogoutAuthResponse(
    bool LoggedOut);

public sealed record LinkGoogleResponse(
    AuthAccountDto Account,
    WalletDto Wallet);

public sealed record PurchaseTicketRequest(
    int TicketCount, string? RequestId = null);

public sealed record PurchaseTicketResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    int TicketCount,
    long ResourceGoldCost, string RequestId, bool Replayed = false);

public sealed record MeResponse(
    AuthAccountDto Account,
    WalletDto Wallet,
    CollectionSummaryDto CollectionSummary,
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

public sealed record AuthenticatedAccount(
    AuthAccountDto Account,
    WalletDto Wallet,
    CollectionSummaryDto CollectionSummary);

public sealed record PvpReconnectStatusResponse(
    bool HasReconnectableBattle,
    string? MatchId,
    string? SeatId,
    string? OnlineSeatId,
    int RemainingSeconds,
    string? ReconnectDeadlineUtc);
