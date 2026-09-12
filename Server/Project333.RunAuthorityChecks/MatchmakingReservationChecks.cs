using Microsoft.Extensions.Configuration;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Matchmaking;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;
using Project333.Runtime.Application.Online;

static class MatchmakingReservationChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    public static async Task Run(NpgsqlConnection db, IConfiguration config, TestAccount a, TestAccount b,
        TestAccount c, TestAccount d, Action<string> pass, CancellationToken ct)
    {
        var service = new PvpMatchmakingService(new DbConnectionFactory(config));
        var manager = new BattleSessionManager();
        var lease = manager.AcquireForJoin("join-in-progress", false);
        var otherLease = manager.AcquireForJoin("join-in-progress", false);
        manager.RemoveIfEmpty(lease.Session);
        Require(manager.TryGet("join-in-progress", out var kept) && ReferenceEquals(kept, lease.Session), "Waiting disconnect cannot remove a room while another join is using it.");
        lease.Dispose(); lease.Dispose();
        Require(manager.TryGet("join-in-progress", out kept) && ReferenceEquals(kept, otherLease.Session), "One completed join must not release another join's room lease.");
        otherLease.Dispose();
        Require(!manager.TryGet("join-in-progress", out _), "Unused room must be removed after the last join completes.");
        pass("pending join leases keep one shared room through waiting disconnects and release empty rooms exactly once");
        var held = new List<(PvpMatchmakingQueueRequest Request, PvpMatchmakingMatchAssignment Assignment)>();
        PvpMatchmakingQueueRequest Request(TestAccount x) => new(x.AccountId.ToString(), x.RunId.ToString(), x.DeckId.ToString(), "0.1.0-dev", Guid.NewGuid().ToString("N"));
        PvpMatchmakingConnectionRecord Record(PvpMatchmakingQueueRequest r, PvpMatchmakingMatchAssignment m) =>
            new(m.MatchId, r.AccountId, r.ConnectionId, m.Seat, m.Seat == "PlayerA" ? "Player" : "AI", r.RunId, r.DeckId, r.ClientVersion, "127.0.0.1", "synthetic-reservation-check", m.ReservationId);
        async Task<PvpMatchmakingMatchAssignment> Reserve(PvpMatchmakingQueueRequest r)
        { var m = await service.ResolveMatchForQueueAsync(r, ct); held.Add((r, m)); return m; }
        async Task Clear()
        {
            foreach (var (r, m) in held)
            {
                await service.RecordConnectionDisconnectedAsync(new(m.MatchId, r.AccountId, r.ConnectionId, m.Seat, null), ct);
                if (m.ReservationId.HasValue) await service.ReleaseReservationAsync(m.ReservationId.Value, r.ConnectionId, ct);
            }
            held.Clear();
        }
        var ar = Request(a); var br = Request(b); var cr = Request(c);
        var first = await Reserve(ar);
        await service.RecordConnectionJoinedAsync(Record(ar, first), ct);
        var second = await Reserve(br); var third = await Reserve(cr);
        Require(first.MatchId == second.MatchId && third.MatchId != first.MatchId && second.Seat != first.Seat,
            "Third request must not enter a two-seat match while the second join is still pending.");
        await service.RecordConnectionJoinedAsync(Record(br, second), ct);
        await Clear();
        pass("reproduced three-account assignment gap is closed by transactional seat reservations");

        var requests = new[] { a, b, c, d }.Select(Request).ToArray();
        var pairs = await Task.WhenAll(requests.Select(r => service.ResolveMatchForQueueAsync(r, ct)));
        for (var i = 0; i < 4; i++) held.Add((requests[i], pairs[i]));
        Require(pairs.GroupBy(m => m.MatchId).Count() == 2 && pairs.GroupBy(m => m.MatchId).All(g => g.Count() == 2 && g.Select(m => m.Seat).Distinct().Count() == 2),
            "Four concurrent unjoined accounts must reserve two distinct complete pairs.");
        foreach (var pair in held.GroupBy(x => x.Assignment.MatchId))
        {
            var session = new BattleSession(pair.Key, false);
            foreach (var item in pair.OrderByDescending(x => x.Assignment.Seat))
            {
                var r = item.Request; var m = item.Assignment;
                var client = new BattleClientConnection(r.ConnectionId, new OfflineSocket())
                { AccountId = r.AccountId, PlayerToken = r.AccountId, RunId = r.RunId, DeckId = r.DeckId,
                    ReservedOnlineSeatId = Enum.Parse<OnlineBattleSeatId>(m.Seat), MatchmakingJoinPending = true };
                client.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
                Require(session.TryAddConnection(client, out var error), error);
                Require(client.AssignedOnlineSeatId.ToString() == m.Seat, "Reverse arrival must preserve the reserved seat.");
                Require(!session.TryStartBattleIfReady(out _), "Battle must wait for every participant DB commit.");
                await service.RecordConnectionJoinedAsync(Record(r, m), ct);
                session.ConfirmMatchmakingJoin(client);
            }
            Require(session.TryStartBattleIfReady(out var startError), startError);
        }
        await Clear();
        pass("four concurrent reservations form two pairs; reverse arrivals retain seats and battle waits for DB confirmation");

        ar = Request(a); first = await Reserve(ar);
        var repeated = await service.ResolveMatchForQueueAsync(ar, ct);
        Require(repeated.ReservationId == first.ReservationId && repeated.Seat == first.Seat, "Same connection retry must reuse its lease.");
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        { try { await service.ResolveMatchForQueueAsync(Request(a), ct); return false; } catch (InvalidOperationException) { return true; } }));
        Require(duplicates.All(x => x), "Parallel sockets of one account must not reserve additional seats.");
        await service.ReleaseReservationAsync(first.ReservationId!.Value, "wrong-connection", ct);
        await MustReject(() => service.RecordConnectionJoinedAsync(Record(ar, first) with { Seat = "PlayerB", RuntimePlayerId = "AI" }, ct));
        await MustReject(() => service.RecordConnectionJoinedAsync(Record(ar, first) with { AccountId = b.AccountId.ToString() }, ct));
        Require(await LeaseCount(first.ReservationId.Value) == 1, "Rejected consumption or wrong cleanup must retain the original lease.");
        await Execute("""
            create function check_fail_reserved_join() returns trigger language plpgsql as $$ begin
              if NEW.user_agent='fail-reserved-join' then raise exception 'synthetic join failure'; end if; return NEW; end; $$;
            create trigger check_fail_reserved_join before insert on pvp_match_connections for each row execute function check_fail_reserved_join();
            """);
        try { await service.RecordConnectionJoinedAsync(Record(ar, first) with { UserAgent = "fail-reserved-join" }, ct); throw new Exception("Expected SQL fault"); }
        catch (PostgresException ex) when (ex.SqlState == "P0001") { }
        await Execute("drop trigger check_fail_reserved_join on pvp_match_connections; drop function check_fail_reserved_join();");
        Require(await LeaseCount(first.ReservationId.Value) == 1, "Failed join must roll back reservation consumption.");
        await service.RecordConnectionJoinedAsync(Record(ar, first), ct);
        Require(await LeaseCount(first.ReservationId.Value) == 0, "Successful join must consume its reservation once.");
        await service.ReleaseReservationAsync(first.ReservationId.Value, ar.ConnectionId, ct);
        await Clear();
        pass("same-account concurrency, idempotent retry, wrong-owner/seat rejection and failed DB join preserve reservation ownership");

        ar = Request(a); first = await Reserve(ar);
        await service.RecordConnectionJoinedAsync(Record(ar, first), ct);
        br = Request(b); second = await Reserve(br);
        await using (var expiry = new NpgsqlCommand("update pvp_match_reservations set expires_at=clock_timestamp()-interval '1 second' where id=@id;", db))
        { expiry.Parameters.AddWithValue("id", second.ReservationId!.Value); await expiry.ExecuteNonQueryAsync(ct); }
        cr = Request(c); third = await Reserve(cr);
        Require(third.MatchId == first.MatchId && third.Seat == second.Seat, "Expired unjoined reservation must free exactly its seat.");
        await MustReject(() => service.RecordConnectionJoinedAsync(Record(br, second), ct));
        await service.ReleaseReservationAsync(second.ReservationId.Value, br.ConnectionId, ct);
        Require(await LeaseCount(third.ReservationId!.Value) == 1, "Late cleanup must not release a newer reservation.");
        await Clear();
        Require((long)(await Scalar("select count(*) from pvp_match_reservations;"))! == 0, "Tests must release all pending leases.");
        pass("expired leases release capacity; late joins/cleanup cannot steal the replacement reservation; waiting disconnects free seats");

        async Task<long> LeaseCount(Guid id)
        { await using var q = new NpgsqlCommand("select count(*) from pvp_match_reservations where id=@id;", db); q.Parameters.AddWithValue("id", id); return (long)(await q.ExecuteScalarAsync(ct))!; }
        async Task Execute(string sql) { await using var q = new NpgsqlCommand(sql, db); await q.ExecuteNonQueryAsync(ct); }
        async Task<object?> Scalar(string sql) { await using var q = new NpgsqlCommand(sql, db); return await q.ExecuteScalarAsync(ct); }
        static async Task MustReject(Func<Task> action) { try { await action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected rejection"); }
    }

    public static async Task CheckNetwork(TestAccount[] accounts, Action<string> pass, CancellationToken ct)
    {
        var sockets = accounts.Select(_ => new ClientWebSocket()).ToArray();
        try
        {
            var assigned = await Task.WhenAll(accounts.Select(async (account, i) =>
            {
                sockets[i].Options.SetRequestHeader("X-Project333-Client-Version", "0.1.0-dev");
                await sockets[i].ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"), ct);
                await Send(sockets[i], new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.JoinMatch,
                    UseMatchmakingQueue = true, UseServerAiOpponent = false, SessionToken = account.Token,
                    PlayerToken = account.AccountId.ToString(), RunId = account.RunId.ToString(), DeckId = account.DeckId.ToString() }, ct);
                return await Read(sockets[i], m => m.HasAssignedSeat && m.AccountId == account.AccountId.ToString(), ct);
            }));
            Require(assigned.GroupBy(x => x.MatchId).Count() == 2 && assigned.GroupBy(x => x.MatchId).All(g => g.Count() == 2 && g.Select(x => x.AssignedSeatId).Distinct().Count() == 2), "Four simultaneous real clients must form two distinct matches.");
            await Task.WhenAll(sockets.Select(s => Read(s, m => m.StateView != null, ct)));
            for (var i = 0; i < 4; i++)
            {
                if (assigned[i].AssignedSeatId != PlayerIdDto.Player) continue;
                await Send(sockets[i], new OnlineBattleEnvelope { MessageType = OnlineBattleMessageType.ClientCommand,
                    ClientCommand = new() { MatchId = assigned[i].MatchId, ActorId = assigned[i].AssignedSeatId,
                        PlayerToken = accounts[i].AccountId.ToString(), AccountId = accounts[i].AccountId.ToString(),
                        CommandType = Project333.PvpServer.Messages.OnlineBattleCommandType.Surrender, Sequence = 1 } }, ct);
            }
            await Task.WhenAll(sockets.Select(s => Read(s, m => m.StateView?.IsEnded == true, ct)));
            pass("four simultaneous authenticated WebSocket joins form two complete battles without match_full or lost seats");
        }
        finally { foreach (var socket in sockets) { socket.Abort(); socket.Dispose(); } }
    }
    static Task Send(ClientWebSocket socket, OnlineBattleEnvelope message, CancellationToken ct) => socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(message, Json)), WebSocketMessageType.Text, true, ct);
    static async Task<OnlineBattleEnvelope> Read(ClientWebSocket socket, Func<OnlineBattleEnvelope, bool> predicate, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var buffer = new byte[32768];
        for (var i = 0; i < 50; i++)
        {
            using var stream = new MemoryStream(); WebSocketReceiveResult part;
            do { part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token); Require(part.MessageType != WebSocketMessageType.Close, "Unexpected close"); stream.Write(buffer, 0, part.Count); } while (!part.EndOfMessage);
            var message = JsonSerializer.Deserialize<OnlineBattleEnvelope>(stream.ToArray(), Json)!;
            Require(message.Error == null, "Matchmaking request rejected: " + message.Error?.Code);
            if (predicate(message)) return message;
        }
        throw new Exception("Expected matchmaking response not received.");
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
