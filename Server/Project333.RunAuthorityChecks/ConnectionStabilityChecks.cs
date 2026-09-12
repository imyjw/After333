using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Matchmaking;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;

static class ConnectionStabilityChecks
{
    static readonly JsonSerializerOptions Json = new();
    public static async Task Run(Action<string> pass, CancellationToken ct)
    {
        var socket = new ProbeSocket { BlockFirst = true };
        var connection = Connection(socket);
        var sequence = 0;
        Task Send() => connection.SendEnvelopeAsync(() => new OnlineBattleEnvelope { MatchId = (++sequence).ToString() }, Json, ct);
        var sends = Enumerable.Range(0, 24).Select(_ => Send()).ToArray();
        await socket.Entered.Task.WaitAsync(ct);
        Require(sequence == 1, "Queued state factories must wait for the actual send gate.");
        socket.Release.TrySetResult();
        await Task.WhenAll(sends);
        Require(socket.MaxConcurrent == 1 && socket.Messages.SequenceEqual(Enumerable.Range(1, 24).Select(n => n.ToString())),
            "Concurrent sends must not overlap or build state views ahead of their transmission.");
        pass("24 concurrent envelopes serialize socket writes and state-view creation");

        socket = new ProbeSocket { BlockFirst = true }; connection = Connection(socket);
        var sending = connection.SendEnvelopeAsync(() => new(), Json, ct);
        await socket.Entered.Task.WaitAsync(ct);
        socket.CurrentState = WebSocketState.CloseReceived;
        var closing = connection.AcknowledgeCloseAsync(ct);
        Require(!closing.IsCompleted && socket.CloseCount == 0, "Close output must wait for the in-flight send.");
        socket.Release.TrySetResult();
        await Task.WhenAll(sending, closing);
        Require(socket.MaxConcurrent == 1 && socket.CloseCount == 1 && socket.AbortCount == 1 && !connection.IsOpen,
            "Close handshake must share the send gate and terminate only once.");
        pass("close output waits for the active send and completes one transport termination");

        foreach (var pve in new[] { false, true })
        foreach (var timeout in new[] { false, true })
        {
            socket = new ProbeSocket { BlockFirst = true, FailSend = !timeout };
            connection = Connection(socket);
            var session = new BattleSession("disconnect-" + Guid.NewGuid().ToString("N"), pve);
            Require(session.TryAddConnection(connection, out var error), error);
            if (!pve) Require(session.TryAddConnection(Connection(new ProbeSocket()), out error), error);
            Require(session.TryStartBattleIfReady(out error), error);
            var before = JsonSerializer.Serialize(session.CreatePersistenceSnapshot("before").DomainState);
            sending = connection.SendEnvelopeAsync(() => new(), Json, ct, TimeSpan.FromMilliseconds(timeout ? 100 : 5000));
            await socket.Entered.Task.WaitAsync(ct);
            var queuedFactoryRan = false;
            var queued = connection.SendEnvelopeAsync(() => { queuedFactoryRan = true; return new(); }, Json, ct);
            if (!timeout) socket.Release.TrySetResult();
            await MustFail(sending); await MustFail(queued);
            Require(socket.AbortCount == 1 && !queuedFactoryRan, "Failure must abort once and suppress queued state creation.");
            var detached = session.DetachConnection(connection, TimeSpan.FromMinutes(1));
            Require(detached?.Reserved == true && session.HasPendingReconnectReservations, "Send failure must preserve the disconnected seat.");
            Require(session.DetachConnection(connection, TimeSpan.FromMinutes(2)) == null, "Repeated cleanup must not extend the reservation.");
            var replacement = Connection(new ProbeSocket(), connection.AccountId);
            Require(session.TryAddConnection(replacement, out error), error);
            Require(replacement.AssignedSeatId == connection.AssignedSeatId && replacement.RunId == connection.RunId &&
                JsonSerializer.Serialize(session.CreatePersistenceSnapshot("after").DomainState) == before,
                "Reconnect must retain seat, run, hand, board and resources after transport failure.");
            Require(session.DetachConnection(connection, TimeSpan.FromMinutes(1)) == null && !session.HasPendingReconnectReservations,
                "Old cleanup cannot detach the replacement.");
        }
        pass("PVE/PVP send failures and timeouts reserve the original seat exactly once and preserve battle state on reconnect");

        var manager = new BattleSessionManager();
        var old = manager.GetOrCreate("same-room", false);
        manager.RemoveIfEmpty(old);
        var current = manager.GetOrCreate("same-room", false);
        manager.RemoveIfEmpty(old);
        Require(manager.TryGet("same-room", out var found) && ReferenceEquals(found, current), "Old room cleanup cannot remove a new room with the same ID.");
        pass("delayed empty-room cleanup preserves the replacement session with the same match ID");
    }

