using Project333.PvpServer.Auth;

namespace Project333.PvpServer.Gifts;

public sealed record GiftSendRequest(
    Guid RequestId,
    string Title,
    string Message,
    int Tickets,
    long ResourceGold,
    bool AllExistingAccounts,
    IReadOnlyList<Guid>? AccountIds,
    DateTimeOffset? ExpiresAtUtc,
    string Reason);

public sealed record GiftAccountDto(Guid AccountId, string DisplayName, string AccountStatus, bool IsBanned);

public sealed record GiftSendResult(
    Guid GiftId,
    bool AlreadySent,
    long RecipientCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record GiftPreviewResult(
    Guid RequestId,
    long RecipientCount,
    bool AlreadySent,
    IReadOnlyList<GiftAccountDto> Accounts,
    bool AccountsTruncated);

public sealed record GiftInboxItemDto(
    string GiftId,
    string Title,
    string Message,
    int Tickets,
    long ResourceGold,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? ClaimedAtUtc,
    string Status);

public sealed record GiftInboxResponse(
    IReadOnlyList<GiftInboxItemDto> Gifts,
    long UnclaimedCount,
    string? NextCursor);

public sealed record GiftClaimResponse(string GiftId, bool AlreadyClaimed, WalletDto Wallet);

public sealed class GiftInboxException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public GiftInboxException(string code, string message, int statusCode = 400) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}
