using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Auth;
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
        var owner = await Create(db, ct);
        var other = await Create(db, ct);
        await CheckRunEligibility(db, service, other, ct);
        Require((await service.GetStatusAsync(owner.Account, ct)) is { RunLimit: 3, RemainingAttempts: 3 } initial
            && initial.RunId == owner.Run && initial.PendingAttempt == null,
            "A complete active run must expose its own three-attempt quota.");
        pass("Gold Dungeon requires the current complete run deck; drafting, ended, archived and incomplete decks have no usable quota");

        var request = Request(owner);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.StartAsync(owner.Account, request, ct)));
        var first = responses[0];
        Require(responses.All(x => x.AttemptId == first.AttemptId && x.RemainingAttempts == 2)
            && responses.Count(x => !x.Replayed) == 1, "Concurrent button requests must consume one attempt from this run.");
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
        await Reject(() => service.StartAsync(owner.Account, Request(owner), ct), "ai_challenge_run_limit");
        Require(await Gold(db, owner.Account, ct) == 10 && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 0,
            "Draw and reconnect-timeout loss must consume attempts and grant no gold.");
        Require(await Scalar(db, "select wins*100+losses from draft_runs where id=@run", ct, ("run", owner.Run)) == 0
            && await Scalar(db, "select count(*) from battle_run_results where account_id=@account", ct, ("account", owner.Account)) == 0
            && await Scalar(db, "select count(*) from reward_grants where account_id=@account", ct, ("account", owner.Account)) == 0,
            "AI challenges must not change run scores, run receipts or run rewards.");
        var restored = new AiChallengeService(new DbConnectionFactory(configuration));
        Require((await restored.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 0,
            "A new service instance must read the persisted run quota.");
        pass("three admissions exhaust the run quota; draw/timeout loss pay zero and never modify the normal run or run rewards");

        // Move only synthetic audit dates: crossing a day boundary cannot reset this run's quota.
        await Sql(db, "update ai_challenge_attempts set challenge_day=challenge_day-1, created_at=created_at-interval '1 day' where account_id=@account", ct,
            ("account", owner.Account));
        Require((await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 0,
            "All earlier-day attempts from the same run must still count after midnight.");
        await Reject(() => service.StartAsync(owner.Account, Request(owner), ct), "ai_challenge_run_limit");
        var oldReplay = await service.StartAsync(owner.Account, request, ct);
        Require(oldReplay.Replayed && oldReplay.AttemptId == first.AttemptId && oldReplay.RemainingAttempts == 0,
            "A previous-day request replay must return the original attempt and exhausted original run quota.");
        // Legacy daily usage can already exceed three across days; retain those rows and clamp usable capacity to zero.
        await Sql(db, """
            insert into ai_challenge_attempts(id,account_id,request_id,run_id,deck_id,match_id,challenge_day,status,ended_reason,completed_at)
                values(@id,@account,@request,@run,@deck,@match,current_date-2,'canceled','join_failed',clock_timestamp());
            """, ct, ("id", Guid.NewGuid()), ("account", owner.Account), ("request", Guid.NewGuid()),
            ("run", owner.Run), ("deck", owner.Deck), ("match", "challenge-legacy-" + Guid.NewGuid().ToString("N")));
        Require((await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 0
            && await Scalar(db, "select count(*) from ai_challenge_attempts where account_id=@account and run_id=@run and refunded_at is null", ct,
                ("account", owner.Account), ("run", owner.Run)) == 4,
            "Legacy usage over three must remain intact and expose zero remaining attempts.");
        pass("midnight and service recreation do not restore attempts; legacy run usage over three is retained and clamped to zero");

        var previousRun = owner;
        owner = await StartPaidRun(db, configuration, service, owner, ct);
        Require((await service.GetStatusAsync(owner.Account, ct)) is { RunLimit: 3, RemainingAttempts: 3 } nextRun
            && nextRun.RunId == owner.Run && owner.Run != previousRun.Run,
            "A new paid run on the same day must have three independent attempts.");
        oldReplay = await service.StartAsync(owner.Account, secondClick, ct);
        Require(oldReplay.Replayed && oldReplay.RunId == previousRun.Run && oldReplay.RemainingAttempts == 0
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 3,
            "Replaying an earlier run request must not borrow or consume the new run quota.");
        await Reject(() => service.StartAsync(owner.Account, Request(previousRun), ct), "ai_challenge_deck_invalid");
        pass("a three-ticket run and 33 authoritative draft picks create a fresh quota; completed-run replays remain bound to their original run");

        var canceled = await service.StartAsync(owner.Account, Request(owner), ct);
        await service.MarkBattleJoinedAsync(owner.Account, canceled.AttemptId, canceled.MatchId, ct);
        Require(await service.CancelAsync(owner.Account, canceled.AttemptId, true, "server_cancel", ct)
            && !await service.CancelAsync(owner.Account, canceled.AttemptId, true, "server_cancel", ct)
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 3,
            "A server cancellation refunds exactly its original run attempt once.");
        var stale = Result(owner, canceled, true);
        Require(!await store.ApplyAsync(stale, ct)
            && await Scalar(db, "select count(*) from battle_result_receipts where result_id=@id", ct, ("id", stale.ResultId)) == 0
            && await Gold(db, owner.Account, ct) == 10, "Canceled attempts reject stale rewards and roll back their receipt.");
        await Invalid(() => service.CancelAsync(owner.Account, canceled.AttemptId, true, "join_failed", ct));
        var earlierDayPending = await service.StartAsync(owner.Account, Request(owner), ct);
        await Sql(db, "update ai_challenge_attempts set challenge_day=challenge_day-1 where id=@id", ct, ("id", earlierDayPending.AttemptId));
        Require(await service.CancelAsync(owner.Account, earlierDayPending.AttemptId, true, "server_cancel", ct)
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 3,
            "Refunding an earlier-day attempt restores only its original run and never exceeds three attempts.");
        var pendingResult = await service.StartAsync(owner.Account, Request(owner), ct);
        await service.MarkBattleJoinedAsync(owner.Account, pendingResult.AttemptId, pendingResult.MatchId, ct);
        await service.CancelStaleOnStartupAsync(true, ct, new[] { pendingResult.AttemptId });
        Require((await service.FindAttemptAsync(owner.Account, pendingResult.AttemptId, ct))?.Status == "active"
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 2,
            "A durable result awaiting replay must not be canceled/refunded at startup.");
        Require(await service.CancelStaleOnStartupAsync(true, ct) == 1
            && (await service.GetStatusAsync(owner.Account, ct)).RemainingAttempts == 3,
            "A truly unfinished server-startup battle is canceled and refunds its original run.");
        pass("server interruption refunds once in the original run; stale results cannot revive it and unreplayed final results remain protected");

        // An old run can be canceled after a new run is visible; its refund must not add new-run capacity.
        var originalOtherRun = other;
        var originalOtherRequest = Request(other);
        var originalOtherAttempt = await service.StartAsync(other.Account, originalOtherRequest, ct);
        await Sql(db, "update draft_runs set status='completed',reward_claimed_at=clock_timestamp() where id=@run", ct,
            ("run", other.Run));
        other = await CreateReadyRun(db, other.Account, ct);
        Require((await service.GetStatusAsync(other.Account, ct)).RemainingAttempts == 3,
            "A previous-run pending attempt cannot reduce the current run's independent quota.");
        Require(await service.CancelAsync(other.Account, originalOtherAttempt.AttemptId, true, "server_cancel", ct)
            && (await service.GetStatusAsync(other.Account, ct)).RemainingAttempts == 3,
            "Refunding a previous-run attempt cannot create a fourth current-run attempt.");
        var originalOtherReplay = await service.StartAsync(other.Account, originalOtherRequest, ct);
        Require(originalOtherReplay is { Replayed: true, Status: "canceled", RemainingAttempts: 3 }
            && originalOtherReplay.RunId == originalOtherRun.Run,
            "The canceled old request must retain its old run binding and that run's restored quota.");
        var joinFailed = await service.StartAsync(other.Account, Request(other), ct);
        Require(await service.CancelAsync(other.Account, joinFailed.AttemptId, false, "join_failed", ct)
            && (await service.GetStatusAsync(other.Account, ct)).RemainingAttempts == 2,
            "A join failure without a server refund still consumes one attempt in its original run.");
        pass("previous-run cancellation cannot increase the new run quota; canceled request replay and non-refunded join failures retain their original binding");

    }

    private static AiChallengeStartRequest Request(Fixture f) => new(Guid.NewGuid(), f.Run.ToString(), f.Deck.ToString());

    private static BattleResult Result(Fixture f, AiChallengeStartResponse attempt, bool won, bool draw = false, string reason = "normal") =>
        new BattleResult(Guid.NewGuid(), attempt.MatchId, true, draw,
            draw ? OnlineBattleSeatId.None : won ? OnlineBattleSeatId.PlayerA : OnlineBattleSeatId.ServerAI, reason,
            new[] { new BattleRunResultRecord(PlayerIdDto.Player, OnlineBattleSeatId.PlayerA,
                f.Account.ToString(), f.Run.ToString(), f.Deck.ToString(), won) })
            { AiChallengeAttemptId = attempt.AttemptId };

    private static async Task CheckRunEligibility(NpgsqlConnection db, AiChallengeService service, Fixture f, CancellationToken ct)
    {
        foreach (var status in new[] { "drafting", "completed", "abandoned" })
        {
            await Sql(db, "update draft_runs set status=@status where id=@run", ct, ("status", status), ("run", f.Run));
            var unavailable = await service.GetStatusAsync(f.Account, ct);
            Require(unavailable.RunLimit == 3 && unavailable.RunId == null && unavailable.RemainingAttempts == 0,
                $"A {status} run must not expose a Gold Dungeon quota.");
            await Reject(() => service.StartAsync(f.Account, Request(f), ct), "ai_challenge_deck_invalid");
        }
        await Sql(db, "update draft_runs set status='ready' where id=@run; update decks set is_archived=true where id=@deck;", ct,
            ("run", f.Run), ("deck", f.Deck));
        Require((await service.GetStatusAsync(f.Account, ct)).RunId == null,
            "An archived deck cannot expose a current run quota.");
        await Reject(() => service.StartAsync(f.Account, Request(f), ct), "ai_challenge_deck_invalid");
        await Sql(db, "update decks set is_archived=false where id=@deck; delete from deck_cards where deck_id=@deck and slot_index=32;", ct,
            ("deck", f.Deck));
        Require((await service.GetStatusAsync(f.Account, ct)).RemainingAttempts == 0,
            "An incomplete run deck has no usable quota.");
        await Reject(() => service.StartAsync(f.Account, Request(f), ct), "ai_challenge_deck_invalid");
        await Sql(db, """
            insert into deck_cards(deck_id,slot_index,card_id) values(@deck,32,'fixture-card');
            update draft_runs set status='in_progress' where id=@run;
            insert into draft_runs(account_id,status,mode,started_at) values(@account,'abandoned','pve',clock_timestamp()+interval '1 day');
            """, ct, ("deck", f.Deck), ("run", f.Run), ("account", f.Account));
        Require((await service.GetStatusAsync(f.Account, ct)) is { RemainingAttempts: 3 } usable && usable.RunId == f.Run,
            "An in-progress complete run remains usable even behind newer abandoned history.");
    }

    private static async Task<Fixture> StartPaidRun(NpgsqlConnection db, IConfiguration configuration,
        AiChallengeService challenge, Fixture previous, CancellationToken ct)
    {
        // Lifecycle and ticket top-ups affect only this runner's synthetic fixture account.
        await Sql(db, """
            update draft_runs set status='completed',reward_claimed_at=clock_timestamp() where id=@run;
            update user_wallets set tickets=3 where account_id=@account;
            """, ct, ("run", previous.Run), ("account", previous.Account));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DbConnectionFactory.ConnectionStringKey] = configuration[DbConnectionFactory.ConnectionStringKey],
            ["PROJECT333_DRAFT_RUN_TICKET_COST"] = "3",
        }).Build();
        var runs = new RunStartService(new DbConnectionFactory(config), config);
        var account = new AuthenticatedAccount(new AuthAccountDto(previous.Account, "challenge-test", "registered"),
            new WalletDto(await Gold(db, previous.Account, ct), 3), new CollectionSummaryDto(0, Array.Empty<OwnedCardDto>()));
        var started = await runs.StartDraftRunAsync(account, new StartRunRequest("pve"), ct);
        Require(started.ActiveRun.TicketCostPaid == 3 && started.Wallet.Tickets == 0
            && await Scalar(db, "select count(*) from account_transactions where account_id=@account and source_id=@run and transaction_type='ticket_spend' and ticket_delta=-3", ct,
                ("account", previous.Account), ("run", started.ActiveRun.Id)) == 1,
            "The new run must atomically spend three tickets once.");
        Require((await challenge.GetStatusAsync(previous.Account, ct)) is { RunId: null, RemainingAttempts: 0 },
            "A paid run must finish its draft before Gold Dungeon is available.");
        var offer = started.CurrentOfferCardIds;
        SelectDraftCardResponse? selected = null;
        for (var index = 0; index < 33; index++)
        {
            selected = await runs.SelectAuthoritativeDraftCardAsync(account,
                new SelectDraftCardRequest(started.ActiveRun.Id.ToString(), offer[0], index), ct);
            offer = selected.CurrentOfferCardIds;
        }
        Require(selected is { IsComplete: true, Deck.CardCount: 33 }, "Thirty-three authoritative picks must complete the new run deck.");
        return new Fixture(previous.Account, selected!.ActiveRun.Id, selected.Deck!.Id);
    }

    private static async Task<Fixture> Create(NpgsqlConnection db, CancellationToken ct)
    {
        var account = Guid.NewGuid();
        await Sql(db, """
            insert into accounts(id,display_name,account_kind) values (@account,'challenge-test','registered');
            insert into user_wallets(account_id) values (@account);
            """, ct, ("account", account));
        return await CreateReadyRun(db, account, ct);
    }

    private static async Task<Fixture> CreateReadyRun(NpgsqlConnection db, Guid account, CancellationToken ct)
    {
        var f = new Fixture(account, Guid.NewGuid(), Guid.NewGuid());
        await Sql(db, """
            insert into draft_runs(id,account_id,status,mode,ticket_cost_paid) values (@run,@account,'ready','pve',3);
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
