using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Gifts;
using Project333.PvpServer.Persistence.Db;

static class GiftInboxChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public sealed record RestartFixture(GameIdAuthResponse Owner, GameIdAuthResponse Later, Guid ClaimedGift,
        GiftSendRequest Pending, GiftSendRequest Global, long GlobalRecipients, string Snapshot);
    static string ReplayFile => Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("AFTER333_AUTHORITY_TEST_DATA"))!, "gift-replays.json");

    public static async Task Run(HttpClient http, NpgsqlConnection db, IConfiguration configuration,
        Action<string> pass, CancellationToken ct)
    {
        var service = new GiftInboxService(new DbConnectionFactory(configuration));
        Require(await Scalar(db, "select count(*) from schema_migrations where version='0017_gift_inbox'", ct) == 1,
            "Gift inbox migration must be present in the isolated database.");
        var owner = await Register(http, "gift-owner", ct);
        var other = await Register(http, "gift-other", ct);
        var deleted = await Register(http, "gift-deleted", ct);
        var disabled = await Register(http, "gift-disabled", ct);
        var banned = await Register(http, "gift-banned", ct);
        await Sql(db, "update accounts set account_status='deleted' where id=@id", ct, ("id", deleted.Account.Id));
        await Sql(db, "update accounts set account_status='disabled' where id=@id", ct, ("id", disabled.Account.Id));
        await Sql(db, "update accounts set is_banned=true where id=@id", ct, ("id", banned.Account.Id));
        var first = Request(owner.Account.Id, 3, 17);
        var before = await Snapshot(db, owner.Account.Id, ct);
        var beforeCampaigns = await Scalar(db, "select count(*) from gift_campaigns", ct);
        var preview = await service.PreviewAsync(first, ct);
        Require(preview.RecipientCount == 1 && !preview.AlreadySent && preview.Accounts.Single().AccountId == owner.Account.Id
            && await Snapshot(db, owner.Account.Id, ct) == before
            && await Scalar(db, "select count(*) from gift_campaigns", ct) == beforeCampaigns,
            "Preview must identify the recipient without dispatching a gift or modifying economic rows.");
        var found = await service.FindAccountsAsync(owner.Account.Id.ToString("D"), 10, ct);
        Require(found.Any(a => a.AccountId == owner.Account.Id), "Administrator account lookup must accept the unique account id.");
        pass("gift migration and administrator preview/account lookup work without creating a campaign, inbox entry or wallet credit");

        var originalWallet = await Wallet(db, owner.Account.Id, ct);
        var sent = await service.SendAsync(first, ct);
        Require(sent.GiftId == first.RequestId && !sent.AlreadySent && sent.RecipientCount == 1,
            "Send must persist one immutable dispatch under its request id.");
        Require(await Wallet(db, owner.Account.Id, ct) == originalWallet && await Ledger(db, owner.Account.Id, first.RequestId, ct) == 0,
            "Sending a gift must not grant its contents before claim.");
        var inbox = await Get(http, owner.SessionToken, "/gifts", ct);
        Require(inbox.Status == HttpStatusCode.OK && inbox.Body.Field("unclaimedCount").GetInt64() == 1
            && inbox.Body.Field("gifts").EnumerateArray().Single().Field("giftId").GetString() == first.RequestId.ToString("D"),
            "Owner must see the unclaimed gift over authenticated HTTP.");
        var foreignInbox = await Get(http, other.SessionToken, "/gifts", ct);
        Require(foreignInbox.Body.Field("gifts").GetArrayLength() == 0, "A different account must not list this targeted gift.");
        await HttpError(http, other.SessionToken, first.RequestId, 404, "gift_not_found", ct);
        await HttpError(http, owner.SessionToken, Guid.NewGuid(), 404, "gift_not_found", ct);
        Require((await Get(http, null, "/gifts", ct)).Status == HttpStatusCode.Unauthorized
            && (await Post(http, "invalid-token", "/gifts/" + first.RequestId + "/claim", ct)).Status == HttpStatusCode.Unauthorized,
            "Listing and claiming require valid authentication.");
        Require((await Post(http, owner.SessionToken, "/admin/gifts/send", ct)).Status == HttpStatusCode.NotFound,
            "Public server must not expose a gift administration endpoint.");
        Require(await Wallet(db, owner.Account.Id, ct) == originalWallet, "Rejected foreign/unauthenticated claims changed the wallet.");
        pass("targeted gifts are visible only to their authenticated recipient; send does not pay, foreign claims fail and no public admin route exists");

        var sends = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.SendAsync(first, ct)));
        Require(sends.All(r => r.AlreadySent && r.RecipientCount == 1), "Retrying a sent gift must return the original dispatch.");
        await ServiceError(() => service.SendAsync(first with { Tickets = 4 }, ct), 409, "gift_request_conflict");
        await ServiceError(() => service.SendAsync(first with { AccountIds = new[] { other.Account.Id } }, ct), 409, "gift_request_conflict");
        var canonical = Request(owner.Account.Id, 1, 1) with { AccountIds = new[] { owner.Account.Id, other.Account.Id } };
        var two = await service.SendAsync(canonical, ct);
        var reordered = await service.SendAsync(canonical with { AccountIds = new[] { other.Account.Id, owner.Account.Id } }, ct);
        Require(two.RecipientCount == 2 && reordered.AlreadySent && reordered.RecipientCount == 2,
            "Recipient order must not change the canonical retry payload.");
        var racingSend = Request(owner.Account.Id, 1, 1);
        var racingResults = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.SendAsync(racingSend, ct)));
        Require(racingResults.Count(r => !r.AlreadySent) == 1 && racingResults.All(r => r.RecipientCount == 1)
            && await Scalar(db, "select count(*) from gift_inbox where gift_id=@gift", ct, ("gift", racingSend.RequestId)) == 1,
            "Concurrent first dispatch must create exactly one campaign and recipient row.");
        Require(await Ledger(db, owner.Account.Id, first.RequestId, ct) == 0, "Repeated sends must never grant contents.");
        pass("concurrent dispatch retries keep one gift and original recipients; changed payloads conflict while reordered targets replay");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Post(http, owner.SessionToken, "/gifts/" + first.RequestId + "/claim", ct)));
        Require(responses.All(r => r.Status == HttpStatusCode.OK) && responses.Count(r => !r.Body.Field("alreadyClaimed").GetBoolean()) == 1,
            "Eight concurrent claims must produce one first claim and seven receipt replays.");
        Require(await Wallet(db, owner.Account.Id, ct) == (originalWallet.Gold + 17, originalWallet.Tickets + 3)
            && await Ledger(db, owner.Account.Id, first.RequestId, ct) == 1, "Exactly one atomic gold+ticket grant and ledger entry are required.");
        await AssertLedger(db, owner.Account.Id, first.RequestId, 17, 3, ct);
        pass("eight authenticated concurrent claims grant both items exactly once and persist one admin_adjustment ledger row");

        var walletBeforeDistinct = await Wallet(db, owner.Account.Id, ct);
        var distinct = Enumerable.Range(0, 8).Select(_ => Request(owner.Account.Id, 1, 2)).ToArray();
        foreach (var gift in distinct) await service.SendAsync(gift, ct);
        var distinctClaims = await Task.WhenAll(distinct.Select(g => Post(http, owner.SessionToken, "/gifts/" + g.RequestId + "/claim", ct)));
        Require(distinctClaims.All(r => r.Status == HttpStatusCode.OK && !r.Body.Field("alreadyClaimed").GetBoolean())
            && await Wallet(db, owner.Account.Id, ct) == (walletBeforeDistinct.Gold + 16, walletBeforeDistinct.Tickets + 8),
            "Different gifts claimed concurrently must serialize wallet changes without lost increments.");
        foreach (var gift in distinct) Require(await Ledger(db, owner.Account.Id, gift.RequestId, ct) == 1, "Distinct gift ledger missing.");
        pass("concurrent claims for different gifts preserve every wallet increment without lost updates");

        var global = Request(owner.Account.Id, 2, 5) with { AllExistingAccounts = true, AccountIds = null };
        var eligible = await Scalar(db, "select count(*) from accounts where account_status <> 'deleted'", ct);
        var beforeGlobalWallet = await Wallet(db, owner.Account.Id, ct);
        var globalResult = await service.SendAsync(global, ct);
        Require(globalResult.RecipientCount == eligible
            && await Scalar(db, "select count(*) from gift_inbox where gift_id=@gift", ct, ("gift", global.RequestId)) == eligible
            && await Scalar(db, "select count(*) from gift_inbox where gift_id=@gift and account_id=@id", ct, ("gift", global.RequestId), ("id", deleted.Account.Id)) == 0
            && await Scalar(db, "select count(*) from gift_inbox where gift_id=@gift and account_id in (@disabled,@banned)", ct,
                ("gift", global.RequestId), ("disabled", disabled.Account.Id), ("banned", banned.Account.Id)) == 2
            && await Wallet(db, owner.Account.Id, ct) == beforeGlobalWallet,
            "Global dispatch must snapshot all existing non-deleted accounts, including disabled/banned accounts, without paying.");
        var later = await Register(http, "gift-after-dispatch", ct);
        var globalReplay = await service.SendAsync(global, ct);
        Require(globalReplay.AlreadySent && globalReplay.RecipientCount == eligible
            && (await service.ListAsync(later.Account.Id, null, ct)).Gifts.Count == 0,
            "A later registration must not receive an older global gift, even when send is replayed.");
        pass("all-account gifts snapshot existing non-deleted accounts only; later registrations stay excluded across dispatch retries");

        var expired = Request(owner.Account.Id, 1, 2) with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
        await service.SendAsync(expired, ct);
        await Expire(db, expired.RequestId, ct);
        var expirationBefore = await Snapshot(db, owner.Account.Id, ct);
        await HttpError(http, owner.SessionToken, expired.RequestId, 409, "gift_expired", ct);
        Require(await Snapshot(db, owner.Account.Id, ct) == expirationBefore
            && (await service.ListAsync(owner.Account.Id, null, ct)).Gifts.Single(g => g.GiftId == expired.RequestId.ToString("D")).Status == "expired",
            "An expired unclaimed gift must remain unpaid and be presented as expired.");
        var expiringClaimed = Request(owner.Account.Id, 1, 1) with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
        await service.SendAsync(expiringClaimed, ct);
        await service.ClaimAsync(owner.Account.Id, expiringClaimed.RequestId, ct);
        await Expire(db, expiringClaimed.RequestId, ct);
        var claimedBefore = await Snapshot(db, owner.Account.Id, ct);
        var afterExpiryReplay = await Post(http, owner.SessionToken, "/gifts/" + expiringClaimed.RequestId + "/claim", ct);
        Require(afterExpiryReplay.Status == HttpStatusCode.OK && afterExpiryReplay.Body.Field("alreadyClaimed").GetBoolean()
            && await Snapshot(db, owner.Account.Id, ct) == claimedBefore,
            "Expiry must not turn a successfully claimed gift into a failed retry or pay it twice.");
        pass("optional expiry blocks unpaid gifts without mutations; already-claimed gifts still replay after expiry");

        var overflow = Request(owner.Account.Id, 2, 3);
        await service.SendAsync(overflow, ct);
        var walletForRestore = await Wallet(db, owner.Account.Id, ct);
        foreach (var balance in new[] { (Gold: long.MaxValue, Tickets: 5), (Gold: 5L, Tickets: int.MaxValue) })
        {
            await SetWallet(db, owner.Account.Id, balance.Gold, balance.Tickets, ct);
            var overflowBefore = await Snapshot(db, owner.Account.Id, ct);
            await HttpError(http, owner.SessionToken, overflow.RequestId, 409, "gift_wallet_limit", ct);
            Require(await Snapshot(db, owner.Account.Id, ct) == overflowBefore,
                "One item's overflow must roll back both items, claim timestamp and ledger.");
        }
        await SetWallet(db, owner.Account.Id, walletForRestore.Gold, walletForRestore.Tickets, ct);
        Require(!(await service.ClaimAsync(owner.Account.Id, overflow.RequestId, ct)).AlreadyClaimed
            && await Ledger(db, owner.Account.Id, overflow.RequestId, ct) == 1, "A rolled-back overflow claim must remain safely claimable.");
        pass("gold and ticket overflow each roll back the complete grant and receipt; retry after capacity is restored pays once");

        var failedGift = Request(owner.Account.Id, 2, 7);
        await service.SendAsync(failedGift, ct);
        var failedBefore = await Snapshot(db, owner.Account.Id, ct);
        await Sql(db, """
            create function check_fail_gift_ledger() returns trigger language plpgsql as $$ begin
              if new.source_table='gift_inbox' then raise exception 'synthetic gift ledger failure'; end if;
              return new; end; $$;
            create trigger check_fail_gift_ledger before insert on account_transactions
              for each row execute function check_fail_gift_ledger();
            """, ct);
        try { await HttpError(http, owner.SessionToken, failedGift.RequestId, 503, "gift_service_unavailable", ct); }
        finally { await Sql(db, "drop trigger check_fail_gift_ledger on account_transactions; drop function check_fail_gift_ledger();", ct); }
        Require(await Snapshot(db, owner.Account.Id, ct) == failedBefore, "Ledger failure did not roll back wallet and claimed timestamp.");
        Require(!(await service.ClaimAsync(owner.Account.Id, failedGift.RequestId, ct)).AlreadyClaimed
            && await Ledger(db, owner.Account.Id, failedGift.RequestId, ct) == 1, "Failed claim must safely retry.");
        pass("a database failure at gift ledger insertion rolls back wallet and claim state; the same gift then succeeds once");

        for (var i = 0; i < 52; i++) await service.SendAsync(Request(other.Account.Id, 1, 0), ct);
        var firstPage = await Get(http, other.SessionToken, "/gifts", ct);
        Require(firstPage.Status == HttpStatusCode.OK && firstPage.Body.Field("gifts").GetArrayLength() == 50
            && !string.IsNullOrEmpty(firstPage.Body.Field("nextCursor").GetString()), "Inbox must paginate after fifty entries.");
        var expectedRows = await Scalar(db, "select count(*) from gift_inbox where account_id=@id", ct, ("id", other.Account.Id));
        var seen = firstPage.Body.Field("gifts").EnumerateArray().Select(g => g.Field("giftId").GetString()!).ToHashSet();
        var cursor = firstPage.Body.Field("nextCursor").GetString();
        var newer = Request(other.Account.Id, 1, 0);
        await service.SendAsync(newer, ct);
        while (!string.IsNullOrEmpty(cursor))
        {
            var page = await Get(http, other.SessionToken, "/gifts?cursor=" + Uri.EscapeDataString(cursor), ct);
            Require(page.Status == HttpStatusCode.OK, "Valid cursor was rejected.");
            foreach (var row in page.Body.Field("gifts").EnumerateArray())
                Require(seen.Add(row.Field("giftId").GetString()!), "Pagination duplicated a gift.");
            cursor = JsonSerializer.Deserialize<GiftInboxResponse>(page.Body.GetRawText(), Json)!.NextCursor;
        }
        Require(seen.Count == expectedRows && !seen.Contains(newer.RequestId.ToString("D")),
            "Keyset pagination must return original older entries exactly once when a new gift arrives.");
        var badCursor = await Get(http, owner.SessionToken, "/gifts?cursor=invalid", ct);
        Require((int)badCursor.Status == 400 && ErrorCode(badCursor.Body) == "gift_cursor_invalid", "Malformed cursor must fail closed.");
        var listed = await service.ListAsync(owner.Account.Id, null, ct);
        Require(listed.UnclaimedCount == await Scalar(db, """
            select count(*) from gift_inbox i join gift_campaigns c on c.id=i.gift_id
            where i.account_id=@id and i.claimed_at is null and (c.expires_at is null or c.expires_at>clock_timestamp())
            """, ct, ("id", owner.Account.Id)), "Badge count must exclude claimed and expired gifts.");
        pass("fifty-entry keyset pages do not skip or duplicate older gifts during new delivery; invalid cursors fail and badge count excludes expired/claimed entries");

        var absentTarget = Request(owner.Account.Id, 1, 1) with { AccountIds = new[] { owner.Account.Id, Guid.NewGuid() } };
        var invalidBefore = await Scalar(db, "select count(*) from gift_campaigns", ct);
        await AnyServiceError(() => service.SendAsync(absentTarget, ct));
        await AnyServiceError(() => service.SendAsync(Request(owner.Account.Id, 1, 1) with { AccountIds = new[] { owner.Account.Id, owner.Account.Id } }, ct));
        await AnyServiceError(() => service.SendAsync(Request(owner.Account.Id, 0, 0), ct));
        await AnyServiceError(() => service.SendAsync(Request(owner.Account.Id, -1, 1), ct));
        await AnyServiceError(() => service.SendAsync(Request(owner.Account.Id, 1, 0) with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1) }, ct));
        await AnyServiceError(() => service.SendAsync(Request(owner.Account.Id, 1, 0) with { RequestId = Guid.Empty }, ct));
        await AnyServiceError(() => service.SendAsync(Request(deleted.Account.Id, 1, 0), ct));
        Require(await Scalar(db, "select count(*) from gift_campaigns", ct) == invalidBefore,
            "Invalid/missing recipients and invalid item/expiry/id inputs must not create partial campaigns.");
        pass("invalid ids, empty/negative items, past expiry, deleted and mixed missing recipients leave no partially sent campaign");

        await CheckAdminCli(db, owner, pass, ct);
        var pending = Request(owner.Account.Id, 4, 11);
        await service.SendAsync(pending, ct);
        await File.WriteAllTextAsync(ReplayFile, JsonSerializer.Serialize(new RestartFixture(owner, later, first.RequestId,
            pending, global, eligible, await Snapshot(db, owner.Account.Id, ct))), ct);
        pass("claimed and pending gift fixtures are preserved for real isolated HTTP server restart verification");
    }


    static async Task CheckAdminCli(NpgsqlConnection db, GameIdAuthResponse owner, Action<string> pass, CancellationToken ct)
    {
        var cli = Environment.GetEnvironmentVariable("AFTER333_GIFT_ADMIN_TEST_DLL");
        if (string.IsNullOrWhiteSpace(cli)) return;
        Require(Path.IsPathFullyQualified(cli) && File.Exists(cli) && Path.GetFileName(cli) == "Project333.GiftAdmin.dll",
            "Gift CLI checks require an explicitly supplied built administrator DLL.");
        var root = Path.GetDirectoryName(ReplayFile)!;
        var file = Path.Combine(root, "prepared-gift-check.json");
        var request = Request(owner.Account.Id, 3, 19);
        var before = await Snapshot(db, owner.Account.Id, ct);
        var walletBefore = await Wallet(db, owner.Account.Id, ct);
        var campaigns = await Scalar(db, "select count(*) from gift_campaigns", ct);
        var accounts = await Invoke("accounts", null, new { Search = owner.Account.Id.ToString("D") });
        Require(accounts.ExitCode == 0 && accounts.Body.Field("accounts").EnumerateArray().Any(a =>
            a.Field("accountId").GetGuid() == owner.Account.Id), "Administrator CLI account search did not find the exact id.");
        var prepared = await Invoke("prepare", file, request);
        Require(prepared.ExitCode == 0 && !prepared.Body.Field("sent").GetBoolean()
            && prepared.Body.Field("preview").Field("recipientCount").GetInt64() == 1,
            "Administrator prepare must save an unsent request file.");
        var content = await File.ReadAllTextAsync(file, ct);
        var preview = await Invoke("preview", file, null);
        Require(preview.ExitCode == 0 && preview.Body.Field("preview").Field("recipientCount").GetInt64() == 1
            && await Snapshot(db, owner.Account.Id, ct) == before
            && await Scalar(db, "select count(*) from gift_campaigns", ct) == campaigns,
            "Administrator prepare/preview must not dispatch or change the wallet.");
        pass("built administrator CLI finds an exact account and prepares/previews an immutable request file without sending");

        var overwrite = await Invoke("prepare", file, request with { Tickets = 30 });
        Require(overwrite.ExitCode == 2 && ErrorCode(overwrite.Body) == "invalid_local_request"
            && await File.ReadAllTextAsync(file, ct) == content, "Prepare must refuse to overwrite an existing retry file.");
        var document = JsonSerializer.Deserialize<JsonElement>(content);
        var wrongFile = Path.Combine(root, "wrong-database-gift-check.json");
        await File.WriteAllTextAsync(wrongFile, JsonSerializer.Serialize(new { Version = 1, DatabaseIdentity = "wrong",
            Request = document.Field("request") }), ct);
        var mismatch = await Invoke("send", wrongFile, null);
        Require(mismatch.ExitCode == 2 && ErrorCode(mismatch.Body) == "database_mismatch"
            && await Snapshot(db, owner.Account.Id, ct) == before
            && await Scalar(db, "select count(*) from gift_campaigns", ct) == campaigns,
            "A request bound to a different DB must be rejected without partial sending.");
        pass("administrator request files resist accidental overwrite and reject a different database identity without mutations");

        await Sql(db, """
            create function check_fail_cli_dispatch() returns trigger language plpgsql as $$ begin
              raise exception 'synthetic administrator dispatch failure'; end; $$;
            create trigger check_fail_cli_dispatch before insert on gift_inbox
              for each row execute function check_fail_cli_dispatch();
            """, ct);
        try
        {
            var failed = await Invoke("send", file, null);
            Require(failed.ExitCode == 3 && ErrorCode(failed.Body) == "database_unavailable",
                "Administrator CLI must report a retryable DB failure.");
        }
        finally { await Sql(db, "drop trigger check_fail_cli_dispatch on gift_inbox; drop function check_fail_cli_dispatch();", ct); }
        Require(await Scalar(db, "select count(*) from gift_campaigns", ct) == campaigns,
            "Failed administrator dispatch left a partial campaign.");
        var sent = await Invoke("send", file, null);
        var repeated = await Invoke("send", file, null);
        Require(sent.ExitCode == 0 && !sent.Body.Field("result").Field("alreadySent").GetBoolean()
            && repeated.ExitCode == 0 && repeated.Body.Field("result").Field("alreadySent").GetBoolean()
            && await Scalar(db, "select count(*) from gift_inbox where gift_id=@gift", ct, ("gift", request.RequestId)) == 1
            && await Wallet(db, owner.Account.Id, ct) == walletBefore,
            "The same prepared file must retry a failed send, then deduplicate successful replays without paying the wallet.");
        Require(await Ledger(db, owner.Account.Id, request.RequestId, ct) == 0,
            "Administrator sending must leave item payment to claim.");
        pass("administrator send failure rolls back and the same saved file safely retries; repeated successful sends create one unpaid gift");

        async Task<(int ExitCode, JsonElement Body)> Invoke(string command,string? path,object? input)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = new UTF8Encoding(false)
            };
            start.ArgumentList.Add(cli);
            start.ArgumentList.Add(command);
            if (path is not null) start.ArgumentList.Add(path);
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(ct);
            var error = process.StandardError.ReadToEndAsync(ct);
            try
            {
                if(input is not null) await process.StandardInput.WriteAsync(JsonSerializer.Serialize(input).AsMemory(), ct);
                process.StandardInput.Close();
                await process.WaitForExitAsync(ct);
                var stdout = await output;
                var stderr = await error;
                Require(string.IsNullOrWhiteSpace(stderr), "Administrator CLI emitted unexpected stderr.");
                return (process.ExitCode, JsonSerializer.Deserialize<JsonElement>(stdout));
            }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        }
    }

    public static async Task ReplayAfterRestart(HttpClient http, NpgsqlConnection db, CancellationToken ct)
    {
        var fixture = JsonSerializer.Deserialize<RestartFixture>(await File.ReadAllTextAsync(ReplayFile, ct), Json)!;
        Require(await Snapshot(db, fixture.Owner.Account.Id, ct) == fixture.Snapshot, "Restart changed the owner's gift/economy state.");
        var replay = await Post(http, fixture.Owner.SessionToken, "/gifts/" + fixture.ClaimedGift + "/claim", ct);
        Require(replay.Status == HttpStatusCode.OK && replay.Body.Field("alreadyClaimed").GetBoolean()
            && await Snapshot(db, fixture.Owner.Account.Id, ct) == fixture.Snapshot, "Restart lost the claimed receipt or paid a duplicate.");
        var inbox = await Get(http, fixture.Owner.SessionToken, "/gifts", ct);
        Require(inbox.Status == HttpStatusCode.OK && inbox.Body.Field("gifts").EnumerateArray().Any(
            g => g.Field("giftId").GetString() == fixture.Pending.RequestId.ToString("D") && g.Field("status").GetString() == "unclaimed"),
            "Restart lost the pending gift.");
        var before = await Wallet(db, fixture.Owner.Account.Id, ct);
        var claimed = await Post(http, fixture.Owner.SessionToken, "/gifts/" + fixture.Pending.RequestId + "/claim", ct);
        Require(claimed.Status == HttpStatusCode.OK && !claimed.Body.Field("alreadyClaimed").GetBoolean()
            && await Wallet(db, fixture.Owner.Account.Id, ct) == (before.Gold + fixture.Pending.ResourceGold, before.Tickets + fixture.Pending.Tickets)
            && await Ledger(db, fixture.Owner.Account.Id, fixture.Pending.RequestId, ct) == 1, "Pending gift must pay exactly once after restart.");
        var after = await Snapshot(db, fixture.Owner.Account.Id, ct);
        var duplicate = await Post(http, fixture.Owner.SessionToken, "/gifts/" + fixture.Pending.RequestId + "/claim", ct);
        Require(duplicate.Status == HttpStatusCode.OK && duplicate.Body.Field("alreadyClaimed").GetBoolean()
            && await Snapshot(db, fixture.Owner.Account.Id, ct) == after, "Restarted pending gift paid twice.");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { [DbConnectionFactory.ConnectionStringKey] = db.ConnectionString }).Build();
        var resent = await new GiftInboxService(new DbConnectionFactory(config)).SendAsync(fixture.Global, ct);
        Require(resent.AlreadySent && resent.RecipientCount == fixture.GlobalRecipients
            && (await Get(http, fixture.Later.SessionToken, "/gifts", ct)).Body.Field("gifts").GetArrayLength() == 0,
            "Restart lost global recipient snapshot/idempotency.");
        Console.WriteLine("PASS: gift inbox survives a real isolated HTTP restart; committed claims replay, pending gifts pay once and global replay excludes later accounts.");
    }

    static GiftSendRequest Request(Guid accountId, int tickets, long gold) => new(Guid.NewGuid(), "합성 선물 검사", "격리 DB 전용 선물", tickets, gold, false, new[] { accountId }, null, "Synthetic gift inbox regression");
    static async Task<GameIdAuthResponse> Register(HttpClient http, string label, CancellationToken ct)
    {
        var response = await Send(http, null, HttpMethod.Post, "/auth/register", new
            { gameId = "gifts." + Guid.NewGuid().ToString("N")[..12], password = "Synthetic_Only_333!", displayName = label, clientVersion = "0.1.0-dev" }, ct);
        Require(response.Status == HttpStatusCode.OK, "Synthetic gift account registration failed: " + response.Status);
        return JsonSerializer.Deserialize<GameIdAuthResponse>(response.Body.GetRawText(), Json)!;
    }
    static Task<(HttpStatusCode Status, JsonElement Body)> Get(HttpClient http,string? token,string path,CancellationToken ct)
        => Send(http,token,HttpMethod.Get,path,null,ct);
    static Task<(HttpStatusCode Status, JsonElement Body)> Post(HttpClient http,string? token,string path,CancellationToken ct)
        => Send(http,token,HttpMethod.Post,path,new { },ct);
    static async Task<(HttpStatusCode Status, JsonElement Body)> Send(HttpClient http,string? token,HttpMethod method,string path,object? body,CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method,path);
        if(body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
        if(token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
        using var response = await http.SendAsync(request,ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        return(response.StatusCode,string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<JsonElement>(text));
    }
    static string? ErrorCode(JsonElement body) => body.ValueKind == JsonValueKind.Object && (body.TryGetProperty("error",out var e) || body.TryGetProperty("Error",out e)) ? e.Field("code").GetString() : null;
    static async Task HttpError(HttpClient http,string token,Guid gift,int status,string code,CancellationToken ct)
    {
        var response = await Post(http,token,"/gifts/"+gift+"/claim",ct);
        Require((int)response.Status == status && ErrorCode(response.Body) == code,"Gift rejection differed: "+response.Status+" "+ErrorCode(response.Body)+"; expected "+status+" "+code);
    }
    static async Task ServiceError<T>(Func<Task<T>> action,int status,string code)
    {
        try { await action();throw new InvalidOperationException("Expected gift rejection: "+code); }
        catch(GiftInboxException e) { Require(e.StatusCode == status && e.Code == code,"Wrong gift service rejection: "+e.Code); }
    }
    static async Task AnyServiceError<T>(Func<Task<T>> action)
    {
        try { await action();throw new InvalidOperationException("Invalid gift unexpectedly succeeded."); }
        catch(GiftInboxException e) { Require(e.StatusCode is 400 or 404 or 409,"Invalid gift should be a structured input/conflict rejection."); }
    }
    static async Task Sql(NpgsqlConnection db,string sql,CancellationToken ct,params (string Name,object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql,db);foreach(var p in parameters) cmd.Parameters.AddWithValue(p.Name,p.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    static async Task<long> Scalar(NpgsqlConnection db,string sql,CancellationToken ct,params (string Name,object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql,db);foreach(var p in parameters) cmd.Parameters.AddWithValue(p.Name,p.Value);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }
    static async Task<(long Gold,int Tickets)> Wallet(NpgsqlConnection db,Guid account,CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("select resource_gold,tickets from user_wallets where account_id=@id",db);
        cmd.Parameters.AddWithValue("id",account);await using var reader = await cmd.ExecuteReaderAsync(ct);
        Require(await reader.ReadAsync(ct),"Gift wallet missing.");return(reader.GetInt64(0),reader.GetInt32(1));
    }
    static Task SetWallet(NpgsqlConnection db,Guid account,long gold,int tickets,CancellationToken ct)
        => Sql(db,"update user_wallets set resource_gold=@gold,tickets=@tickets where account_id=@id",ct,("id",account),("gold",gold),("tickets",tickets));
    static Task<long> Ledger(NpgsqlConnection db,Guid account,Guid gift,CancellationToken ct)
        => Scalar(db,"select count(*) from account_transactions where account_id=@id and source_table='gift_inbox' and source_id=@gift",ct,("id",account),("gift",gift));
    static async Task AssertLedger(NpgsqlConnection db,Guid account,Guid gift,long gold,int tickets,CancellationToken ct)
    {
        Require(await Scalar(db,"select count(*) from account_transactions where account_id=@id and source_table='gift_inbox' and source_id=@gift and transaction_type='admin_adjustment' and resource_gold_delta=@gold and ticket_delta=@tickets",
            ct,("id",account),("gift",gift),("gold",gold),("tickets",tickets)) == 1,"Gift ledger content does not match the two granted items.");
    }
    static Task Expire(NpgsqlConnection db,Guid gift,CancellationToken ct) => Sql(db,
        "update gift_campaigns set created_at=clock_timestamp()-interval '2 days',expires_at=clock_timestamp()-interval '1 day' where id=@gift",ct,("gift",gift));
    static async Task<string> Snapshot(NpgsqlConnection db,Guid account,CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            select jsonb_build_object(
              'wallet',(select to_jsonb(w) from user_wallets w where account_id=@id),
              'ledger',(select coalesce(jsonb_agg(to_jsonb(t) order by t.id),'[]'::jsonb) from account_transactions t where account_id=@id),
              'inbox',(select coalesce(jsonb_agg(to_jsonb(i) order by i.gift_id),'[]'::jsonb) from gift_inbox i where account_id=@id)
            )::text
            """,db);
        cmd.Parameters.AddWithValue("id",account);return(string)(await cmd.ExecuteScalarAsync(ct))!;
    }
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}