    public static async Task CheckDatabase(NpgsqlConnection db, IConfiguration configuration, TestAccount a, Action<string> pass, CancellationToken ct)
    {
        var service = new PvpMatchmakingService(new DbConnectionFactory(configuration));
        var original = new PvpMatchmakingConnectionRecord("disconnect-db-" + Guid.NewGuid().ToString("N"),
            a.AccountId.ToString(), Guid.NewGuid().ToString("N"), "PlayerA", "Player", a.RunId.ToString(),
            a.DeckId.ToString(), "0.1.0-dev", "127.0.0.1", "synthetic-check");
        await service.RecordConnectionJoinedAsync(original, ct);
        await service.RecordBattleStartedAsync(original.MatchId, ct);
        var replacement = original with { ConnectionId = Guid.NewGuid().ToString("N") };
        await service.RecordConnectionJoinedAsync(replacement, ct);
        await Disconnect(original); await Disconnect(original);
        await Check("active", "active", false);
        await Disconnect(replacement);
        await Check("reconnect_grace", "disconnected", true);
        var before = await Deadline();
        await Disconnect(replacement);
        Require(await Deadline() == before, "Duplicate disconnect must not reset grace deadline.");
        var third = original with { ConnectionId = Guid.NewGuid().ToString("N") };
        await service.RecordConnectionJoinedAsync(third, ct);
        await Disconnect(replacement);
        await Check("active", "active", false);
        for (var i = 0; i < 8; i++)
        {
            var next = original with { ConnectionId = Guid.NewGuid().ToString("N") };
            await Task.WhenAll(Disconnect(third), service.RecordConnectionJoinedAsync(next, ct));
            await Check("active", "active", false);
            third = next;
        }
        pass("DB late/duplicate disconnect and eight concurrent reconnect races retain the current connection and grace deadline");

        Task Disconnect(PvpMatchmakingConnectionRecord c) => service.RecordConnectionDisconnectedAsync(
            new PvpMatchmakingDisconnectRecord(c.MatchId, c.AccountId, c.ConnectionId, c.Seat, DateTimeOffset.UtcNow.AddMinutes(1)), ct);
        async Task Check(string matchStatus, string playerStatus, bool hasDeadline)
        {
            await using var command = new NpgsqlCommand("select m.match_status, p.player_status, p.reconnect_deadline_at is not null from pvp_matches m join pvp_match_players p on p.match_id=m.id where m.match_id=@match;", db);
            command.Parameters.AddWithValue("match", original.MatchId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            Require(await reader.ReadAsync(ct) && reader.GetString(0) == matchStatus && reader.GetString(1) == playerStatus && reader.GetBoolean(2) == hasDeadline,
                "Disconnect/reconnect must leave the current DB seat in its expected state.");
        }
        async Task<string> Deadline()
        {
            await using var command = new NpgsqlCommand("select p.reconnect_deadline_at::text from pvp_match_players p join pvp_matches m on p.match_id=m.id where m.match_id=@match;", db);
            command.Parameters.AddWithValue("match", original.MatchId);
            return (string)(await command.ExecuteScalarAsync(ct))!;
        }
    }

    static BattleClientConnection Connection(ProbeSocket socket, string? account = null)
    {
        var c = new BattleClientConnection(Guid.NewGuid().ToString("N"), socket)
        { AccountId = account ?? Guid.NewGuid().ToString(), PlayerToken = Guid.NewGuid().ToString("N"), RunId = Guid.NewGuid().ToString(), DeckId = Guid.NewGuid().ToString() };
        c.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        return c;
    }
    static async Task MustFail(Task task)
    {
        try { await task; } catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { return; }
        throw new InvalidOperationException("Expected transport failure.");
    }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    sealed class ProbeSocket : WebSocket
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<string> Messages = new();
        public bool BlockFirst, FailSend;
        public int MaxConcurrent, AbortCount, CloseCount;
        int concurrent, sendCount;
        public WebSocketState CurrentState = WebSocketState.Open;
        public override WebSocketState State => CurrentState;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override void Abort() { Interlocked.Increment(ref AbortCount); CurrentState = WebSocketState.Aborted; }
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? reason, CancellationToken ct) => throw new InvalidOperationException("Use serialized CloseOutputAsync.");
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? reason, CancellationToken ct)
        {
            MaxConcurrent = Math.Max(MaxConcurrent, Interlocked.Increment(ref concurrent));
            CloseCount++; Interlocked.Decrement(ref concurrent); return Task.CompletedTask;
        }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct) => throw new NotSupportedException();
        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken ct)
        {
            MaxConcurrent = Math.Max(MaxConcurrent, Interlocked.Increment(ref concurrent));
            try
            {
                if (Interlocked.Increment(ref sendCount) == 1)
                { Entered.TrySetResult(); if (BlockFirst) await Release.Task.WaitAsync(ct); }
                if (FailSend) throw new WebSocketException("Injected send failure");
                await Task.Yield();
                Messages.Add(JsonSerializer.Deserialize<OnlineBattleEnvelope>(buffer.AsSpan(), Json)!.MatchId);
            }
            finally { Interlocked.Decrement(ref concurrent); }
        }
    }
}
