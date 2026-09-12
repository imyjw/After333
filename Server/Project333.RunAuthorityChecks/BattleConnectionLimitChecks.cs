using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Security;

static class BattleConnectionLimitChecks
{
    public static void Run(Action<string> pass)
    {
        var settings = BattleConnectionLimitSettings.FromConfiguration(new ConfigurationBuilder().Build());
        Require(settings == new BattleConnectionLimitSettings(), "Absent configuration must use the documented defaults.");
        foreach (var invalid in new[] { "0", "bad", "301" })
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["PROJECT333_BATTLE_AUTH_TIMEOUT_SECONDS"] = invalid }).Build();
            try { BattleConnectionLimitSettings.FromConfiguration(config); throw new Exception("Invalid config accepted."); }
            catch (InvalidOperationException) { }
        }
        var clock = new TestClock();
        var accounts = new BattleAccountConnections(2);
        using (var guard = new BattleConnectionGuard(settings, accounts, clock))
        {
            for (var i = 0; i < 30; i++) Require(guard.TryAcceptMessage(), "Ordinary burst rejected.");
            Require(!guard.TryAcceptMessage(), "Message burst exceeded.");
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Require(guard.TryAcceptMessage() && !guard.TryAcceptMessage(), "Exactly one message should replenish in 100ms.");
            for (var i = 0; i < 4; i++) Require(guard.TryAcceptJoin(), "Join burst rejected.");
            Require(!guard.TryAcceptJoin(), "Join burst exceeded.");
            clock.Advance(TimeSpan.FromSeconds(5));
            Require(guard.TryAcceptJoin() && !guard.TryAcceptJoin(), "Exactly one join should replenish in 5s.");
            clock.Advance(TimeSpan.FromDays(1));
            for (var i = 0; i < 30; i++) Require(guard.TryAcceptMessage(), "Long-idle capacity too small.");
            Require(!guard.TryAcceptMessage(), "Long-idle refill must remain capped.");
        }
        pass("connection budgets accept normal bursts, refill by monotonic elapsed time, cap idle credit and reject invalid configuration");

        clock = new TestClock();
        using (var guard = new BattleConnectionGuard(settings, accounts, clock))
        {
            clock.Advance(TimeSpan.FromSeconds(29));
            Require(!guard.ExpireAuthenticationIfDue(), "Authentication expired early.");
            Require(guard.TryAcceptMessage(), "KeepAlive budget should remain available.");
            clock.Advance(TimeSpan.FromSeconds(1));
            Require(guard.ExpireAuthenticationIfDue(), "KeepAlive must not extend authentication deadline.");
            Require(guard.Authenticate(Guid.NewGuid()) == BattleAuthenticationAdmission.Expired, "A late DB auth response must not acquire capacity.");
        }
        using (var guard = new BattleConnectionGuard(settings, accounts, clock))
        {
            var id = Guid.NewGuid();
            Require(guard.Authenticate(id) == BattleAuthenticationAdmission.Accepted, "Valid account rejected.");
            clock.Advance(TimeSpan.FromDays(1));
            Require(!guard.ExpireAuthenticationIfDue(), "Authenticated connections must not expire on auth deadline.");
            Require(guard.Authenticate(id) == BattleAuthenticationAdmission.Accepted &&
                    guard.Authenticate(Guid.NewGuid()) == BattleAuthenticationAdmission.IdentityMismatch, "Same identity retry must be idempotent; account switching must fail even before a seat is assigned.");
        }
        pass("authentication deadline cannot be renewed by input; late authentication and unseated account switching are rejected");

        var identity = Guid.NewGuid();
        var guards = Enumerable.Range(0, 20).Select(_ => new BattleConnectionGuard(settings, accounts)).ToArray();
        var admissions = new BattleAuthenticationAdmission[guards.Length];
        Parallel.For(0, guards.Length, i => admissions[i] = guards[i].Authenticate(identity));
        Require(admissions.Count(a => a == BattleAuthenticationAdmission.Accepted) == 2 &&
                admissions.Count(a => a == BattleAuthenticationAdmission.ConnectionLimit) == 18, "Concurrent admission must grant exactly two sockets.");
        var old = guards[Array.IndexOf(admissions, BattleAuthenticationAdmission.Accepted)];
        old.Dispose(); old.Dispose();
        using var replacement = new BattleConnectionGuard(settings, accounts);
        Require(replacement.Authenticate(identity) == BattleAuthenticationAdmission.Accepted, "Disconnected capacity must be reusable.");
        old.Dispose();
        using var extra = new BattleConnectionGuard(settings, accounts);
        Require(extra.Authenticate(identity) == BattleAuthenticationAdmission.ConnectionLimit, "Late cleanup must not release replacement capacity.");
        foreach (var guard in guards) guard.Dispose();
        replacement.Dispose();
        Require(accounts.TrackedAccountCount == 0 && old.Authenticate(identity) == BattleAuthenticationAdmission.Closed, "Cleanup must remove idle account entries and forbid post-disposal admission.");
        pass("20 concurrent account admissions grant two sockets; duplicate cleanup preserves replacements and removes idle registry entries");
    }

    public static async Task CheckNetwork(TestAccount account, NpgsqlConnection db, Action<string> pass, CancellationToken ct)
    {
        await Flood("{\"MessageType\":\"KeepAlive\"}", ct);
        await Flood("{invalid", ct);
        await Flood("{\"MessageType\":\"ClientCommand\"}", ct);
        pass("real WebSocket KeepAlive, malformed JSON and unsupported command floods close with 1008 before unbounded command processing");
        using (var joins = await Open(ct))
        {
            for (var i = 0; i < 4; i++)
            {
                await Send(joins, "{\"MessageType\":\"JoinMatch\",\"MatchId\":\"guard-check\"}", ct);
                await ExpectError(joins, "missing_session_token", ct);
            }
            await Send(joins, "{\"MessageType\":\"JoinMatch\",\"MatchId\":\"guard-check\"}", ct);
            await ExpectClose(joins, "join_rate_limited", ct);
        }
        pass("real repeated unauthenticated joins exhaust the separate join budget and close with 1008");

        var before = await DatabaseCounts(db, ct);
        using (var first = await Open(ct))
        using (var second = await Open(ct))
        using (var third = await Open(ct))
        {
            // Authenticate but omit run/deck; these sockets must count even without a seat.
            var join = JsonSerializer.Serialize(new { MessageType = "JoinMatch", UseMatchmakingQueue = true, SessionToken = account.Token });
            foreach (var socket in new[] { first, second })
            {
                await Send(socket, join, ct);
                await ExpectError(socket, "battle_deck_required", ct);
            }
            await Send(third, join, ct);
            await ExpectClose(third, "account_connection_limit", ct);
            await first.CloseAsync(WebSocketCloseStatus.NormalClosure, "release", ct);
            // Server acknowledges close before disposing the admission; bounded retry handles that handoff.
            var reacquired = false;
            for (var i = 0; i < 10 && !reacquired; i++)
            {
                using var retry = await Open(ct);
                await Send(retry, join, ct);
                var response = await Receive(retry, ct);
                if (!response.Close)
                {
                    Require(response.Text.Contains("battle_deck_required"), "Released socket must authenticate normally.");
                    reacquired = true;
                    await retry.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", ct);
                }
                else { Require(response.Description == "account_connection_limit", "Unexpected retry close."); await Task.Delay(30, ct); }
            }
            Require(reacquired, "Released account capacity was not reusable.");
            await second.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", ct);
        }
        Require(await DatabaseCounts(db, ct) == before, "Rejected/unseated admission must not create matches or mutate runs/rewards.");
        pass("real authenticated unseated sockets enforce two-account-connection capacity, reuse released capacity and preserve match/run/reward counts");

        // Runner uses 5 seconds for this wire test; deterministic tests cover the 30-second production default.
        using var heartbeat = await Open(ct);
        using var partial = await Open(ct);
        await partial.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{")), WebSocketMessageType.Text, false, ct);
        var heartbeatClose = ExpectClose(heartbeat, "authentication_timeout", ct);
        var partialClose = ExpectClose(partial, "authentication_timeout", ct);
        using var sending = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sender = Task.Run(async () =>
        {
            try
            {
                while (!heartbeatClose.IsCompleted)
                {
                    await Send(heartbeat, "{\"MessageType\":\"KeepAlive\"}", sending.Token);
                    await Task.Delay(250, sending.Token);
                }
            }
            catch (OperationCanceledException) when (sending.IsCancellationRequested) { }
            catch (WebSocketException) when (heartbeatClose.IsCompletedSuccessfully) { }
        }, ct);
        try { await Task.WhenAll(heartbeatClose, partialClose); }
        finally { sending.Cancel(); await sender; }
        pass("real unauthenticated heartbeat traffic and unfinished fragmented input both expire at the authentication deadline");
    }

    public static async Task Flood(string payload, CancellationToken ct)
    {
        using var socket = await Open(ct);
        await FloodConnection(socket, payload, ct);
    }

    public static async Task FloodConnection(ClientWebSocket socket, string payload, CancellationToken ct)
    {
        var closed = ExpectClose(socket, "message_rate_limited", ct);
        try
        {
            for (var i = 0; i < 31 && !closed.IsCompleted; i++) await Send(socket, payload, ct);
        }
        catch (WebSocketException) when (closed.IsCompletedSuccessfully) { }
        await closed;
    }

    static async Task ExpectClose(ClientWebSocket socket, string reason, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(9));
        for (var i = 0; i < 300; i++)
        {
            var response = await Receive(socket, deadline.Token);
            if (!response.Close) continue;
            Require(response.Status == WebSocketCloseStatus.PolicyViolation && response.Description == reason, "Wrong policy close: " + response.Description);
            return;
        }
        throw new InvalidOperationException("Expected policy close was not received.");
    }

    static async Task ExpectError(ClientWebSocket socket, string code, CancellationToken ct)
    {
        var response = await Receive(socket, ct);
        Require(!response.Close && response.Text.Contains(code), "Expected error " + code + ", received " + response.Description);
    }

    static async Task<ClientWebSocket> Open(CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-Project333-Client-Version", "0.1.0-dev");
        await socket.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"), ct);
        var hello = await Receive(socket, ct);
        Require(!hello.Close && hello.Text.Contains("KeepAlive"), "Missing hello.");
        return socket;
    }

    static Task Send(ClientWebSocket socket, string text, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, ct);

    static async Task<(bool Close, WebSocketCloseStatus? Status, string? Description, string Text)> Receive(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[32768]; using var data = new MemoryStream();
        WebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (part.MessageType == WebSocketMessageType.Close) return (true, part.CloseStatus, part.CloseStatusDescription, "");
            data.Write(buffer, 0, part.Count);
        } while (!part.EndOfMessage);
        return (false, null, null, Encoding.UTF8.GetString(data.ToArray()));
    }

    static async Task<string> DatabaseCounts(NpgsqlConnection db, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            select json_build_array((select count(*) from pvp_matches), (select count(*) from pvp_match_reservations),
                (select sum(wins) from draft_runs), (select sum(losses) from draft_runs), (select count(*) from reward_grants))::text;
            """, db);
        return (string)(await command.ExecuteScalarAsync(ct))!;
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    sealed class TestClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan delta) => _ticks += delta.Ticks;
    }
}
