using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Text.Json;
using Project333.Runtime.Application.Online;
using Project333.PvpServer.Messages;

namespace Project333.PvpServer.BattleSessions;

public sealed class BattleClientConnection
{
    private long _lastClientMessageUtcTicks = DateTime.UtcNow.Ticks;
    private int _isSuperseded;
    private int _transportTerminated;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly TaskCompletionSource _peerCloseReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    public Guid? MatchmakingReservationId { get; set; }
    public OnlineBattleSeatId ReservedOnlineSeatId { get; set; }
    public bool MatchmakingJoinPending { get; set; }

    public bool ReconnectedToPendingSeat { get; set; }

    public long LastCombatLogSequenceSent { get; set; }

    public bool IsOpen => Volatile.Read(ref _transportTerminated) == 0 && !IsSuperseded && Socket.State == WebSocketState.Open;

    // State views are built inside the send gate so their combat-log cursors and
    // snapshots cannot be created in one order and transmitted in another.
    public async Task SendEnvelopeAsync(Func<OnlineBattleEnvelope> createEnvelope, JsonSerializerOptions options,
        CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));
        var entered = false;
        try
        {
            await _sendGate.WaitAsync(deadline.Token);
            entered = true;
            if (!IsOpen) throw new WebSocketException("Battle connection is no longer open.");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(createEnvelope(), options);
            await Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, deadline.Token);
        }
        catch
        {
            // Wake the owning receive loop; it owns the single disconnect cleanup.
            TerminateTransport();
            throw;
        }
        finally { if (entered) _sendGate.Release(); }
    }

    public Task AcknowledgeCloseAsync(CancellationToken cancellationToken) =>
        CloseTransportAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);

    public async Task CloseTransportAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken, bool waitForPeerClose = false, bool readerActive = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var entered = false;
        try
        {
            await _sendGate.WaitAsync(deadline.Token);
            entered = true;
            if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await Socket.CloseOutputAsync(status, description, deadline.Token);
            if (waitForPeerClose && Socket.State == WebSocketState.CloseSent)
            {
                // The receive-loop owner can drain after its reader unwinds. A timer must
                // await the existing reader notification and never start a second receive.
                // Give the peer a bounded chance to receive our close and acknowledge it;
                // aborting immediately can discard the close behind queued error responses.
                using var peerDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                peerDeadline.CancelAfter(TimeSpan.FromSeconds(1));
                var discard = new byte[2048];
                try
                {
                    if (readerActive)
                        await _peerCloseReceived.Task.WaitAsync(peerDeadline.Token);
                    else for (var i = 0; i < 32; i++)
                    {
                        var reply = await Socket.ReceiveAsync(new ArraySegment<byte>(discard), peerDeadline.Token);
                        if (reply.MessageType == WebSocketMessageType.Close) break;
                    }
                }
                catch (OperationCanceledException) when (peerDeadline.IsCancellationRequested) { }
                catch (WebSocketException) { }
            }
        }
        finally
        {
            if (entered) _sendGate.Release();
            TerminateTransport();
        }
    }

    public void MarkPeerCloseReceived() => _peerCloseReceived.TrySetResult();

    public void TerminateTransport()
    {
        if (Interlocked.Exchange(ref _transportTerminated, 1) == 0 && Socket.State != WebSocketState.Closed) Socket.Abort();
    }

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
