using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Project333.PvpServer.BattleResults;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Matchmaking;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;

static class ServerRestartCancellationChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    static string Root => Path.GetDirectoryName(Environment.GetEnvironmentVariable("AFTER333_AUTHORITY_TEST_DATA"))!;

    public static void SaveAccounts(TestAccount[] accounts) =>
        File.WriteAllText(Path.Combine(Root, "restart-battle-accounts.json"), JsonSerializer.Serialize(accounts));

    public static async Task CheckShutdown(Action<string> pass, CancellationToken ct)
    {
        foreach (var pve in new[] { false, true })
        {
            var manager = new BattleSessionManager();
            var session = manager.GetOrCreate("shutdown-" + Guid.NewGuid().ToString("N"), pve);
            var a = Connection(); var b = Connection();
            Require(session.TryAddConnection(a, out var error), error);
            if (!pve) Require(session.TryAddConnection(b, out error), error);
            Require(session.TryStartBattleIfReady(out error), error);
            Require(session.TryGetCurrentTurnTimer(out var actor, out var version, out var deadline), "Timer required.");
            Require(session.TryCreateDisconnectReconnectGraceEnvelope(a, TimeSpan.FromMilliseconds(1), out _, out var seat, out var identity, out var reconnectDeadline), "Reservation required.");
            var before = JsonSerializer.Serialize(session.CreatePersistenceSnapshot("before").DomainState);
            await Task.Delay(10, ct); // The forfeit is already due; only shutdown may suppress it.
            manager.CancelForServerShutdown();
            await Task.Delay(10, ct); // The forfeit is already due; only shutdown may suppress it.
            manager.CancelForServerShutdown();
            Require(!session.TryCreateExpiredDisconnectForfeitEnvelope(seat, identity, reconnectDeadline, out _), "Shutdown must suppress pending forfeits.");
            Require(!session.TryCreateTurnTimeoutEnvelope(actor, version, deadline, out _), "Shutdown must suppress timer resolution.");
            Require(session.RunServerAiActionIfNeeded(forceEndTurn: true).Count == 0, "Shutdown must suppress AI actions.");
            Throws(() => session.ApplyCommand(new ClientBattleCommandMessage { ActorId = a.AssignedSeatId, CommandType = OnlineBattleCommandType.Surrender }));
            Throws(() => manager.GetOrCreate("late-join", pve));
            Require(!session.TryAddConnection(Connection(), out _), "Shutdown must reject joins.");
            Require(!session.HasPendingReconnectReservations && session.GetFinalResult() == null && !session.IsBattleEnded, "Cancellation must produce no result, including a draw.");
            Require(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("after").DomainState) == before, "Shutdown must not mutate battle state into an outcome.");
            pass($"{(pve ? "PvE" : "PvP")} shutdown cancels pending actions/timers/forfeit and creates no result");
        }

        var ended = new BattleSession("ended-before-shutdown", true);
        var player = Connection();
        Require(ended.TryAddConnection(player, out _) && ended.TryStartBattleIfReady(out _), "Start completed fixture.");
        ended.ApplyCommand(new ClientBattleCommandMessage { ActorId = player.AssignedSeatId, CommandType = OnlineBattleCommandType.Surrender });
        var final = ended.GetFinalResult()!.ToJson();
        ended.CancelForServerShutdown();
        Require(ended.GetFinalResult()!.ToJson() == final, "Already finalized result must survive shutdown.");
        pass("shutdown preserves an already finalized result and its exact ID");

        var directory = Path.Combine(Root, "startup-journal-check");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["PROJECT333_BATTLE_RESULT_OUTBOX_DIR"] = directory }).Build();
        var store = new ToggleStore();
        using var outbox = new BattleResultOutbox(store, config, NullLogger<BattleResultOutbox>.Instance);
        outbox.Capture(ended.GetFinalResult()!);
        await ThrowsAsync(() => outbox.ReplayBeforeStartupCancellationAsync(ct));
        Require(Directory.GetFiles(directory, "*.json").Length == 1, "Failed startup preserves completed result journal.");
        store.Fail = false;
        using (var retry = new BattleResultOutbox(store, config, NullLogger<BattleResultOutbox>.Instance))
            await retry.ReplayBeforeStartupCancellationAsync(ct);
        Require(store.Applied == 1 && Directory.GetFiles(directory, "*.json").Length == 0, "Completed result must replay before cancellation.");
        File.WriteAllText(Path.Combine(directory, "malformed.json"), "{broken");
        using var malformed = new BattleResultOutbox(store, config, NullLogger<BattleResultOutbox>.Instance);
        await ThrowsAsync(() => malformed.ReplayBeforeStartupCancellationAsync(ct));
        Require(File.Exists(Path.Combine(directory, "malformed.json")), "Unreadable journal must block startup, not silently cancel its match.");
        pass("startup replay preserves completed results and blocks cancellation when journals cannot be delivered/read");
    }

    // Runs in a separate client process while the runner kills/restarts only its own HTTP server.
    public static async Task HoldAcrossRestart(HttpClient http, NpgsqlConnection db, CancellationToken ct)
    {
        var accounts = JsonSerializer.Deserialize<TestAccount[]>(File.ReadAllText(Path.Combine(Root, "restart-battle-accounts.json")))!;
        using var first = await Open(ct); using var second = await Open(ct); using var solo = await Open(ct);
        await Send(first, Join(accounts[0], "", false), ct);
        var assigned = await ReadUntil(first, m => m.HasAssignedSeat, ct);
        var match = assigned.MatchId;
        await Send(second, Join(accounts[1], "", false), ct);
        var assignedB = await ReadUntil(second, m => m.HasAssignedSeat, ct);
        await ReadUntil(second, m => m.StateView != null, ct);
        await ReadUntil(first, m => m.StateView != null, ct);
        await PassMulligan(first, accounts[0], assigned, ct);
        await PassMulligan(second, accounts[1], assignedB, ct);
        await ReadUntil(first, m => m.StateView?.Phase.ToString() == "Main", ct);
        var challengeAdmission = await PveReconnectChecks.StartChallenge(http, accounts[2], ct);
        var pveMatch = challengeAdmission.MatchId;
        await Send(solo, Join(accounts[2], pveMatch, true, challengeAdmission.AttemptId), ct);
        var assignedSolo = await ReadUntil(solo, m => m.HasAssignedSeat, ct);
        await ReadUntil(solo, m => m.StateView != null, ct);
        await PassMulligan(solo, accounts[2], assignedSolo, ct);
        await ReadUntil(solo, m => m.StateView?.Phase.ToString() == "Main", ct);
        // Remain connected through the actual process kill. Both battles are still unfinished.
        var before = await ProtectedSnapshot(db, accounts, ct);
        File.WriteAllText(Path.Combine(Root, "restart-battles.ready"), match);
        while (!File.Exists(Path.Combine(Root, "restart-battles.resume"))) await Task.Delay(100, ct);
        Require(await ProtectedSnapshot(db, accounts, ct) == before, "Restart must preserve all run/economy rows and result receipts.");
        await using (var state = new NpgsqlCommand("""
            select m.match_status='canceled' and m.ended_reason='server_cancel' and m.winner_account_id is null
              and m.completed_at is not null
              and not exists(select 1 from pvp_match_players p where p.match_id=m.id and (p.final_result is not null or p.reconnect_deadline_at is not null))
              and not exists(select 1 from pvp_match_connections c where c.match_id=m.id and c.connection_status='active')
              and not exists(select 1 from pvp_match_reservations r where r.match_id=m.id)
            from pvp_matches m where m.match_id=@match;
            """, db))
        {
            state.Parameters.AddWithValue("match", match);
            Require(await state.ExecuteScalarAsync(ct) is true, "Interrupted PvP must be canceled without participant outcomes/reservations.");
        }
        await using (var challenge = new NpgsqlCommand("""
            select status='canceled' and ended_reason='server_cancel' and refunded_at is not null
                and result_id is null and won is null and resource_gold_awarded=0
            from ai_challenge_attempts where id=@id;
            """, db))
        {
            challenge.Parameters.AddWithValue("id", Guid.Parse(challengeAdmission.AttemptId));
            Require(await challenge.ExecuteScalarAsync(ct) is true,
                "Server restart must cancel and refund the original challenge attempt without a result.");
        }
        for (var i = 0; i < 3; i++)
        {
            using var reconnect = await Open(ct);
            var join = Join(accounts[i], i == 2 ? pveMatch : match, i == 2, i == 2 ? challengeAdmission.AttemptId : "");
            join.IsReconnectAttempt = true;
            await Send(reconnect, join, ct);
            var rejection = await ReadUntil(reconnect, m => m.Error != null, ct, true);
            Require(rejection.Error!.Code == (i == 2 ? "ai_challenge_not_available" : "battle_cancelled"), "Old session must not restore or silently queue a new battle.");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/pvp/reconnect-status"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accounts[0].Token);
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync(ct);
            Require(text.Contains("false", StringComparison.OrdinalIgnoreCase) && !text.Contains(match), "Canceled battle must not be offered for reconnect.");
        }
        Require(await ProtectedSnapshot(db, accounts, ct) == before, "Rejected reconnects must not alter scores/economy.");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            [DbConnectionFactory.ConnectionStringKey] = db.ConnectionString }).Build();
        var matchmaking = new PvpMatchmakingService(new DbConnectionFactory(configuration));
        var again = await matchmaking.CancelInterruptedMatchesOnServerStartupAsync(ct);
        Require(again.MatchCount == 0 && again.ClosedConnectionCount == 0, "Repeated startup cleanup must be idempotent.");
        // A late result for the canceled PvP may not recreate outcomes.
        var stale = new BattleSession(match, false);
        var a = Connection(accounts[0]); var b = Connection(accounts[1]);
        Require(stale.TryAddConnection(a, out _) && stale.TryAddConnection(b, out _) && stale.TryStartBattleIfReady(out _), "Stale fixture starts.");
        stale.ApplyCommand(new ClientBattleCommandMessage { ActorId = a.AssignedSeatId, CommandType = OnlineBattleCommandType.Surrender });
        Require(!await new BattleResultStore(new DbConnectionFactory(configuration)).ApplyAsync(stale.GetFinalResult()!, ct), "Canceled PvP rejects a delayed result.");
        Require(await ProtectedSnapshot(db, accounts, ct) == before, "Stale result must leave no receipt, score or economic changes.");
        using var fresh = await Open(ct);
        await Send(fresh, Join(accounts[0], "", false), ct);
        var newAssignment = await ReadUntil(fresh, m => m.HasAssignedSeat, ct);
        Require(newAssignment.MatchId != match, "Existing run/deck must be able to queue a fresh battle.");
        Console.WriteLine("PASS: actual HTTP process kill/restart cancels live PvP/PvE; scores/economy/receipts unchanged; old reconnects rejected; cleanup idempotent; late results suppressed; same run/deck can queue again.");
    }

    static Task PassMulligan(ClientWebSocket socket, TestAccount account, OnlineBattleEnvelope assigned, CancellationToken ct) =>
        Send(socket, new OnlineBattleEnvelope {
            MessageType=OnlineBattleMessageType.ClientCommand,
            ClientCommand=new ClientBattleCommandMessage {
                MatchId=assigned.MatchId, ActorId=assigned.AssignedSeatId, AccountId=account.AccountId.ToString(),
                PlayerToken=account.AccountId.ToString(), CommandType=OnlineBattleCommandType.PassMulligan, Sequence=1 }
        }, ct);
    static async Task<string> ProtectedSnapshot(NpgsqlConnection db, TestAccount[] accounts, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            select jsonb_build_object(
              'runs',(select jsonb_agg(to_jsonb(t) order by id) from draft_runs t where account_id=any(@accounts)),
              'decks',(select jsonb_agg(to_jsonb(t) order by id) from decks t where account_id=any(@accounts)),
              'wallets',(select jsonb_agg(to_jsonb(t) order by account_id) from user_wallets t where account_id=any(@accounts)),
              'cards',(select jsonb_agg(to_jsonb(t) order by account_id,card_id) from user_card_collection t where account_id=any(@accounts)),
              'transactions',(select jsonb_agg(to_jsonb(t) order by id) from account_transactions t where account_id=any(@accounts)),
              'grants',(select jsonb_agg(to_jsonb(t) order by id) from reward_grants t where account_id=any(@accounts)),
              'receipts',(select jsonb_agg(to_jsonb(t) order by result_id) from battle_result_receipts t),
              'results',(select jsonb_agg(to_jsonb(t) order by result_id,seat) from battle_run_results t))::text;
            """, db);
        command.Parameters.AddWithValue("accounts", accounts.Select(a => a.AccountId).ToArray());
        return (string)(await command.ExecuteScalarAsync(ct))!;
    }
    static BattleClientConnection Connection(TestAccount? account = null)
    {
        var c = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket()) {
            AccountId=(account?.AccountId ?? Guid.NewGuid()).ToString(), PlayerToken=Guid.NewGuid().ToString("N"),
            RunId=(account?.RunId ?? Guid.NewGuid()).ToString(), DeckId=(account?.DeckId ?? Guid.NewGuid()).ToString() };
        c.PlayerDeckCardIds.AddRange(account?.Cards ?? Enumerable.Repeat("Goblin",33).ToArray());
        return c;
    }
    static OnlineBattleEnvelope Join(TestAccount a, string match, bool pve, string challengeAttemptId = "") => new() {
        MessageType=OnlineBattleMessageType.JoinMatch, MatchId=match, SessionToken=a.Token,
        PlayerToken=a.AccountId.ToString(), AccountId=a.AccountId.ToString(), RunId=a.RunId.ToString(), DeckId=a.DeckId.ToString(),
        UseServerAiOpponent=pve, UseMatchmakingQueue=!pve,
        GameMode=pve ? "ai_challenge" : "run", AiChallengeAttemptId=challengeAttemptId };
    static async Task<ClientWebSocket> Open(CancellationToken ct)
    {
        var socket=new ClientWebSocket(); socket.Options.SetRequestHeader("X-Project333-Client-Version","0.1.0-dev");
        await socket.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"),ct); return socket;
    }
    static Task Send(ClientWebSocket socket, OnlineBattleEnvelope message, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(message,Json)),WebSocketMessageType.Text,true,ct);
    static async Task<OnlineBattleEnvelope> ReadUntil(ClientWebSocket socket, Func<OnlineBattleEnvelope,bool> predicate, CancellationToken ct, bool allowError=false)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var buffer=new byte[32768];
        while(true) {
            using var data=new MemoryStream(); WebSocketReceiveResult part;
            do { part=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),deadline.Token); Require(part.MessageType!=WebSocketMessageType.Close,"Unexpected close."); data.Write(buffer,0,part.Count); } while(!part.EndOfMessage);
            var m=JsonSerializer.Deserialize<OnlineBattleEnvelope>(data.ToArray(),Json)!;
            if(m.Error!=null && !allowError) throw new InvalidOperationException(m.Error.Code);
            if(predicate(m)) return m;
        }
    }
    sealed class ToggleStore : IBattleResultStore {
        public bool IsConfigured=>true; public bool Fail=true; public int Applied;
        public Task<bool> ApplyAsync(BattleResult result,CancellationToken ct) { if(Fail)throw new IOException("Synthetic failure"); Applied++; return Task.FromResult(true); }
    }
    static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    static void Throws(Action action) { try { action(); } catch(InvalidOperationException) { return; } throw new InvalidOperationException("Expected rejection."); }
    static async Task ThrowsAsync(Func<Task> action) { try { await action(); } catch(InvalidOperationException) { return; } throw new InvalidOperationException("Expected rejection."); }
}
