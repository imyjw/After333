using Project333.Runtime.Application.Online;

namespace Project333.PvpServer.Messages;

public sealed class OnlineBattleEnvelope
{
    public OnlineBattleMessageType MessageType { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public string PlayerToken { get; set; } = string.Empty;

    public string SessionToken { get; set; } = string.Empty;

    public string AccountId { get; set; } = string.Empty;

    public List<string> PlayerDeckCardIds { get; set; } = new();

    public string RunId { get; set; } = string.Empty;

    public string DeckId { get; set; } = string.Empty;

    public bool UseServerAiOpponent { get; set; } = true;

    public bool UseMatchmakingQueue { get; set; }

    public bool IsReconnectAttempt { get; set; }

    public string PreviousConnectionId { get; set; } = string.Empty;

    public string ConnectionId { get; set; } = string.Empty;

    public int ConnectionCount { get; set; }

    public bool HasAssignedSeat { get; set; }

    public PlayerIdDto AssignedSeatId { get; set; }

    public OnlineBattleSeatId AssignedOnlineSeatId { get; set; }

    public ClientBattleCommandMessage? ClientCommand { get; set; }

    public BattleStateViewDto? StateView { get; set; }

    public List<BattleEventDto> BattleEvents { get; set; } = new();

    public OnlineBattleErrorDto? Error { get; set; }
}
