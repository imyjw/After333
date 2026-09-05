using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using Project333.Runtime.Application.Online;
using Project333.PvpServer.Messages;

namespace Project333.PvpServer.BattleSessions;

public sealed class BattleClientConnection
{
    private long _lastClientMessageUtcTicks = DateTime.UtcNow.Ticks;
    private int _isSuperseded;

    public BattleClientConnection(string connectionId, WebSocket socket)
    {
        ConnectionId = connectionId;
        Socket = socket;
    }

    public string ConnectionId { get; }

    public WebSocket Socket { get; }

    public string MatchId { get; set; } = string.Empty;

    public string PlayerToken { get; set; } = string.Empty;

    public string AccountId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string AccountKind { get; set; } = string.Empty;

    public List<string> PlayerDeckCardIds { get; } = new();

    public string RunId { get; set; } = string.Empty;

    public string DeckId { get; set; } = string.Empty;

    public Dictionary<string, int> CardUpgradeLevels { get; } = new(StringComparer.OrdinalIgnoreCase);

    public PlayerIdDto AssignedSeatId { get; set; }

    public OnlineBattleSeatId AssignedOnlineSeatId { get; set; }

    public bool HasAssignedSeat { get; set; }

    public bool ReconnectedToPendingSeat { get; set; }

    public long LastCombatLogSequenceSent { get; set; }

    public bool IsOpen => Socket.State == WebSocketState.Open;

    public bool IsSuperseded => Volatile.Read(ref _isSuperseded) != 0;

    public DateTime LastClientMessageUtc =>
        new(Interlocked.Read(ref _lastClientMessageUtcTicks), DateTimeKind.Utc);

    public void MarkClientMessageReceived()
    {
        Interlocked.Exchange(ref _lastClientMessageUtcTicks, DateTime.UtcNow.Ticks);
    }

    public void MarkSuperseded()
    {
        Interlocked.Exchange(ref _isSuperseded, 1);
    }
}
