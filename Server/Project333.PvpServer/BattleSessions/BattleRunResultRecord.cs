using Project333.Runtime.Application.Online;
using Project333.PvpServer.Messages;

namespace Project333.PvpServer.BattleSessions;

public sealed record BattleRunResultRecord(
    PlayerIdDto SeatId,
    OnlineBattleSeatId OnlineSeatId,
    string AccountId,
    string RunId,
    string DeckId,
    bool Won);
