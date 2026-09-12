using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Matchmaking;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;
using Project333.PvpServer.BattleResults;

static class BattleBindingNetworkChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    public static async Task CheckDatabase(NpgsqlConnection db, RunStartService runs, IConfiguration configuration,
        TestAccount a, TestAccount b, CancellationToken ct)
    {
        try
        {
            var result = ResultDeliveryChecks.SoloWin(a);
            result = result with { Runs = new[] { result.Runs[0] with { AccountId = b.AccountId.ToString() } } };
            await new BattleResultStore(new DbConnectionFactory(configuration)).ApplyAsync(result, ct);
            throw new InvalidOperationException("Another account was allowed to record a result on this run.");
        }
        catch (RunServiceException ex) when (ex.Code == "run_result_not_recorded") { }
        var service = new PvpMatchmakingService(new DbConnectionFactory(configuration));
        var original = new PvpMatchmakingConnectionRecord("binding-db-" + Guid.NewGuid().ToString("N"),
            a.AccountId.ToString(), Guid.NewGuid().ToString("N"), "PlayerA", "Player", a.RunId.ToString(),
            a.DeckId.ToString(), "0.1.0-dev", "127.0.0.1", "synthetic-check");
        await service.RecordConnectionJoinedAsync(original, ct);
        await service.RecordBattleStartedAsync(original.MatchId, ct);
        await service.RecordConnectionJoinedAsync(original with { ConnectionId = Guid.NewGuid().ToString("N") }, ct);
        var before = await MatchSnapshot();
        foreach (var forged in new[]
        {
            original with { AccountId = b.AccountId.ToString() },
            original with { RunId = b.RunId.ToString() },
            original with { DeckId = b.DeckId.ToString() },
            original with { RuntimePlayerId = "AI" },
            original with { RunId = "", DeckId = "" },
        })
        {
            var rejected = false;
            try { await service.RecordConnectionJoinedAsync(forged with { ConnectionId = Guid.NewGuid().ToString("N") }, ct); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Started match participant rewrite must be rejected.");
            Require(await MatchSnapshot() == before, "Rejected rewrite must roll back match, player, connection and queue changes.");
        }
        async Task<string> MatchSnapshot()
        {
            await using var command = new NpgsqlCommand("""
                select jsonb_build_object(
                  'match', to_jsonb(m),
                  'players', (select jsonb_agg(to_jsonb(p) order by seat) from pvp_match_players p where p.match_id=m.id),
                  'connections', (select jsonb_agg(to_jsonb(c) order by id) from pvp_match_connections c where c.match_id=m.id),
                  'queue', (select jsonb_agg(to_jsonb(q) order by id) from pvp_match_queue q where q.matched_match_id=m.id)
                )::text from pvp_matches m where match_id=@match;
                """, db);
            command.Parameters.AddWithValue("match", original.MatchId);
            return (string)(await command.ExecuteScalarAsync(ct))!;
        }
    }

    public static async Task Run(TestAccount a, TestAccount b, CancellationToken ct)
    {
        for (var scenario = 0; scenario < 3; scenario++)
        {
            using var first = await Open(ct);
            using var second = await Open(ct);
            using var replacement = await Open(ct);
            using var afterRejectedInput = new ClientWebSocket();
            using var afterRateLimit = new ClientWebSocket();
            var match = "binding-wire-" + Guid.NewGuid().ToString("N");
            var joinA = Join(a, match); var joinB = Join(b, match);
            await Send(first, joinA, ct);
            var assigned = await ReadUntil(first, m => m.HasAssignedSeat && m.AccountId == a.AccountId.ToString(), ct);
            match = assigned.MatchId;
            joinA.MatchId = match;
            await Send(second, joinB, ct);
            await ReadUntil(second, m => m.StateView != null, ct);
            var initial = await ReadUntil(first, m => m.StateView != null, ct);
            var active = first;
            if (scenario == 0)
            {
                // A valid token from another account may not relabel the existing socket.
                await Send(first, Join(b, match), ct);
                var rejection = await ReadUntil(first, m => m.Error != null, ct, allowError: true);
                Require(rejection.Error!.Code == "battle_participant_mismatch", "Socket account switch must return the stable identity error.");
                joinA.RunId = b.RunId.ToString(); joinA.DeckId = b.DeckId.ToString();
                joinA.PlayerDeckCardIds = new() { "not-a-real-card" };
                await Send(first, joinA, ct);
            }
            else
            {
                if (scenario == 2)
                {
                    first.Abort();
                    await ReadUntil(second, m => m.BattleEvents.Any(e => e.EventType == BattleEventType.ReconnectGraceStarted), ct);
                }
                joinA.IsReconnectAttempt = true; joinA.PreviousConnectionId = assigned.ConnectionId;
                joinA.RunId = b.RunId.ToString(); joinA.DeckId = b.DeckId.ToString();
                joinA.PlayerDeckCardIds = new() { "not-a-real-card" };
                active = replacement;
                await Send(active, joinA, ct);
            }
            var resumed = await ReadUntil(active, m => m.HasAssignedSeat && m.AccountId == a.AccountId.ToString(), ct);
            Require(resumed.AssignedSeatId == assigned.AssignedSeatId, "Reconnect must retain the original seat.");
            var restored = await ReadUntil(active, m => m.StateView != null, ct);
            Require(JsonSerializer.Serialize(initial.StateView!.Player, Json) == JsonSerializer.Serialize(restored.StateView!.Player, Json) &&
                    JsonSerializer.Serialize(initial.StateView.Opponent, Json) == JsonSerializer.Serialize(restored.StateView.Opponent, Json),
                "Reconnect must preserve original hands, board, resources and hidden opponent information.");
            if (scenario == 2)
            {
                await BattleInputChecks.ExpectTooLargeClose(active, ct);
                await ReadUntil(second, m => m.BattleEvents.Any(e => e.EventType == BattleEventType.ReconnectGraceStarted), ct);
                afterRejectedInput.Options.SetRequestHeader("X-Project333-Client-Version", "0.1.0-dev");
                await afterRejectedInput.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"), ct);
                joinA.PreviousConnectionId = resumed.ConnectionId;
                active = afterRejectedInput;
                await Send(active, joinA, ct);
                resumed = await ReadUntil(active, m => m.HasAssignedSeat && m.AccountId == a.AccountId.ToString(), ct);
                restored = await ReadUntil(active, m => m.StateView != null, ct);
                Require(resumed.AssignedSeatId == assigned.AssignedSeatId &&
                    JsonSerializer.Serialize(initial.StateView.Player, Json) == JsonSerializer.Serialize(restored.StateView!.Player, Json) &&
                    JsonSerializer.Serialize(initial.StateView.Opponent, Json) == JsonSerializer.Serialize(restored.StateView.Opponent, Json),
                    "Rejected input must not alter the board or prevent the original participant from reconnecting.");
            }
            if (scenario == 0)
            {
                await BattleConnectionLimitChecks.FloodConnection(active, "{\"MessageType\":\"KeepAlive\"}", ct);
                await ReadUntil(second, m => m.BattleEvents.Any(e => e.EventType == BattleEventType.ReconnectGraceStarted), ct);
                afterRateLimit.Options.SetRequestHeader("X-Project333-Client-Version", "0.1.0-dev");
                await afterRateLimit.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"), ct);
                joinA.IsReconnectAttempt = true;
                joinA.PreviousConnectionId = resumed.ConnectionId;
                active = afterRateLimit;
                await Send(active, joinA, ct);
                resumed = await ReadUntil(active, m => m.HasAssignedSeat && m.AccountId == a.AccountId.ToString(), ct);
                restored = await ReadUntil(active, m => m.StateView != null, ct);
                Require(resumed.AssignedSeatId == assigned.AssignedSeatId &&
                    JsonSerializer.Serialize(initial.StateView.Player, Json) == JsonSerializer.Serialize(restored.StateView!.Player, Json) &&
                    JsonSerializer.Serialize(initial.StateView.Opponent, Json) == JsonSerializer.Serialize(restored.StateView.Opponent, Json),
                    "Rate rejection must preserve reconnect grace, seat, hand and board.");
            }
            await Send(active, new OnlineBattleEnvelope
            {
                MessageType = OnlineBattleMessageType.ClientCommand,
                ClientCommand = new ClientBattleCommandMessage
                {
                    MatchId = match, ActorId = resumed.AssignedSeatId, AccountId = a.AccountId.ToString(),
                    PlayerToken = joinA.PlayerToken, CommandType = OnlineBattleCommandType.Surrender, Sequence = 1,
                },
            }, ct);
            // Program persists run results before broadcasting the final state.
            await ReadUntil(active, m => m.StateView?.IsEnded == true, ct);
            first.Abort(); second.Abort(); replacement.Abort(); afterRejectedInput.Abort(); afterRateLimit.Abort();
        }
    }

    static OnlineBattleEnvelope Join(TestAccount account, string match) => new()
    {
        MessageType = OnlineBattleMessageType.JoinMatch, MatchId = match, SessionToken = account.Token,
        PlayerToken = account.AccountId.ToString(), AccountId = account.AccountId.ToString(),
        RunId = account.RunId.ToString(), DeckId = account.DeckId.ToString(),
        PlayerDeckCardIds = new() { "uploaded-cards-must-not-be-used" }, UseServerAiOpponent = false, UseMatchmakingQueue = true,
    };
    static async Task<ClientWebSocket> Open(CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-Project333-Client-Version", "0.1.0-dev");
        await socket.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"), ct);
        return socket;
    }
    static Task Send(ClientWebSocket socket, OnlineBattleEnvelope message, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(message, Json)), WebSocketMessageType.Text, true, ct);
    static async Task<OnlineBattleEnvelope> ReadUntil(ClientWebSocket socket, Func<OnlineBattleEnvelope, bool> predicate,
        CancellationToken ct, bool allowError = false)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var buffer = new byte[32768];
        for (var i = 0; i < 100; i++)
        {
            using var data = new MemoryStream();
            WebSocketReceiveResult part;
            do
            {
                part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                Require(part.MessageType != WebSocketMessageType.Close, "Battle socket closed before the expected response.");
                data.Write(buffer, 0, part.Count);
            } while (!part.EndOfMessage);
            var message = JsonSerializer.Deserialize<OnlineBattleEnvelope>(data.ToArray(), Json)!;
            if (message.Error != null && !allowError) throw new InvalidOperationException("Battle rejected request: " + message.Error.Code);
            if (predicate(message)) return message;
        }
        throw new InvalidOperationException("Expected battle response was not received.");
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
