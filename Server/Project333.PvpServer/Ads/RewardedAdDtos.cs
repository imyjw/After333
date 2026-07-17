using Project333.PvpServer.Auth;

namespace Project333.PvpServer.Ads;

public sealed record CreateRewardedAdAttemptRequest(
    string? ProviderUserId);

public sealed record RewardedAdAttemptResponse(
    bool CanShow,
    Guid? AttemptId,
    string? DynamicUserId,
    string Provider,
    string Placement,
    string Status,
    string? ReasonCode,
    string Message,
    int RewardTicketCount,
    int RemainingDailyRewards,
    int CooldownSeconds,
    string? ExpiresAtUtc,
    WalletDto Wallet);

public sealed record LevelPlayRewardCallbackRequest(
    string? EventId,
    string? UserId,
    string? DynamicUserId,
    string? Rewards,
    string? Timestamp,
    string? Signature,
    string? Placement,
    string? AppKey);

public sealed record RewardedAdCallbackResult(
    string EventId,
    bool RewardGranted,
    Guid? AccountId,
    int TicketCount,
    string ResultCode);

public sealed class RewardedAdServiceException : Exception
{
    public RewardedAdServiceException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
