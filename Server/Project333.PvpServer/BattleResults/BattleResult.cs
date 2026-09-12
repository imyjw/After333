using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Online;

namespace Project333.PvpServer.BattleResults;

// Created by BattleSession only; this is never accepted from a client endpoint.
public sealed record BattleResult(
    Guid ResultId,
    string MatchId,
    bool UseServerAiOpponent,
    bool IsDraw,
    OnlineBattleSeatId WinnerSeat,
    string EndedReason,
    IReadOnlyList<BattleRunResultRecord> Runs)
{
    public BattleResult ValidatedCopy()
    {
        if (ResultId == Guid.Empty || string.IsNullOrWhiteSpace(MatchId) ||
            EndedReason is not ("normal" or "forfeit" or "reconnect_timeout") ||
            (IsDraw ? WinnerSeat != OnlineBattleSeatId.None :
                WinnerSeat != OnlineBattleSeatId.PlayerA &&
                WinnerSeat != (UseServerAiOpponent ? OnlineBattleSeatId.ServerAI : OnlineBattleSeatId.PlayerB)) ||
            Runs == null || Runs.Count > (UseServerAiOpponent ? 1 : 2))
            throw new InvalidOperationException("Invalid server battle result.");
        var runs = Runs.OrderBy(r => r.SeatId).ToArray();
        if (runs.Select(r => r.SeatId).Distinct().Count() != runs.Length ||
            runs.Select(r => r.AccountId).Distinct(StringComparer.Ordinal).Count() != runs.Length ||
            runs.Select(r => r.RunId).Distinct(StringComparer.Ordinal).Count() != runs.Length)
            throw new InvalidOperationException("Duplicate battle result participant.");
        foreach (var run in runs)
        {
            if (!Guid.TryParse(run.AccountId, out var account) || account == Guid.Empty ||
                !Guid.TryParse(run.RunId, out var runId) || runId == Guid.Empty ||
                !Guid.TryParse(run.DeckId, out var deck) || deck == Guid.Empty ||
                run.SeatId is not (PlayerIdDto.Player or PlayerIdDto.AI) ||
                run.OnlineSeatId != (run.SeatId == PlayerIdDto.Player ? OnlineBattleSeatId.PlayerA : OnlineBattleSeatId.PlayerB) ||
                (UseServerAiOpponent && run.SeatId != PlayerIdDto.Player) ||
                run.Won != (!IsDraw && run.OnlineSeatId == WinnerSeat))
                throw new InvalidOperationException("Invalid server result participant binding.");
        }
        return this with { Runs = Array.AsReadOnly(runs) };
    }

    public string ToJson() => JsonSerializer.Serialize(ValidatedCopy());
}
