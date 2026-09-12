using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;

static class RunStartAtomicityChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task Run(HttpClient http, NpgsqlConnection db, IConfiguration configuration, Action<string> pass, CancellationToken ct)
    {
        var a = await Register("start-race");
        var b = await Register("start-failure");
        var marker = "start-race-" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(configuration[DbConnectionFactory.ConnectionStringKey]!) { ApplicationName = marker };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DbConnectionFactory.ConnectionStringKey] = cs.ConnectionString,
            ["PROJECT333_DRAFT_RUN_TICKET_COST"] = "1",
        }).Build();
        RunStartService Service() => new(new DbConnectionFactory(config), config);
        var service = Service();
        var accountA = Account(a); var accountB = Account(b);

        await using var holder = new NpgsqlConnection(cs.ConnectionString);
        await holder.OpenAsync(ct);
        Task<(StartRunResponse? Run, string? Error)>[] attempts;
        await using (var block = await holder.BeginTransactionAsync(ct))
        {
            await LockWallet(holder, block, a.Account.Id, ct);
            attempts = Enumerable.Range(0, 8).Select(i => Start(Service(), accountA, i % 2 == 0 ? "pve" : "pvp", ct)).ToArray();
            try { await WaitBlocked(8); }
            finally { await block.RollbackAsync(CancellationToken.None); }
        }
        var outcomes = await Task.WhenAll(attempts);
        Require(outcomes.Count(o => o.Run != null) == 1 && outcomes.Count(o => o.Error == "active_run_exists") == 7,
            $"Expected one creation and seven active_run_exists rejections; got {outcomes.Count(o => o.Run != null)} creations / {string.Join(',', outcomes.Select(o => o.Error ?? "created"))}.");
        var created = outcomes.Single(o => o.Run != null).Run!;
        Require(created.Wallet.Tickets == a.Wallet.Tickets - 1 && created.CurrentOfferCardIds.Count > 0, "Exactly one ticket must buy one server draft offer.");
        await AssertCounts(a.Account.Id, 1, 1, a.Wallet.Tickets - 1);
        pass("eight starts across independent DB connections serialize before the active-run check: one run, one ticket debit, seven stable conflicts");

        var snapshot = await Snapshot(a.Account.Id);
        // A lost HTTP response must not permit a second creation on retry.
        for (var i = 0; i < 3; i++) await ExpectHttpConflict(a.SessionToken, "active_run_exists");
        Require(await Snapshot(a.Account.Id) == snapshot, "Retries must preserve the entire wallet, run, offer and ticket ledger.");
        pass("HTTP start retries after committed creation return 409 and preserve wallet, initial offer, run and ticket ledger");

        // Different accounts must not share one global start lock; cancellation must return capacity.
        await using (var block = await holder.BeginTransactionAsync(ct))
        {
            await LockWallet(holder, block, a.Account.Id, ct);
            using var canceled = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var waiting = Start(service, accountA, "pve", canceled.Token);
            try
            {
                await WaitBlocked(1);
                var other = await Start(service, accountB, "pve", ct);
                Require(other.Run != null, "An unrelated account must start while the first account is locked.");
                canceled.Cancel();
                try { await waiting; throw new Exception("Canceled start succeeded."); }
                catch (OperationCanceledException) { }
            }
            finally { canceled.Cancel(); await block.RollbackAsync(CancellationToken.None); }
        }
        Require(await Snapshot(a.Account.Id) == snapshot, "Canceled lock wait changed account data.");
        pass("account-scoped locking allows another account to start; canceled lock waits leave no debit or run");

        // SQL fixtures are restricted to the runner's fresh synthetic accounts.
        foreach (var status in new[] { "drafting", "ready", "in_progress" })
        {
            await Sql("update draft_runs set status=@status where id=@run", ("status", status), ("run", created.ActiveRun.Id));
            // A newer abandoned historical row must not hide an older active run.
            await Sql("insert into draft_runs(account_id,status,mode,started_at) values(@id,'abandoned','pve',now()+interval '1 day')", ("id", a.Account.Id));
            snapshot = await Snapshot(a.Account.Id);
            await ExpectHttpConflict(a.SessionToken, "active_run_exists");
            Require(await Snapshot(a.Account.Id) == snapshot, "Hidden active run should block without mutations.");
        }
        await Sql("update draft_runs set status='completed',reward_claimed_at=null where id=@run", ("run", created.ActiveRun.Id));
        snapshot = await Snapshot(a.Account.Id);
        await ExpectHttpConflict(a.SessionToken, "unclaimed_run_rewards");
        Require(await Snapshot(a.Account.Id) == snapshot, "Older unclaimed reward must block regardless of newer abandoned rows.");
        await Sql("update draft_runs set reward_claimed_at=now() where id=@run", ("run", created.ActiveRun.Id));
        var next = await Start(service, accountA, "pve", ct);
        Require(next.Run != null && next.Run.Wallet.Tickets == 0, "Claimed completion must permit the next paid run.");
        pass("all resumable statuses and older unclaimed rewards block new starts even behind newer abandoned history; claimed completion permits a new run");

        // The unique index protects inserts and state transitions outside this service too.
        snapshot = await Snapshot(a.Account.Id);
        await UniqueRejected("insert into draft_runs(account_id,status,mode) values(@id,'drafting','pvp')", a.Account.Id);
        await UniqueRejected("update draft_runs set status='ready' where id=(select id from draft_runs where account_id=@id and status='abandoned' limit 1)", a.Account.Id);
        Require(await Snapshot(a.Account.Id) == snapshot, "Unique violation must leave all rows unchanged.");
        pass("DB partial unique index rejects a second active insert and historical reactivation for the same account across modes");

        // Fail after the wallet debit and run insert, at the ticket-ledger insert.
        await Sql("update draft_runs set status='abandoned' where account_id=@id", ("id", b.Account.Id));
        snapshot = await Snapshot(b.Account.Id);
        await Sql("""
            create function check_fail_start_ledger() returns trigger language plpgsql as $$ begin
                if new.transaction_type='ticket_spend' then raise exception 'synthetic start ledger failure'; end if;
                return new; end; $$;
            create trigger check_fail_start_ledger before insert on account_transactions
                for each row execute function check_fail_start_ledger();
            """);
        try
        {
            try { await service.StartDraftRunAsync(accountB, new StartRunRequest("pve"), ct); throw new Exception("Injected failure did not run."); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.RaiseException) { }
        }
        finally { await Sql("drop trigger check_fail_start_ledger on account_transactions; drop function check_fail_start_ledger();"); }
        Require(await Snapshot(b.Account.Id) == snapshot, "Late insert failure must roll back wallet, run, offer and ledger together.");
        var afterFailure = await Start(service, accountB, "pve", ct);
        Require(afterFailure.Run?.Wallet.Tickets == 0, "A retry after rollback should create exactly one run.");
        await Sql("update draft_runs set status='abandoned' where account_id=@id", ("id", b.Account.Id));
        snapshot = await Snapshot(b.Account.Id);
        await ExpectHttpConflict(b.SessionToken, "insufficient_tickets");
        Require(await Snapshot(b.Account.Id) == snapshot, "Insufficient tickets must leave no empty run or ledger entry.");
        pass("failure after ticket debit/run creation rolls back the whole transaction; retry works and insufficient balance remains mutation-free");

        // HTTP concurrency uses a synthetic wallet top-up; each start still costs one ticket.
        await Sql("update user_wallets set tickets=3 where account_id=@id", ("id", b.Account.Id));
        var replies = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => HttpStart(b.SessionToken)));
        Require(replies.Count(r => r.Status == HttpStatusCode.OK) == 1 &&
                replies.Count(r => r.Status == HttpStatusCode.Conflict && r.Code == "active_run_exists") == 7,
            "Parallel authenticated HTTP requests must create one run and return stable 409 conflicts.");
        await AssertCounts(b.Account.Id, 3, 3, 2);
        pass("eight real authenticated HTTP starts create one run and seven active_run_exists 409 responses");

        await AccountOperationChecks.Run(http, db, configuration, a, b, pass, ct);

        async Task<GameIdAuthResponse> Register(string label)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { gameId = "start." + Guid.NewGuid().ToString("N")[..12], password = "Synthetic_Only_333!", displayName = label, clientVersion = "0.1.0-dev" }), Encoding.UTF8, "application/json")
            };
            using var response = await http.SendAsync(request, ct);
            Require(response.IsSuccessStatusCode, "Synthetic registration failed.");
            return JsonSerializer.Deserialize<GameIdAuthResponse>(await response.Content.ReadAsStringAsync(ct), Json)!;
        }
        async Task<(HttpStatusCode Status, string? Code)> HttpStart(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/runs/start") { Content = new StringContent("{\"mode\":\"pve\"}", Encoding.UTF8, "application/json") };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, ct);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return (response.StatusCode, response.IsSuccessStatusCode ? null : json.RootElement.Field("error").Field("code").GetString());
        }
        async Task ExpectHttpConflict(string token, string code)
        {
            var result = await HttpStart(token);
            Require(result.Status == HttpStatusCode.Conflict && result.Code == code, "Expected HTTP 409 " + code + "; got " + result.Status + "/" + result.Code);
        }
        async Task Sql(string text, params (string Key, object Value)[] args)
        {
            await using var command = new NpgsqlCommand(text, db);
            foreach (var (key, value) in args) command.Parameters.AddWithValue(key, value);
            await command.ExecuteNonQueryAsync(ct);
        }
        async Task WaitBlocked(int expected)
        {
            for (var i = 0; i < 100; i++)
            {
                await using var query = new NpgsqlCommand("select count(*) from pg_stat_activity where application_name=@name and wait_event_type='Lock'", db);
                query.Parameters.AddWithValue("name", marker);
                if ((long)(await query.ExecuteScalarAsync(ct))! >= expected) return;
                await Task.Delay(20, ct);
            }
            throw new InvalidOperationException("Concurrent starts did not reach the controlled wallet lock barrier.");
        }
        async Task<string> Snapshot(Guid id)
        {
            await using var query = new NpgsqlCommand("""
                select jsonb_build_object(
                    'wallet',(select to_jsonb(w) from user_wallets w where account_id=@id),
                    'runs',(select jsonb_agg(to_jsonb(r) order by id) from draft_runs r where account_id=@id),
                    'ledger',(select jsonb_agg(to_jsonb(t) order by id) from account_transactions t where account_id=@id))::text;
                """, db);
            query.Parameters.AddWithValue("id", id);
            return (string)(await query.ExecuteScalarAsync(ct))!;
        }
        async Task AssertCounts(Guid id, int runs, int debits, int tickets)
        {
            await using var query = new NpgsqlCommand("select (select count(*) from draft_runs where account_id=@id), (select count(*) from account_transactions where account_id=@id and transaction_type='ticket_spend'), (select tickets from user_wallets where account_id=@id)", db);
            query.Parameters.AddWithValue("id", id);
            await using var reader = await query.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
            Require(reader.GetInt64(0) == runs && reader.GetInt64(1) == debits && reader.GetInt32(2) == tickets, "Run/debit/wallet totals do not match.");
        }
        async Task UniqueRejected(string sql, Guid id)
        {
            try { await Sql(sql, ("id", id)); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "draft_runs_one_active_per_account_idx") { return; }
            throw new InvalidOperationException("Expected DB active-run uniqueness violation.");
        }
    }

    static AuthenticatedAccount Account(GameIdAuthResponse response) => new(response.Account, response.Wallet, new CollectionSummaryDto(0, Array.Empty<OwnedCardDto>()));
    static async Task<(StartRunResponse? Run, string? Error)> Start(RunStartService service, AuthenticatedAccount account, string mode, CancellationToken ct)
    {
        try { return (await service.StartDraftRunAsync(account, new StartRunRequest(mode), ct), null); }
        catch (RunServiceException ex) { return (null, ex.Code); }
    }
    static async Task LockWallet(NpgsqlConnection db, NpgsqlTransaction tx, Guid accountId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select account_id from user_wallets where account_id=@id for update", db, tx);
        command.Parameters.AddWithValue("id", accountId); await command.ExecuteScalarAsync(ct);
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
