namespace Project333.PvpServer.Challenges;

public sealed record AiChallengeStartRequest(Guid RequestId, string? RunId, string? DeckId);

public sealed record AiChallengeAttemptDto(
    Guid AttemptId, string MatchId, Guid RunId, Guid DeckId, string Status,
    int RewardGold = 0, Guid? ResultId = null, bool? Won = null);

public sealed record AiChallengeStartResponse(
    Guid AttemptId, string MatchId, Guid RunId, Guid DeckId, string Status,
    int RemainingAttempts, DateTimeOffset ResetsAtUtc, bool Replayed);

public sealed record AiChallengeStatusResponse(
    int DailyLimit, int RemainingAttempts, DateTimeOffset ResetsAtUtc,
    AiChallengeAttemptDto? PendingAttempt);
