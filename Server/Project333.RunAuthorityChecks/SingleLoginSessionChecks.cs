using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Persistence.Db;

static class SingleLoginSessionChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    const string Password = "Synthetic_Only_333!";
    const string ReplacedMessage = "다른 기기에서 로그인하여 연결이 종료되었습니다.";

    public static async Task Run(HttpClient http, NpgsqlConnection db, IConfiguration configuration,
        Action<string> pass, CancellationToken ct)
    {
        await CheckMigration(db,pass,ct);
        GuestAuthService Service() => new(new DbConnectionFactory(configuration), new AuthTokenService(),
            new GoogleIdentityTokenValidator(configuration), configuration);
        var service = Service();
        var eventCount = 0;
        service.LoginSessionsChanged += _ => throw new InvalidOperationException("Synthetic observer failure");
        service.LoginSessionsChanged += _ => Interlocked.Increment(ref eventCount);
        var gameId = "single." + Guid.NewGuid().ToString("N")[..12];
        var a = await service.RegisterGameIdAsync(new(gameId, Password, "Single login fixture", "0.1.0-dev"), Context("A"), ct);
        Require(eventCount==1, "Observer exceptions must not fail a committed registration or skip later observers.");
        var aAuth = await service.GetCurrentAccountAsync(Bearer(a.SessionToken), ct);
        Require(aAuth.LoginFamilyId.HasValue && await service.IsLoginFamilyCurrentAsync(a.Account.Id, aAuth.LoginFamilyId.Value, ct),
            "Registration must create one current login family.");
        await Expect("invalid_credentials", () => service.LoginGameIdAsync(new(gameId, "Wrong_333!", null, "0.1.0-dev"), Context("B"), ct));
        await service.ValidateSessionAsync(Bearer(a.SessionToken), ct);
        Require(eventCount==1, "A failed login must not replace the old login or publish an event.");

        var rotated = await service.RefreshSessionAsync(new(a.RefreshToken, "0.1.0-dev"), Context("A"), ct);
        var rotatedAuth = await service.GetCurrentAccountAsync(Bearer(rotated.SessionToken), ct);
        Require(eventCount==1 && rotatedAuth.LoginFamilyId==aAuth.LoginFamilyId,
            "Refresh must preserve its family and must not signal a different-device takeover.");
        await Expect("invalid_session_token", () => service.ValidateSessionAsync(Bearer(a.SessionToken), ct));
        Require(await service.IsLoginFamilyCurrentAsync(a.Account.Id, aAuth.LoginFamilyId!.Value, ct),
            "A socket's family must remain current when its device rotates the access token.");
        pass("successful registration survives a failing transport observer; failed login leaves the current session alone; refresh preserves the family without takeover events");

        var before = await Wallet(a.Account.Id);
        var b = await service.LoginGameIdAsync(new(gameId, Password, null, "0.1.0-dev"), Context("B"), ct);
        var bAuth = await service.GetCurrentAccountAsync(Bearer(b.SessionToken), ct);
        Require(eventCount==2 && bAuth.LoginFamilyId!=aAuth.LoginFamilyId, "Independent login must replace the family and notify once after commit.");
        await Expect("session_replaced", () => service.ValidateSessionAsync(Bearer(rotated.SessionToken), ct));
        await Expect("session_replaced", () => service.GetCurrentAccountAsync(Bearer(rotated.SessionToken), ct));
        await Expect("session_replaced", () => service.RefreshSessionAsync(new(rotated.RefreshToken, "0.1.0-dev"), Context("A"), ct));
        await Expect("session_replaced", () => service.RefreshSessionAsync(new(a.RefreshToken, "0.1.0-dev"), Context("A"), ct));
        Require(!await service.IsLoginFamilyCurrentAsync(a.Account.Id, aAuth.LoginFamilyId!.Value, ct), "Superseded family must stop authenticating active sockets.");
        await service.LogoutAsync(Bearer(rotated.SessionToken), new(rotated.RefreshToken), ct);
        await service.ValidateSessionAsync(Bearer(b.SessionToken), ct);
        Require(before==await Wallet(a.Account.Id), "Takeover, old refresh and delayed old logout must preserve the account wallet.");
        pass("a newer login invalidates all older access/refresh tokens with the stable Korean session_replaced error; delayed old logout cannot revoke the newer family or touch balances");

        using (var request = new HttpRequestMessage(HttpMethod.Get, "/auth/session-status"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rotated.SessionToken);
            using var response = await http.SendAsync(request, ct);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Require(response.StatusCode==HttpStatusCode.Unauthorized && body.RootElement.GetProperty("error").GetProperty("code").GetString()=="session_replaced",
                "Cheap HTTP session polling must reject a replaced token without requesting /me.");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/auth/session-status"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", b.SessionToken);
            using var response = await http.SendAsync(request, ct);
            Require(response.IsSuccessStatusCode, "The newer session must pass cheap HTTP polling.");
        }
        pass("real HTTP session-status distinguishes the old rejected family from the active new login");

        // Both requests must lock accounts before token rows. Whichever wins the
        // lock, a later explicit login is the only surviving family.
        var refreshRace = service.RefreshSessionAsync(new(b.RefreshToken, "0.1.0-dev"), Context("B"), ct);
        var loginRace = Service().LoginGameIdAsync(new(gameId, Password, null, "0.1.0-dev"), Context("C"), ct);
        RefreshAuthResponse? raceRefresh = null;
        try { raceRefresh = await refreshRace; }
        catch (AuthServiceException ex) when (ex.Code=="session_replaced") { }
        var c = await loginRace;
        await service.ValidateSessionAsync(Bearer(c.SessionToken), ct);
        if (raceRefresh != null)
            await Expect("session_replaced", () => service.ValidateSessionAsync(Bearer(raceRefresh.SessionToken), ct));
        await AssertOnlyCurrentFamily(a.Account.Id);
        pass("concurrent refresh and independent login finish without a deadlock and cannot revive the replaced family");

        var simultaneous = await Task.WhenAll(Enumerable.Range(0,8).Select(i => Service().LoginGameIdAsync(
            new(gameId, Password, null, "0.1.0-dev"), Context("parallel-"+i), ct)));
        var accepted = new List<GameIdAuthResponse>();
        foreach (var result in simultaneous)
        {
            try { await service.ValidateSessionAsync(Bearer(result.SessionToken), ct); accepted.Add(result); }
            catch (AuthServiceException ex) when (ex.Code=="session_replaced") { }
        }
        Require(accepted.Count==1, "Eight independent concurrent logins must leave exactly one valid session family.");
        var current = accepted.Single();
        await AssertOnlyCurrentFamily(a.Account.Id);
        pass("eight parallel independent logins serialize by account and leave exactly one usable access/refresh family");

        var currentAuth = await service.GetCurrentAccountAsync(Bearer(current.SessionToken), ct);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var commandTask = Task.Run(() => service.ExecuteWithCurrentLoginFamilyAsync(a.Account.Id,
            currentAuth.LoginFamilyId!.Value, () =>
            {
                entered.Set();
                Require(release.Wait(TimeSpan.FromSeconds(10), ct), "Fixture command release timed out.");
                return 73;
            }, ct), ct);
        Require(entered.Wait(TimeSpan.FromSeconds(10), ct), "Protected command did not acquire its account share lock.");
        var competing = Service().LoginGameIdAsync(new(gameId, Password, null, "0.1.0-dev"), Context("after-command"), ct);
        try
        {
            await WaitForAccountLock();
            Require(!competing.IsCompleted, "Independent login must wait until the already-authorized synchronous command ends.");
        }
        finally { release.Set(); }
        Require(await commandTask==73, "Protected command result was lost.");
        var afterCommand = await competing;
        var callbackRan = false;
        await Expect("session_replaced", () => service.ExecuteWithCurrentLoginFamilyAsync(a.Account.Id,
            currentAuth.LoginFamilyId!.Value, () => { callbackRan=true; return 0; }, ct));
        Require(!callbackRan, "A superseded family must never execute another battle mutation.");
        await service.ValidateSessionAsync(Bearer(afterCommand.SessionToken), ct);
        pass("a short battle command holds the account share lock through its mutation; takeover waits and every later old-family command is rejected before its callback");

        var latest = await service.RefreshSessionAsync(new(afterCommand.RefreshToken, "0.1.0-dev"), Context("after-command"), ct);
        await service.LogoutAsync(Bearer(afterCommand.SessionToken), new(afterCommand.RefreshToken), ct);
        await Expect("invalid_session_token", () => service.ValidateSessionAsync(Bearer(latest.SessionToken), ct));
        var latestFamily = await Family(a.Account.Id);
        Require(latestFamily.HasValue && !await service.IsLoginFamilyCurrentAsync(a.Account.Id, latestFamily.Value, ct),
            "Logout through a rotated token must revoke that device's entire family.");
        pass("logout through an earlier rotated token revokes its current same-device descendants without mislabeling logout as a new-device takeover");

        var replay = await service.LoginGameIdAsync(new(gameId, Password, null, "0.1.0-dev"), Context("replay"), ct);
        var replayNext = await service.RefreshSessionAsync(new(replay.RefreshToken, "0.1.0-dev"), Context("replay"), ct);
        await Expect("refresh_token_reused", () => service.RefreshSessionAsync(new(replay.RefreshToken, "0.1.0-dev"), Context("replay"), ct));
        await Expect("invalid_session_token", () => service.ValidateSessionAsync(Bearer(replayNext.SessionToken), ct));
        Require(before==await Wallet(a.Account.Id), "Auth concurrency fixtures must not modify wallet state.");
        pass("same-family refresh replay protection is preserved and cannot leave an active replacement token after replay detection");

        async Task<(long Gold,int Tickets)> Wallet(Guid accountId)
        {
            await using var command = new NpgsqlCommand("select resource_gold,tickets from user_wallets where account_id=@id", db);
            command.Parameters.AddWithValue("id",accountId);
            await using var reader=await command.ExecuteReaderAsync(ct);
            Require(await reader.ReadAsync(ct),"Fixture wallet missing.");
            return (reader.GetInt64(0),reader.GetInt32(1));
        }
        async Task<Guid?> Family(Guid accountId)
        {
            await using var command=new NpgsqlCommand("select active_login_family_id from accounts where id=@id",db);
            command.Parameters.AddWithValue("id",accountId);
            return await command.ExecuteScalarAsync(ct) is Guid id ? id : null;
        }
        async Task AssertOnlyCurrentFamily(Guid accountId)
        {
            const string sql="""
                select count(*) filter(where s.login_family_id=a.active_login_family_id),
                    count(*) filter(where s.login_family_id is distinct from a.active_login_family_id)
                from auth_sessions s join accounts a on a.id=s.account_id
                where a.id=@id and s.revoked_at is null and s.expires_at>now();
                """;
            await using var command=new NpgsqlCommand(sql,db);
            command.Parameters.AddWithValue("id",accountId);
            await using var reader=await command.ExecuteReaderAsync(ct);
            Require(await reader.ReadAsync(ct) && reader.GetInt64(0)==1 && reader.GetInt64(1)==0,
                "Exactly one non-revoked access session must belong to the active family.");
        }
        async Task WaitForAccountLock()
        {
            var until=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<until)
            {
                await using var command=new NpgsqlCommand("select count(*) from pg_stat_activity where datname=current_database() and wait_event_type='Lock' and query like '%from accounts where id=%for update%'",db);
                if ((long)(await command.ExecuteScalarAsync(ct))!>0) return;
                await Task.Delay(15,ct);
            }
            throw new InvalidOperationException("Competing independent login did not wait on the account lock.");
        }
    }

    static async Task CheckMigration(NpgsqlConnection db,Action<string> pass,CancellationToken ct)
    {
        var source=Path.Combine(AppContext.BaseDirectory,"Persistence","Migrations","0025_single_login_family.sql");
        var migration=await File.ReadAllTextAsync(source,ct);
        // Execute the real migration against temporary legacy table shapes. No
        // permanent table, test account, other test's token, or public row changes.
        await using var transaction=await db.BeginTransactionAsync(ct);
        try
        {
            const string fixture="""
                create temporary table accounts (like public.accounts including all) on commit drop;
                create temporary table auth_sessions (like public.auth_sessions including all) on commit drop;
                create temporary table auth_refresh_tokens (like public.auth_refresh_tokens including all) on commit drop;
                create temporary table schema_migrations (like public.schema_migrations including all) on commit drop;
                alter table pg_temp.accounts drop column active_login_family_id;
                alter table pg_temp.auth_sessions drop column login_family_id;
                alter table pg_temp.auth_sessions drop column superseded_at;
                set local search_path=pg_temp,public;
                insert into accounts(id,display_name,account_kind) values
                    ('aaaaaaaa-0000-0000-0000-000000000001','legacy-refresh','registered'),
                    ('aaaaaaaa-0000-0000-0000-000000000002','legacy-orphan','registered'),
                    ('aaaaaaaa-0000-0000-0000-000000000003','legacy-ambiguous','registered');
                insert into auth_sessions(id,account_id,session_token_hash,created_at,expires_at,revoked_at) values
                    ('bbbbbbbb-0000-0000-0000-000000000001','aaaaaaaa-0000-0000-0000-000000000001','older-first',now()-interval '5 days',now()+interval '2 days',now()-interval '1 day'),
                    ('bbbbbbbb-0000-0000-0000-000000000002','aaaaaaaa-0000-0000-0000-000000000001','older-rotated-recently',now()-interval '1 day',now()+interval '6 days',null),
                    ('bbbbbbbb-0000-0000-0000-000000000003','aaaaaaaa-0000-0000-0000-000000000001','newer-access-expired',now()-interval '2 days',now()-interval '1 hour',null),
                    ('bbbbbbbb-0000-0000-0000-000000000004','aaaaaaaa-0000-0000-0000-000000000002','orphan-access',now()-interval '1 day',now()+interval '6 days',null),
                    ('bbbbbbbb-0000-0000-0000-000000000005','aaaaaaaa-0000-0000-0000-000000000003','ambiguous-access',now()-interval '1 day',now()+interval '6 days',null);
                insert into auth_refresh_tokens(account_id,session_id,token_family_id,refresh_token_hash,created_at,expires_at,revoked_at) values
                    ('aaaaaaaa-0000-0000-0000-000000000001','bbbbbbbb-0000-0000-0000-000000000001','cccccccc-0000-0000-0000-000000000001','older-first-refresh',now()-interval '5 days',now()+interval '25 days',now()-interval '1 day'),
                    ('aaaaaaaa-0000-0000-0000-000000000001','bbbbbbbb-0000-0000-0000-000000000002','cccccccc-0000-0000-0000-000000000001','older-rotated-refresh',now()-interval '1 day',now()+interval '29 days',null),
                    ('aaaaaaaa-0000-0000-0000-000000000001','bbbbbbbb-0000-0000-0000-000000000003','cccccccc-0000-0000-0000-000000000002','newer-live-refresh',now()-interval '2 days',now()+interval '28 days',null),
                    ('aaaaaaaa-0000-0000-0000-000000000003','bbbbbbbb-0000-0000-0000-000000000005','cccccccc-0000-0000-0000-000000000003','ambiguous-one',now()-interval '1 day',now()+interval '29 days',null),
                    ('aaaaaaaa-0000-0000-0000-000000000003','bbbbbbbb-0000-0000-0000-000000000005','cccccccc-0000-0000-0000-000000000004','ambiguous-two',now()-interval '1 day',now()+interval '29 days',null);
                """;
            await using(var command=new NpgsqlCommand(fixture,db,transaction)) await command.ExecuteNonQueryAsync(ct);
            migration=migration.Trim();
            Require(migration.StartsWith("begin;",StringComparison.OrdinalIgnoreCase) && migration.EndsWith("commit;",StringComparison.OrdinalIgnoreCase),"Migration fixture requires its standard transaction wrapper.");
            migration=migration[6..^7];
            await using(var command=new NpgsqlCommand(migration,db,transaction)) await command.ExecuteNonQueryAsync(ct);
            const string verify="""
                select
                    (select active_login_family_id='cccccccc-0000-0000-0000-000000000002'::uuid from pg_temp.accounts where id='aaaaaaaa-0000-0000-0000-000000000001'),
                    (select revoked_at is null from pg_temp.auth_refresh_tokens where refresh_token_hash='newer-live-refresh'),
                    (select revoked_at is null and superseded_at is null from pg_temp.auth_sessions where session_token_hash='newer-access-expired'),
                    (select bool_and(revoked_at is not null and superseded_at is not null) from pg_temp.auth_sessions where session_token_hash in ('older-first','older-rotated-recently')),
                    (select bool_and(revoked_at is not null) from pg_temp.auth_refresh_tokens where refresh_token_hash in ('older-first-refresh','older-rotated-refresh')),
                    (select bool_and(active_login_family_id is null) from pg_temp.accounts where id in ('aaaaaaaa-0000-0000-0000-000000000002','aaaaaaaa-0000-0000-0000-000000000003')),
                    (select bool_and(revoked_at is not null and login_family_id is null) from pg_temp.auth_sessions where session_token_hash in ('orphan-access','ambiguous-access'));
                """;
            await using(var command=new NpgsqlCommand(verify,db,transaction))
            await using(var reader=await command.ExecuteReaderAsync(ct))
            {
                Require(await reader.ReadAsync(ct),"Migration result missing.");
                for(var i=0;i<reader.FieldCount;i++) Require(reader.GetBoolean(i),"Legacy migration invariant failed at column "+i+".");
            }
        }
        finally { await transaction.RollbackAsync(CancellationToken.None); }
        pass("real migration0025 keeps the newest independent family's live refresh after access expiry, ignores a later old-family rotation, and revokes orphan/ambiguous legacy sessions");
    }

    static DefaultHttpContext Context(string name)
    {
        var context=new DefaultHttpContext(); context.Request.Headers.UserAgent="Single-login-fixture-"+name;
        context.Connection.RemoteIpAddress=IPAddress.Loopback; return context;
    }
    static string Bearer(string token)=>"Bearer "+token;
    static async Task Expect(string code,Func<Task> operation)
    {
        try { await operation(); }
        catch(AuthServiceException ex) when(ex.Code==code)
        {
            Require(code!="session_replaced" || ex.Message==ReplacedMessage,"Session replacement must use the stable Korean explanation."); return;
        }
        throw new InvalidOperationException("Expected auth error "+code+".");
    }
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}
