using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.BattleResults;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Challenges;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;
using Project333.Runtime.Application.Online;

static class AiChallengeChecks
{
    private sealed record Fixture(Guid Account, Guid Run, Guid Deck);

    public static async Task Run(NpgsqlConnection db, IConfiguration configuration, Action<string> pass, CancellationToken ct)
    {
        Require(db.Database == "after333_run_authority" && db.Host == "127.0.0.1" && db.Port == 15439,
            "AI challenge checks require the isolated authority database.");
        var service = new AiChallengeService(new DbConnectionFactory(configuration));
        var store = new BattleResultStore(new DbConnectionFactory(configuration));
        var beforeMidnight = AiChallengeService.GetDayWindow(DateTimeOffset.Parse("2026-09-13T14:59:59.9999999Z"));
        var midnight = AiChallengeService.GetDayWindow(DateTimeOffset.Parse("2026-09-13T15:00:00Z"));
        Require(beforeMidnight.Day == new DateOnly(2026, 9, 13) && midnight.Day == new DateOnly(2026, 9, 14)
            && beforeMidnight.ResetsAtUtc == DateTimeOffset.Parse("2026-09-13T15:00:00Z")
            && midnight.ResetsAtUtc == DateTimeOffset.Parse("2026-09-14T15:00:00Z"),
            "The three-attempt day must roll exactly at Korean midnight.");
        pass("AI challenge calendar changes at 00:00 KST and supplies the corresponding UTC reset time");

        var owner = await Create(db, ct);
        var other = await Create(db, ct);
        var request = Request(owner);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.StartAsync(owner.Account, request, ct)));
        var first = responses[0];
        Require(responses.All(x => x.AttemptId == first.AttemptId && x.RemainingAttempts == 2)
            && responses.Count(x => !x.Replayed) == 1, "Concurrent button requests must consume one daily attempt.");
        var secondClick = Request(owner);
        var alias = await service.StartAsync(owner.Account, secondClick, ct);
        Require(alias.AttemptId == first.AttemptId && alias.Replayed && alias.RemainingAttempts == 2,
            "A second click with a distinct request ID must reuse the pending attempt.");
        Require(await service.FindAttemptAsync(other.Account, first.AttemptId, ct) == null,
            "Another account must not read the challenge attempt.");
        await Reject(() => service.StartAsync(owner.Account, request with { DeckId = other.Deck.ToString() }, ct),
            "ai_challenge_request_conflict");
        await Reject(() => service.StartAsync(other.Account, request, ct), "ai_challenge_deck_invalid");
        Require((await service.GetStatusAsync(other.Account, ct)).RemainingAttempts == 3,
            "An invalid run/deck must not consume an attempt.");
        pass("parallel starts, distinct double-click keys and request replays use one durable admission; foreign decks fail without charging");

        await service.MarkBattleJoinedAsync(owner.Account, first.AttemptId, first.MatchId, ct);
        var win = Result(owner, first, won: true);
        var wrongRun = win with { ResultId = Guid.NewGuid(), Runs = new[]
            { new BattleRunResultRecord(PlayerIdDto.Player, OnlineBattleSeatId.PlayerA,
                owner.Account.ToString(), other.Run.ToString(), owner.Deck.ToString(), true) } };
        await Invalid(() => store.ApplyAsync(wrongRun, ct));
        Require(await Scalar(db, "select count(*) from battle_result_receipts where result_id=@id", ct, ("id", wrongRun.ResultId)) == 0,
            "A mismatched result receipt must roll back.");
        await Invalid(() => store.ApplyAsync(win with { ResultId = Guid.NewGuid(), AiChallengeAttemptId = null }, ct));
        // Failure after the attempt UPDATE but before wallet/ledger commit rolls everything back.
        await Sql(db, """
            create function ai_challenge_test_fail_credit() returns trigger language plpgsql as $body$
            begin if new.source_table='ai_challenge_attempts' then raise exception 'synthetic challenge credit failure'; end if;
            return new; end; $body$;
            create trigger ai_challenge_test_fail_credit before insert on account_transactions
            for each row execute function ai_challenge_test_fail_credit();
            """, ct);
        try
        {
            var failed = false;
            try { await store.ApplyAsync(win, ct); } catch (PostgresException ex) when (ex.SqlState == "P0001") { failed = true; }
            Require(failed && await Gold(db, owner.Account, ct) == 0
                && (await service.FindAttemptAsync(owner.Account, first.AttemptId, ct))?.Status == "active"
                && await Scalar(db, "select count(*) from battle_result_receipts where result_id=@id", ct, ("id", win.ResultId)) == 0,
                "Attempt completion, receipt, wallet and ledger must roll back together.");
        }
        finally
        {
            await Sql(db, "drop trigger ai_challenge_test_fail_credit on account_transactions; drop function ai_challenge_test_fail_credit();", ct);
        }
        var applied = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.ApplyAsync(win, ct)));
        var settled = await service.FindAttemptAsync(owner.Account, first.AttemptId, ct);
        Require(applied.Count(x => x) == 1 && await Gold(db, owner.Account, ct) == 10
            && settled is { Status: "completed", RewardGold: 10, Won: true } && settled.ResultId == win.ResultId
            && await Scalar(db, "select count(*) from account_transactions where account_id=@account and source_table='ai_challenge_attempts'", ct,
                ("account", owner.Account)) == 1, "A victory must grant exactly ten gold once, including concurrent result replays.");
        Require((await service.StartAsync(owner.Account, secondClick, ct)) is { Replayed: true, Status: "completed", RemainingAttempts: 2 },
            "The second click's persisted alias must still replay after completion, not charge a new attempt.");
        pass("challenge result bindings and transaction rollback are enforced; concurrent winning-result delivery pays exactly 10 gold once");

        var second = await service.StartAsync(owner.Account, Request(owner), ct);
        await service.MarkBattleJoinedAsync(owner.Account, second.AttemptId, second.MatchId, ct);
        await store.ApplyAsync(Result(owner, second, false, draw: true), ct);
        var third = await service.StartAsync(owner.Account, Request(owner), ct);
        await service.MarkBattleJoinedAsync(owner.Account, third.AttemptId, third.MatchId, ct);
        await store.ApplyAsync(Result(owner, third, false, reason: "reconnect_timeout"), ct);
        await Reject(() => service.StartAsync(owner.Account, Request(owner), ct), "ai_challenge_daily_limit");
        Require(await Gold(db, owner.Account, ct) == 10 && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 0,
            "Draw and reconnect-timeout loss must consume attempts and grant no gold.");
        Require(await Scalar(db, "select wins*100+losses from draft_runs where id=@run", ct, ("run", owner.Run)) == 0
            && await Scalar(db, "select count(*) from battle_run_results where account_id=@account", ct, ("account", owner.Account)) == 0
            && await Scalar(db, "select count(*) from reward_grants where account_id=@account", ct, ("account", owner.Account)) == 0,
            "AI challenges must not change run scores, run receipts or run rewards.");
        var restored = new AiChallengeService(new DbConnectionFactory(configuration));
        Require((await restored.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 0,
            "A new service instance must read the persisted daily quota.");
        pass("three admissions exhaust the daily quota; draw/timeout loss pay zero and never modify the normal run or run rewards");

        // Shift only these synthetic fixture rows to the previous bucket to exercise the next-day query.
        await Sql(db, "update ai_challenge_attempts set challenge_day=challenge_day-1 where account_id=@account", ct, ("account", owner.Account));
        Require((await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 3, "A new Korean day starts with three attempts.");
        var canceled = await service.StartAsync(owner.Account, Request(owner), ct);
        await service.MarkBattleJoinedAsync(owner.Account, canceled.AttemptId, canceled.MatchId, ct);
        Require(await service.CancelAsync(owner.Account, canceled.AttemptId, true, "server_cancel", ct)
            && !await service.CancelAsync(owner.Account, canceled.AttemptId, true, "server_cancel", ct)
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 3,
            "A server cancellation refunds exactly its original attempt once.");
        var stale = Result(owner, canceled, true);
        Require(!await store.ApplyAsync(stale, ct)
            && await Scalar(db, "select count(*) from battle_result_receipts where result_id=@id", ct, ("id", stale.ResultId)) == 0
            && await Gold(db, owner.Account, ct) == 10, "Canceled attempts reject stale rewards and roll back their receipt.");
        await Invalid(() => service.CancelAsync(owner.Account, canceled.AttemptId, true, "join_failed", ct));
        var yesterdayPending = await service.StartAsync(owner.Account, Request(owner), ct);
        await Sql(db, "update ai_challenge_attempts set challenge_day=challenge_day-1 where id=@id", ct, ("id", yesterdayPending.AttemptId));
        Require(await service.CancelAsync(owner.Account, yesterdayPending.AttemptId, true, "server_cancel", ct)
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 3,
            "Refunding yesterday's attempt must not add a fourth attempt today.");
        var pendingResult = await service.StartAsync(owner.Account, Request(owner), ct);
        await service.MarkBattleJoinedAsync(owner.Account, pendingResult.AttemptId, pendingResult.MatchId, ct);
        await service.CancelStaleOnStartupAsync(true, ct, new[] { pendingResult.AttemptId });
        Require((await service.FindAttemptAsync(owner.Account, pendingResult.AttemptId, ct))?.Status == "active"
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 2,
            "A durable result awaiting replay must not be canceled/refunded at startup.");
        Require(await service.CancelStaleOnStartupAsync(true, ct) == 1
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 3,
            "A truly unfinished server-startup battle is canceled and refunds its original day.");
        pass("server interruption refunds once in the original day; stale results cannot revive it and unreplayed final results remain protected");
    }

    private static AiChallengeStartRequest Request(Fixture f) => new(Guid.NewGuid(), f.Run.ToString(), f.Deck.ToString());

    private static BattleResult Result(Fixture f, AiChallengeStartResponse attempt, bool won, bool draw = false, string reason = "normal") =>
        new BattleResult(Guid.NewGuid(), attempt.MatchId, true, draw,
            draw ? OnlineBattleSeatId.None : won ? OnlineBattleSeatId.PlayerA : OnlineBattleSeatId.ServerAI, reason,
            new[] { new BattleRunResultRecord(PlayerIdDto.Player, OnlineBattleSeatId.PlayerA,
                f.Account.ToString(), f.Run.ToString(), f.Deck.ToString(), won) })
            { AiChallengeAttemptId = attempt.AttemptId };

    private static async Task<Fixture> Create(NpgsqlConnection db, CancellationToken ct)
    {
        var f = new Fixture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await Sql(db, """
            insert into accounts(id,display_name,account_kind) values (@account,'challenge-test','registered');
            insert into user_wallets(account_id) values (@account);
            insert into draft_runs(id,account_id,status,mode) values (@run,@account,'ready','pve');
            insert into decks(id,account_id,name,deck_type,source_run_id,card_definition_version)
                values (@deck,@account,'challenge-test','draft_run',@run,'test');
            insert into deck_cards(deck_id,slot_index,card_id) select @deck,i,'fixture-card' from generate_series(0,32) i;
            update draft_runs set completed_deck_id=@deck where id=@run;
            """, ct, ("account", f.Account), ("run", f.Run), ("deck", f.Deck));
        return f;
    }

    private static Task<long> Gold(NpgsqlConnection db, Guid account, CancellationToken ct) =>
        Scalar(db, "select resource_gold from user_wallets where account_id=@account", ct, ("account", account));

    private static async Task<long> Scalar(NpgsqlConnection db, string sql, CancellationToken ct, params (string Key, object Value)[] p)
    {
        await using var c = new NpgsqlCommand(sql, db);
        foreach (var v in p) c.Parameters.AddWithValue(v.Key, v.Value);
        return Convert.ToInt64(await c.ExecuteScalarAsync(ct));
    }

    private static async Task Sql(NpgsqlConnection db, string sql, CancellationToken ct, params (string Key, object Value)[] p)
    {
        await using var c = new NpgsqlCommand(sql, db);
        foreach (var v in p) c.Parameters.AddWithValue(v.Key, v.Value);
        await c.ExecuteNonQueryAsync(ct);
    }

    private static async Task Reject(Func<Task> action, string code)
    {
        try { await action(); }
        catch (RunServiceException ex) when (ex.Code == code) { return; }
        throw new Exception("Expected service rejection: " + code);
    }

    private static async Task Invalid(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Expected an invalid result or cancellation rejection.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
