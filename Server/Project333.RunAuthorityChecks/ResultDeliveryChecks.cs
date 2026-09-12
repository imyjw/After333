using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Project333.PvpServer.BattleResults;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Matchmaking;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;
using Project333.Runtime.Application.Online;
using ClientBattleCommandMessage = Project333.PvpServer.Messages.ClientBattleCommandMessage;
using OnlineBattleCommandType = Project333.PvpServer.Messages.OnlineBattleCommandType;

static class ResultDeliveryChecks
{
    public static BattleResult SoloWin(TestAccount a) => new(Guid.NewGuid(), "delivery-" + Guid.NewGuid().ToString("N"),
        true, false, OnlineBattleSeatId.PlayerA, "normal",
        new[] { new BattleRunResultRecord(PlayerIdDto.Player, OnlineBattleSeatId.PlayerA,
            a.AccountId.ToString(), a.RunId.ToString(), a.DeckId.ToString(), true) });

    public static async Task Run(NpgsqlConnection db, IConfiguration configuration, TestAccount a, TestAccount b,
        Action<string> pass, CancellationToken ct)
    {
        var store = new BattleResultStore(new DbConnectionFactory(configuration));
        var matchmaking = new PvpMatchmakingService(new DbConnectionFactory(configuration));
        var result = SoloWin(a) with
        {
            UseServerAiOpponent = false,
            Runs = new[]
            {
                new BattleRunResultRecord(PlayerIdDto.Player, OnlineBattleSeatId.PlayerA, a.AccountId.ToString(), a.RunId.ToString(), a.DeckId.ToString(), true),
                new BattleRunResultRecord(PlayerIdDto.AI, OnlineBattleSeatId.PlayerB, b.AccountId.ToString(), b.RunId.ToString(), b.DeckId.ToString(), false),
            }
        };
        await SetupMatch(result);
        var before = await Scores();
        // Force the second UPDATE to fail, after the first run UPDATE and receipt INSERT.
        var secondRun = result.Runs.OrderBy(r => r.RunId, StringComparer.Ordinal).Last().RunId;
        await Sql($"""
            create function authority_fail_second_run() returns trigger language plpgsql as $body$
            begin if new.id = '{Guid.Parse(secondRun)}'::uuid then raise exception 'synthetic second run failure'; end if;
            return new; end; $body$;
            create trigger authority_fail_second_run before update on draft_runs
            for each row execute function authority_fail_second_run();
            """);
        try
        {
            var failed = false;
            try { await store.ApplyAsync(result, ct); } catch (PostgresException ex) when (ex.SqlState == "P0001") { failed = true; }
            Require(failed, "Injected second-run failure must be observed.");
            Require(await Scores() == before && await Receipts(result.ResultId) == 0,
                "Both runs and the receipt must roll back together.");
            Require(await MatchState(result.MatchId) == "active", "Failed run persistence must not complete the PVP match.");
        }
        finally { await Sql("drop trigger authority_fail_second_run on draft_runs; drop function authority_fail_second_run();"); }
        Require(await store.ApplyAsync(result, ct), "Retry after transaction failure must succeed.");
        Require(await MatchState(result.MatchId) == "completed", "Match and runs must commit together.");
        pass("second-player SQL failure rolls back both run updates and receipt; retry atomically completes runs and PVP match");

        before = await Scores();
        var repeats = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => store.ApplyAsync(result, ct)));
        Require(repeats.All(applied => !applied) && await Scores() == before && await Receipts(result.ResultId) == 1,
            "Concurrent replays must not increment either run again.");
        var concurrentNew = SoloWin(a);
        repeats = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => store.ApplyAsync(concurrentNew, ct)));
        Require(repeats.Count(applied => applied) == 1 && await Receipts(concurrentNew.ResultId) == 1,
            "Concurrent first deliveries must apply exactly once.");
        before = await Scores();
        var conflict = false;
        try { await store.ApplyAsync(result with { MatchId = "changed-match" }, ct); }
        catch (InvalidOperationException) { conflict = true; }
        Require(conflict && await Scores() == before, "A receipt ID reused with a different payload must fail closed.");
        pass("12 concurrent first deliveries and 12 replays apply once; conflicting reuse of the result ID is rejected");

        var draw = result with { ResultId = Guid.NewGuid(), MatchId = "delivery-draw-" + Guid.NewGuid().ToString("N"),
            IsDraw = true, WinnerSeat = OnlineBattleSeatId.None, Runs = result.Runs.Select(r => r with { Won = false }).ToArray() };
        await SetupMatch(draw);
        before = await Scores();
        Require(await store.ApplyAsync(draw, ct) && !await store.ApplyAsync(draw, ct), "Draw result is idempotent.");
        Require(await Scores() == before && await MatchState(draw.MatchId) == "completed", "Draw must finish the match without a run increment.");
        await using (var drawPlayers = new NpgsqlCommand("""
            select count(*) from pvp_match_players p join pvp_matches m on p.match_id=m.id
            where m.match_id=@match and p.final_result='draw' and p.player_status='draw';
            """, db))
        {
            drawPlayers.Parameters.AddWithValue("match", draw.MatchId);
            Require((long)(await drawPlayers.ExecuteScalarAsync(ct))! == 2, "Both persisted seats must have draw status.");
        }
        pass("draw completion is durable and replayable without changing either run's wins or losses");

        var commitLost = SoloWin(a);
        var directory = NewDirectory("commit-response-lost");
        using (var outbox = Outbox(new ThrowAfterCommitStore(store), configuration, directory))
        {
            outbox.Capture(commitLost);
            Require(File.Exists(Journal(directory, commitLost.ResultId)), "Capture must persist before DB delivery.");
            Require(!await outbox.DeliverAsync(commitLost.ResultId, ct), "Simulated commit-response loss must retain the result.");
            Require(await Receipts(commitLost.ResultId) == 1 && File.Exists(Journal(directory, commitLost.ResultId)),
                "The committed result must retain a replay journal after response loss.");
        }
        before = await Scores();
        await ReplayProcess(directory);
        Require(await Scores() == before && !File.Exists(Journal(directory, commitLost.ResultId)),
            "A new process must remove a committed journal without paying the result twice.");
        pass("commit succeeds but response is lost: journal survives and a new process safely replays the DB receipt");

        var unavailableConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DbConnectionFactory.ConnectionStringKey] = "Host=127.0.0.1;Port=1;Database=unavailable_synthetic;Username=synthetic;Timeout=1;Pooling=false"
        }).Build();
        var unavailableStore = new BattleResultStore(new DbConnectionFactory(unavailableConfiguration));
        var outage = SoloWin(a);
        directory = NewDirectory("database-outage");
        before = await Scores();
        using (var outbox = Outbox(unavailableStore, configuration, directory))
        {
            outbox.Capture(outage);
            Require(!await outbox.DeliverAsync(outage.ResultId, ct), "Unavailable loopback DB must fail delivery.");
            Require(await Scores() == before && File.Exists(Journal(directory, outage.ResultId)),
                "DB connection failure must leave a durable unapplied result.");
        }
        await ReplayProcess(directory);
        Require(await Receipts(outage.ResultId) == 1 && !File.Exists(Journal(directory, outage.ResultId)),
            "DB recovery in a fresh process must apply and acknowledge the pending result.");
        pass("actual DB connection failure retains the result on disk; a fresh process with restored DB access applies it");

        directory = NewDirectory("battle-engine-capture");
        BattleResult ended;
        using (var outbox = Outbox(unavailableStore, configuration, directory))
        {
            var session = new BattleSession("delivery-engine-" + Guid.NewGuid().ToString("N"), true, resultOutbox: outbox);
            var connection = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket())
            { AccountId = a.AccountId.ToString(), RunId = a.RunId.ToString(), DeckId = a.DeckId.ToString(), PlayerToken = "synthetic" };
            connection.PlayerDeckCardIds.AddRange(a.Cards);
            Require(session.TryAddConnection(connection, out var error), error);
            Require(session.TryStartBattleIfReady(out error), error);
            session.ApplyCommand(new ClientBattleCommandMessage
            { MatchId=session.MatchId, ActorId=connection.AssignedSeatId, PlayerToken=connection.PlayerToken,
                CommandType=OnlineBattleCommandType.Surrender, Sequence=1 });
            ended = session.GetFinalResult()!;
            Require(ended.EndedReason == "forfeit" && File.Exists(Journal(directory, ended.ResultId)),
                "The shared engine must journal its terminal result before any socket sends or caller delivery.");
            var snapshot = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(
                JsonSerializer.Serialize(session.CreatePersistenceSnapshot("ended")))!;
            var restored = new BattleSession(snapshot.MatchId, true);
            Require(restored.RestoreFromPersistenceSnapshot(snapshot) &&
                restored.GetFinalResult()!.ToJson() == ended.ToJson(), "Snapshot restore must retain the exact result ID, binding and outcome.");
            var reusedName = new BattleSession(session.MatchId, true);
            Require(reusedName.CreatePersistenceSnapshot("new").ResultId != ended.ResultId,
                "A reused room name must not reuse a server result ID.");
            session.RemoveConnection(connection.ConnectionId);
        }
        await ReplayProcess(directory);
        Require(await Receipts(ended.ResultId) == 1, "Result must survive the session disappearing and process restart.");
        pass("engine journals surrender before network I/O; snapshot keeps result ID; replay survives loss of the session");

        directory = NewDirectory("disconnect-forfeit");
        using (var outbox = Outbox(store, configuration, directory))
        {
            var session = new BattleSession("delivery-disconnect-" + Guid.NewGuid().ToString("N"), false, resultOutbox: outbox);
            var connections = new[] { a, b }.Select(account =>
            {
                var c = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket())
                { AccountId=account.AccountId.ToString(), RunId=account.RunId.ToString(), DeckId=account.DeckId.ToString(), PlayerToken="synthetic" };
                c.PlayerDeckCardIds.AddRange(account.Cards); return c;
            }).ToArray();
            foreach (var c in connections) Require(session.TryAddConnection(c, out var error), error);
            Require(session.TryStartBattleIfReady(out var startError), startError);
            var legacy = session.CreatePersistenceSnapshot("legacy-active") with { ResultId = Guid.Empty };
            var firstRestore = new BattleSession(session.MatchId, false);
            var secondRestore = new BattleSession(session.MatchId, false);
            Require(firstRestore.RestoreFromPersistenceSnapshot(legacy) && secondRestore.RestoreFromPersistenceSnapshot(legacy) &&
                firstRestore.CreatePersistenceSnapshot("check").ResultId == secondRestore.CreatePersistenceSnapshot("check").ResultId,
                "Repeated legacy active-snapshot recovery must derive a stable result ID.");
            Require(session.TryCreateDisconnectReconnectGraceEnvelope(connections[1], TimeSpan.FromMilliseconds(1), out _,
                out var seat, out var identity, out var deadline), "Disconnect must reserve the loser seat.");
            session.RemoveConnection(connections[1].ConnectionId);
            await Task.Delay(10, ct);
            Require(session.TryCreateExpiredDisconnectForfeitEnvelope(seat, identity, deadline, out _), "Expired reconnect must resolve forfeiture.");
            ended = session.GetFinalResult()!;
            Require(ended.EndedReason == "reconnect_timeout" && File.Exists(Journal(directory, ended.ResultId)),
                "Disconnect forfeiture must synchronously journal its outcome.");
            var oldEnded = session.CreatePersistenceSnapshot("unsafe-legacy-ended") with { ResultId = Guid.Empty };
            var rejected = false;
            try { new BattleSession(session.MatchId, false).RestoreFromPersistenceSnapshot(oldEnded); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Legacy ended results without receipt identity must not be replayed automatically.");
        }
        await ReplayProcess(directory);
        Require(await Receipts(ended.ResultId) == 1, "Disconnect-forfeit journal must replay once.");
        pass("disconnect timeout journals and replays its result; legacy active recovery is stable and unsafe old ended results are rejected");

        directory = NewDirectory("background-retry");
        var automatic = SoloWin(a);
        var corruptPath = Journal(directory, Guid.NewGuid());
        File.WriteAllText(corruptPath, "{broken");
        using (var outbox = Outbox(store, configuration, directory))
        {
            outbox.Capture(automatic);
            await outbox.StartAsync(ct);
            try
            {
                for (var i = 0; i < 40 && File.Exists(Journal(directory, automatic.ResultId)); i++) await Task.Delay(100, ct);
                Require(await Receipts(automatic.ResultId) == 1 && !File.Exists(Journal(directory, automatic.ResultId)),
                    "Hosted worker must deliver without another client message.");
                Require(File.Exists(corruptPath), "Malformed journal must remain available for repair.");
            }
            finally { await outbox.StopAsync(ct); }
        }
        pass("hosted retry worker delivers without client activity; corrupt journal is retained and does not block valid results");

        async Task SetupMatch(BattleResult battle)
        {
            foreach (var r in battle.Runs)
                await matchmaking.RecordConnectionJoinedAsync(new PvpMatchmakingConnectionRecord(battle.MatchId,
                    r.AccountId, Guid.NewGuid().ToString("N"), r.OnlineSeatId.ToString(), r.SeatId.ToString(),
                    r.RunId, r.DeckId, "0.1.0-dev", "127.0.0.1", "synthetic-result-check"), ct);
            await matchmaking.RecordBattleStartedAsync(battle.MatchId, ct);
        }
        async Task<string> Scores()
        {
            await using var command = new NpgsqlCommand("select jsonb_agg(to_jsonb(r) order by id)::text from draft_runs r where id=@a or id=@b;", db);
            command.Parameters.AddWithValue("a", a.RunId); command.Parameters.AddWithValue("b", b.RunId);
            return (string)(await command.ExecuteScalarAsync(ct))!;
        }
        async Task<long> Receipts(Guid id)
        {
            await using var command = new NpgsqlCommand("select count(*) from battle_result_receipts where result_id=@id;", db);
            command.Parameters.AddWithValue("id", id);
            return (long)(await command.ExecuteScalarAsync(ct))!;
        }
        async Task<string> MatchState(string id)
        {
            await using var command = new NpgsqlCommand("select match_status from pvp_matches where match_id=@id;", db);
            command.Parameters.AddWithValue("id", id);
            return (string)(await command.ExecuteScalarAsync(ct))!;
        }
        async Task Sql(string sql) { await using var command = new NpgsqlCommand(sql, db); await command.ExecuteNonQueryAsync(ct); }
        async Task ReplayProcess(string path)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--replay-results");
            start.Environment["PROJECT333_BATTLE_RESULT_OUTBOX_DIR"] = path;
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            File.WriteAllText(Path.Combine(path, "replay-process.log"), await stdout + await stderr);
            Require(process.ExitCode == 0, "Isolated replay process failed; inspect its retained log.");
        }
    }

    public static async Task ReplayInChild(string connectionString, string dataPath, CancellationToken ct)
    {
        var path = Path.GetFullPath(Environment.GetEnvironmentVariable("PROJECT333_BATTLE_RESULT_OUTBOX_DIR") ?? "");
        Require(path.StartsWith(Path.GetDirectoryName(Path.GetFullPath(dataPath))! + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Replay must stay inside this synthetic DB's artifact directory.");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [DbConnectionFactory.ConnectionStringKey] = connectionString, ["PROJECT333_BATTLE_RESULT_OUTBOX_DIR"] = path }).Build();
        using var outbox = new BattleResultOutbox(new BattleResultStore(new DbConnectionFactory(configuration)),
            configuration, NullLogger<BattleResultOutbox>.Instance);
        await outbox.RetryPendingAsync(ct);
        Require(!Directory.EnumerateFiles(path, "*.json").Any(), "Child replay must acknowledge every expected valid result.");
        Console.WriteLine("PASS: independent process replayed and acknowledged its durable results.");
    }

    static string NewDirectory(string label)
    {
        var parent = Path.GetDirectoryName(Environment.GetEnvironmentVariable("AFTER333_AUTHORITY_TEST_DATA"))!;
        var path = Path.Combine(parent, label + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    static string Journal(string path, Guid id) => Path.Combine(path, id.ToString("N") + ".json");
    static BattleResultOutbox Outbox(IBattleResultStore store, IConfiguration configuration, string path) =>
        new(store, new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection(
            new Dictionary<string, string?> { ["PROJECT333_BATTLE_RESULT_OUTBOX_DIR"] = path }).Build(),
            NullLogger<BattleResultOutbox>.Instance);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    sealed class ThrowAfterCommitStore(IBattleResultStore inner) : IBattleResultStore
    {
        public bool IsConfigured => inner.IsConfigured;
        public async Task<bool> ApplyAsync(BattleResult result, CancellationToken cancellationToken)
        {
            await inner.ApplyAsync(result, cancellationToken);
            throw new IOException("Synthetic connection loss after the DB committed.");
        }
    }
}
