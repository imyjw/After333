using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;

// The harness verifies its freshly created synthetic PostgreSQL cluster before
// calling this fixture. No real tokens or live connection strings are needed.
static class RunRewardReplayChecks
{
    public static async Task Run(NpgsqlConnection db, IConfiguration configuration,
        Action<string> pass, CancellationToken ct)
    {
        var marker = "reward-replay-" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(configuration[DbConnectionFactory.ConnectionStringKey]!)
        { ApplicationName = marker };
        IConfiguration Config(params (string Key, string Value)[] values)
        {
            var entries = new Dictionary<string, string?>
            { [DbConnectionFactory.ConnectionStringKey] = cs.ConnectionString };
            foreach (var value in values) entries[value.Key] = value.Value;
            return new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
        }
        RunStartService Service(IConfiguration? config = null) => new(new DbConnectionFactory(config ?? Config()), config ?? Config());
        Task<ClaimRunRewardsResponse> Claim(Fixture f, RunStartService? service = null, CancellationToken? token = null) =>
            (service ?? Service()).ClaimRunRewardsAsync(f.Account, new ClaimRunRewardsRequest(f.RunId.ToString()), token ?? ct);

        var pending = await Seed("before-chest-click", 12, 3);
        var pendingBefore = await Snapshot(pending.Account.Account.Id);
        var restored = await Service().GetLatestRunStateForAccountAsync(pending.Account.Account.Id, ct);
        Require(restored.LatestRun?.Id == pending.RunId && restored.LatestRun.Status == "completed" &&
            restored.LatestRun.RewardClaimedAt == null && await Snapshot(pending.Account.Account.Id) == pendingBefore,
            "An unopened chest must stay unclaimed across a new service/account read.");
        await ExpectError(() => Service().StartDraftRunAsync(pending.Account, new StartRunRequest("pve"), ct), "unclaimed_run_rewards");
        Require(await Snapshot(pending.Account.Account.Id) == pendingBefore, "A new run before claim debited a ticket or changed pending rewards.");
        pass("quit-before-chest-click-restores-unclaimed-run-and-blocks-new-ticket-charge");

        foreach (var (wins, losses) in new[] { (33, 0), (4, 3), (0, 3) })
        {
            var f = await Seed("terminal-" + wins + "-" + losses, wins, losses);
            var claim = await Claim(f);
            Require(claim.Run.Id == f.RunId && claim.Run.RewardClaimedAt.HasValue &&
                claim.Reward.ResourceGoldDelta == wins * 3 && claim.Reward.TicketDelta == 0 &&
                claim.Reward.CardRewards.Values.Sum() == wins * 3 &&
                claim.Wallet.ResourceGold == 100 + wins * 3 && claim.Wallet.Tickets == 2,
                "Claim did not use the authoritative terminal run totals.");
            await AssertPersisted(f, claim);
            var committed = await Snapshot(f.Account.Account.Id);
            var replay = await Claim(f); // Recreated service simulates a lost reply and restart.
            Require(SameReceipt(claim.Reward, replay.Reward) &&
                replay.Run == claim.Run && replay.Wallet == claim.Wallet &&
                await Snapshot(f.Account.Account.Id) == committed,
                "Replay rerolled the receipt or wrote a second grant/wallet/card/ledger change.");
            pass("terminal-" + wins + "-" + losses + "-atomic-once-only-payment-and-original-receipt-replay");
        }

        var original = await Seed("original-config-and-items", 1, 3);
        var originalConfig = Config(("PROJECT333_RUN_REWARD_RESOURCE_GOLD_BASE", "7"),
            ("PROJECT333_RUN_REWARD_TICKETS_BASE", "2"), ("PROJECT333_RUN_REWARD_CARD_REWARDS", "firebolt:2"),
            ("PROJECT333_RUN_REWARD_PACK_REWARDS", "recovery_test_pack:2"));
        var paid = await Claim(original, Service(originalConfig));
        Require(paid.Reward.ResourceGoldDelta == 10 && paid.Reward.TicketDelta == 2 &&
            paid.Reward.CardRewards.Values.Sum() == 5 && paid.Reward.PackRewards["recovery_test_pack"] == 2,
            "Original configured card/pack reward was not saved.");
        await Sql("update user_wallets set resource_gold=0,tickets=0 where account_id=@id; update user_card_collection set copy_count=7 where account_id=@id and card_id='firebolt';",
            ("id", original.Account.Account.Id));
        var afterSpending = await Snapshot(original.Account.Account.Id);
        var changedConfig = Config(("PROJECT333_RUN_REWARD_RESOURCE_GOLD_BASE", "9999"),
            ("PROJECT333_RUN_REWARD_TICKETS_BASE", "9999"), ("PROJECT333_RUN_REWARD_CARD_REWARDS", "Goblin:9999"),
            ("PROJECT333_RUN_REWARD_PACK_REWARDS", "different_pack:9999"));
        var replayPaid = await Claim(original, Service(changedConfig));
        Require(SameReceipt(paid.Reward, replayPaid.Reward) &&
            replayPaid.Wallet.ResourceGold == 0 && replayPaid.Wallet.Tickets == 0 &&
            await Snapshot(original.Account.Account.Id) == afterSpending,
            "Replay used changed reward configuration, stale wallet or restored spent copies.");
        Require(replayPaid.Reward.CardRewardItems.Sum(i => i.Count) == 5 &&
            replayPaid.Reward.PackRewardItems.Single().Id == "recovery_test_pack",
            "Receipt replay did not reconstruct the original display item arrays.");
        pass("lost-reply-after-spending-keeps-original-gold-card-pack-receipt-and-current-zero-wallet");

        var concurrent = await Seed("concurrent-clicks", 3, 3);
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Claim(concurrent)));
        Require(claims.Select(c => c.Reward.Id).Distinct().Count() == 1 &&
            claims.All(c => SameReceipt(c.Reward, claims[0].Reward)) &&
            claims.All(c => c.Wallet.ResourceGold == 109), "Concurrent clicks received different rolls or balances.");
        await AssertPersisted(concurrent, claims[0]);
        pass("eight-concurrent-independent-claims-share-one-stored-receipt-and-one-payment");

        var foreign = await Seed("foreign-account", 2, 3);
        var ownershipBefore = await Snapshot(original.Account.Account.Id);
        var foreignBefore = await Snapshot(foreign.Account.Account.Id);
        await ExpectError(() => Service().ClaimRunRewardsAsync(foreign.Account,
            new ClaimRunRewardsRequest(original.RunId.ToString()), ct), "run_not_found");
        Require(await Snapshot(original.Account.Account.Id) == ownershipBefore &&
            await Snapshot(foreign.Account.Account.Id) == foreignBefore, "A different account read or mutated the reward.");
        pass("receipt-replay-remains-authenticated-account-and-run-scoped");

        var active = await Seed("active-run", 2, 0, "in_progress");
        var activeBefore = await Snapshot(active.Account.Account.Id);
        await ExpectError(() => Claim(active), "run_not_completed");
        await ExpectError(() => Service().ClaimRunRewardsAsync(active.Account, new ClaimRunRewardsRequest("not-a-guid"), ct), "invalid_run_id");
        Require(await Snapshot(active.Account.Account.Id) == activeBefore, "An active or invalid run claim changed state.");
        pass("active-run-and-invalid-id-cannot-unlock-or-pay-rewards");

        var missing = await Seed("legacy-missing-receipt", 4, 3);
        await Sql("update draft_runs set reward_claimed_at=now() where id=@run", ("run", missing.RunId));
        var missingBefore = await Snapshot(missing.Account.Account.Id);
        await ExpectError(() => Claim(missing), "reward_receipt_unavailable");
        Require(await Snapshot(missing.Account.Account.Id) == missingBefore, "A missing receipt caused a replacement payout.");
        pass("claimed-legacy-run-with-missing-receipt-fails-closed-without-replacement-reward");

        foreach (var invalid in new[] { "[]", "{\"firebolt\":-1}", "{\"firebolt\":0}", "{\"firebolt\":\"3\"}", "{\"firebolt\":2147483648}", "{\"firebolt\":1,\"FIREBOLT\":2}" })
        {
            await Sql("update reward_grants set card_rewards=cast(@json as jsonb) where id=@grant", ("json", invalid), ("grant", paid.Reward.Id));
            var corruptBefore = await Snapshot(original.Account.Account.Id);
            await ExpectError(() => Claim(original), "reward_receipt_unavailable");
            Require(await Snapshot(original.Account.Account.Id) == corruptBefore, "A malformed stored item map caused writes.");
        }
        await Sql("update reward_grants set card_rewards=cast(@json as jsonb) where id=@grant", ("json", JsonSerializer.Serialize(paid.Reward.CardRewards)), ("grant", paid.Reward.Id));
        pass("corrupt-receipt-map-shape-counts-and-case-duplicate-ids-fail-closed");

        await Sql("insert into reward_grants(account_id,source_type,source_id) values(@id,'run_end',@run)",
            ("id", original.Account.Account.Id), ("run", original.RunId));
        var duplicateBefore = await Snapshot(original.Account.Account.Id);
        await ExpectError(() => Claim(original), "reward_receipt_unavailable");
        Require(await Snapshot(original.Account.Account.Id) == duplicateBefore, "Ambiguous legacy receipts must not be chosen or rerolled.");
        pass("ambiguous-duplicate-legacy-receipts-fail-closed");

        var rollback = await Seed("final-write-failure", 6, 3);
        var rollbackBefore = await Snapshot(rollback.Account.Account.Id);
        await Sql("""
            create function reward_replay_fail_final() returns trigger language plpgsql as $$
            begin raise exception 'synthetic final claim write failure'; end $$;
            """);
        await Sql($"""
            create trigger reward_replay_fail_final before update on draft_runs for each row
            when (new.id='{rollback.RunId}'::uuid and new.reward_claimed_at is not null)
            execute function reward_replay_fail_final();
            """);
        try
        {
            try { await Claim(rollback); throw new InvalidOperationException("Expected synthetic final-write failure."); }
            catch (PostgresException ex) when (ex.SqlState == "P0001") { }
            Require(await Snapshot(rollback.Account.Account.Id) == rollbackBefore,
                "A final run write failure left partial wallet/card/pack/grant/ledger changes.");
        }
        finally { await Sql("drop trigger reward_replay_fail_final on draft_runs; drop function reward_replay_fail_final();"); }
        await AssertPersisted(rollback, await Claim(rollback));
        pass("final-claim-write-failure-rolls-back-all-payment-data-and-later-retry-pays-once");

        var canceled = await Seed("cancel-lock-wait", 6, 3);
        var canceledBefore = await Snapshot(canceled.Account.Account.Id);
        await using var holder = new NpgsqlConnection(cs.ConnectionString);
        await holder.OpenAsync(ct);
        await using (var block = await holder.BeginTransactionAsync(ct))
        {
            await using (var hold = new NpgsqlCommand("select id from draft_runs where id=@id for update", holder, block))
            { hold.Parameters.AddWithValue("id", canceled.RunId); await hold.ExecuteScalarAsync(ct); }
            using var canceledToken = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var waiting = Claim(canceled, token: canceledToken.Token);
            try
            {
                for (var i = 0; ; i++)
                {
                    var blocked = (long)(await Scalar("select count(*) from pg_stat_activity where application_name=@marker and wait_event_type='Lock'", ("marker", marker)))!;
                    if (blocked > 0) break;
                    Require(i < 100, "The claim did not reach its owned run row lock.");
                    await Task.Delay(20, ct);
                }
                canceledToken.Cancel();
                try { await waiting; throw new InvalidOperationException("Canceled lock wait succeeded."); }
                catch (OperationCanceledException) { }
            }
            finally { canceledToken.Cancel(); await block.RollbackAsync(CancellationToken.None); }
        }
        Require(await Snapshot(canceled.Account.Account.Id) == canceledBefore, "Canceled claim changed reward state.");
        await AssertPersisted(canceled, await Claim(canceled));
        pass("canceling-an-inflight-run-lock-wait-keeps-chest-claimable-and-eventual-payment-once");

        async Task<Fixture> Seed(string label, int wins, int losses, string status = "completed")
        {
            var id = Guid.NewGuid(); var run = Guid.NewGuid();
            await Sql("""
                insert into accounts(id,display_name,account_kind) values(@id,@name,'registered');
                insert into user_wallets(account_id,resource_gold,tickets) values(@id,100,2);
                insert into user_card_collection(account_id,card_id,copy_count,upgrade_level) values(@id,'firebolt',11,4);
                insert into draft_runs(id,account_id,status,mode,wins,losses) values(@run,@id,@status,'pve',@wins,@losses);
                """, ("id", id), ("run", run), ("name", "synthetic-reward-" + label), ("status", status), ("wins", wins), ("losses", losses));
            return new Fixture(new AuthenticatedAccount(new AuthAccountDto(id, "synthetic-reward-" + label, "registered"),
                new WalletDto(100, 2), new CollectionSummaryDto(1, new[] { new OwnedCardDto("firebolt", 11, 4) })), run);
        }
        async Task AssertPersisted(Fixture f, ClaimRunRewardsResponse response)
        {
            var id = f.Account.Account.Id;
            Require((long)(await Scalar("select count(*) from reward_grants where account_id=@id and source_type='run_end' and source_id=@run", ("id", id), ("run", f.RunId)))! == 1 &&
                (long)(await Scalar("select count(*) from account_transactions where account_id=@id and transaction_type='reward' and source_id=@grant and card_id is null and pack_id is null", ("id", id), ("grant", response.Reward.Id)))! ==
                    (response.Reward.ResourceGoldDelta == 0 && response.Reward.TicketDelta == 0 ? 0 : 1) &&
                (long)(await Scalar("select coalesce(sum(copy_count),0)::bigint from user_card_collection where account_id=@id", ("id", id)))! == 11 + response.Reward.CardRewards.Values.Sum() &&
                (long)(await Scalar("select resource_gold from user_wallets where account_id=@id", ("id", id)))! == response.Wallet.ResourceGold,
                "Actual persisted grant, ledger, collection or wallet does not match a single receipt.");
        }
        async Task<string> Snapshot(Guid id) => (string)(await Scalar("""
            select jsonb_build_object(
              'wallet',(select to_jsonb(t) from user_wallets t where account_id=@id),
              'runs',(select jsonb_agg(to_jsonb(t) order by id) from draft_runs t where account_id=@id),
              'cards',(select jsonb_agg(to_jsonb(t) order by card_id) from user_card_collection t where account_id=@id),
              'packs',(select jsonb_agg(to_jsonb(t) order by pack_id) from user_pack_inventory t where account_id=@id),
              'grants',(select jsonb_agg(to_jsonb(t) order by id) from reward_grants t where account_id=@id),
              'ledger',(select jsonb_agg(to_jsonb(t) order by id) from account_transactions t where account_id=@id))::text;
            """, ("id", id)))!;
        async Task Sql(string sql, params (string Key, object Value)[] values)
        { await using var command = Command(sql, values); await command.ExecuteNonQueryAsync(ct); }
        async Task<object?> Scalar(string sql, params (string Key, object Value)[] values)
        { await using var command = Command(sql, values); return await command.ExecuteScalarAsync(ct); }
        NpgsqlCommand Command(string sql, params (string Key, object Value)[] values)
        {
            var command = new NpgsqlCommand(sql, db);
            foreach (var value in values) command.Parameters.AddWithValue(value.Key, value.Value);
            return command;
        }
    }

    static async Task ExpectError<T>(Func<Task<T>> action, string code)
    {
        try { await action(); throw new InvalidOperationException("Expected rejection: " + code); }
        catch (RunServiceException ex) when (ex.Code == code) { }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static bool SameReceipt(RewardGrantDto a, RewardGrantDto b) => a.Id == b.Id &&
        a.SourceType == b.SourceType && a.SourceId == b.SourceId && a.CreatedAt == b.CreatedAt &&
        a.ResourceGoldDelta == b.ResourceGoldDelta && a.TicketDelta == b.TicketDelta &&
        a.CardRewards.OrderBy(i => i.Key, StringComparer.Ordinal).SequenceEqual(b.CardRewards.OrderBy(i => i.Key, StringComparer.Ordinal)) &&
        a.PackRewards.OrderBy(i => i.Key, StringComparer.Ordinal).SequenceEqual(b.PackRewards.OrderBy(i => i.Key, StringComparer.Ordinal)) &&
        a.CardRewardItems.SequenceEqual(b.CardRewardItems) && a.PackRewardItems.SequenceEqual(b.PackRewardItems);
    sealed record Fixture(AuthenticatedAccount Account, Guid RunId);
}
