using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Cards;
using Project333.PvpServer.Persistence.Db;

static class AccountOperationChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public sealed record ReplayRequest(string Token, string Path, string Body, Guid AccountId);
    static string ReplayFile => Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("AFTER333_AUTHORITY_TEST_DATA"))!, "operation-replays.json");

    public static async Task Run(HttpClient http, NpgsqlConnection db, IConfiguration config,
        GameIdAuthResponse a, GameIdAuthResponse b, Action<string> pass, CancellationToken ct)
    {
        // Reuse two accounts created by the synthetic run-start checks; never touch live data.
        await Sql("update user_wallets set resource_gold=1000,tickets=10 where account_id in (@a,@b)", ("a", a.Account.Id), ("b", b.Account.Id));
        await Sql("insert into user_card_collection(account_id,card_id,copy_count,upgrade_level) values(@a,'firebolt',100,0) on conflict(account_id,card_id) do update set copy_count=100,upgrade_level=0", ("a", a.Account.Id));
        var ticketId = Guid.NewGuid().ToString("D");
        var ticket = new { requestId = ticketId, ticketCount = 1 };
        var purchases = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Send(http,a.SessionToken,"/wallet/purchase-ticket",ticket,ct)));
        Require(purchases.All(r => r.Status == HttpStatusCode.OK) && purchases.Count(r => !r.Body.Field("replayed").GetBoolean()) == 1, "Concurrent ticket retries must have one original and seven replays.");
        await Totals(a.Account.Id,997,11,100,0,1);
        pass("eight authenticated concurrent ticket retries charge exactly 3 gold, grant one ticket and write one receipt/ledger entry");

        var upgradeId = Guid.NewGuid().ToString("D");
        var upgrade = new { requestId = upgradeId, cardId = "firebolt", expectedUpgradeLevel = 0 };
        var upgrades = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Send(http,a.SessionToken,"/cards/upgrade",upgrade,ct)));
        Require(upgrades.All(r => r.Status == HttpStatusCode.OK) && upgrades.Count(r => !r.Body.Field("replayed").GetBoolean()) == 1, "Concurrent upgrade retries must advance one level.");
        await Totals(a.Account.Id,994,11,97,1,2);
        pass("eight authenticated concurrent upgrade retries consume 3 copies and 3 gold only once and stop at level 1");

        var snapshot = await Snapshot(db,a.Account.Id,ct);
        await Error("/wallet/purchase-ticket",new { requestId=ticketId,ticketCount=2 },409,"request_id_conflict");
        await Error("/cards/upgrade",new { requestId=ticketId,cardId="firebolt",expectedUpgradeLevel=1 },409,"request_id_conflict");
        await Error("/cards/upgrade",new { requestId=upgradeId,cardId="firebolt",expectedUpgradeLevel=1 },409,"request_id_conflict");
        await Error("/cards/upgrade",new { requestId=upgradeId,cardId="Firewall",expectedUpgradeLevel=0 },409,"request_id_conflict");
        Require(await Snapshot(db,a.Account.Id,ct)==snapshot,"Conflicting receipt payload changed data.");
        var other = await Send(http,b.SessionToken,"/wallet/purchase-ticket",ticket,ct);
        Require(other.Status==HttpStatusCode.OK && !other.Body.Field("replayed").GetBoolean(),"A different account must own its own request-id namespace.");
        Require(await Snapshot(db,a.Account.Id,ct)==snapshot,"Cross-account reuse affected original account.");
        pass("same request id with changed count/card/level/operation returns 409 unchanged; another account gets an independent receipt");

        var levelRaces = await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Send(http,a.SessionToken,"/cards/upgrade",
            new { requestId=Guid.NewGuid().ToString("D"),cardId="firebolt",expectedUpgradeLevel=1 },ct)));
        Require(levelRaces.Count(r=>r.Status==HttpStatusCode.OK)==1 && levelRaces.Count(r=>r.Status==HttpStatusCode.Conflict && Code(r.Body)=="card_upgrade_conflict")==7,"Different request ids from the same displayed level must not chain upgrades.");
        await Totals(a.Account.Id,988,11,91,2,3);
        pass("eight different request ids for the same displayed upgrade level produce one upgrade and seven stale-level conflicts");

        snapshot=await Snapshot(db,a.Account.Id,ct);
        var replay=await Send(http,a.SessionToken,"/cards/upgrade",new {requestId=upgradeId,cardId=" FIREBOLT ",expectedUpgradeLevel=0},ct);
        Require(replay.Status==HttpStatusCode.OK && replay.Body.Field("replayed").GetBoolean() && replay.Body.Field("wallet").Field("resourceGold").GetInt64()==988 && replay.Body.Field("upgradedCard").Field("upgradeLevel").GetInt32()==2 && replay.Body.Field("upgradeCost").Field("levelTo").GetInt32()==1,"Replay must retain original cost but return current wallet/collection, accepting canonical card spelling.");
        var ticketReplay=await Send(http,a.SessionToken,"/wallet/purchase-ticket",ticket,ct);
        Require(ticketReplay.Body.Field("wallet").Field("resourceGold").GetInt64()==988 && await Snapshot(db,a.Account.Id,ct)==snapshot,"Ticket replay returned stale balance or mutated data.");
        pass("older committed receipts replay after later spending with current wallet/card state and original cost, without stale-state rollback");

        foreach(var value in new object[] { new {ticketCount=1},new {ticketCount=1,requestId=""},new {ticketCount=1,requestId=Guid.Empty.ToString("D")},new {ticketCount=1,requestId="malformed"} })
            await Error("/wallet/purchase-ticket",value,400,"invalid_request_id");
        await Error("/cards/upgrade",new {cardId="firebolt"},400,"invalid_request_id");
        await Error("/cards/upgrade",new {cardId="firebolt",requestId=Guid.NewGuid().ToString("D")},400,"invalid_expected_upgrade_level");
        await Error("/cards/upgrade",new {cardId="firebolt",requestId=Guid.NewGuid().ToString("D"),expectedUpgradeLevel=-1},400,"invalid_expected_upgrade_level");
        await Error("/wallet/purchase-ticket",new {ticketCount=101,requestId=Guid.NewGuid().ToString("D")},400,"invalid_ticket_count");
        Require(await Snapshot(db,a.Account.Id,ct)==snapshot,"Missing/invalid id or level changed data.");
        pass("legacy bodies without a request id, empty/malformed ids, missing/negative expected levels and invalid ticket counts fail closed without charges");

        // Simulate a crash/failure at the last write, after gold/copies/tickets/ledger changed.
        var failedTicket=new {requestId=Guid.NewGuid().ToString("D"),ticketCount=1};
        var failedUpgrade=new {requestId=Guid.NewGuid().ToString("D"),cardId="firebolt",expectedUpgradeLevel=2};
        await Sql("""
            create function check_fail_operation_receipt() returns trigger language plpgsql as $$ begin
              raise exception 'synthetic receipt write failure'; end; $$;
            create trigger check_fail_operation_receipt before insert on account_operation_receipts
              for each row execute function check_fail_operation_receipt();
            """);
        try
        {
            Require((int)(await Send(http,a.SessionToken,"/wallet/purchase-ticket",failedTicket,ct)).Status==500,"Injected receipt failure must fail purchase.");
            Require((int)(await Send(http,a.SessionToken,"/cards/upgrade",failedUpgrade,ct)).Status==500,"Injected receipt failure must fail upgrade.");
        }
        finally { await Sql("drop trigger check_fail_operation_receipt on account_operation_receipts; drop function check_fail_operation_receipt();"); }
        Require(await Snapshot(db,a.Account.Id,ct)==snapshot,"Late receipt failure did not roll back all economic writes.");
        Require((await Send(http,a.SessionToken,"/wallet/purchase-ticket",failedTicket,ct)).Status==HttpStatusCode.OK,"Rolled-back purchase id must be safely retryable.");
        Require((await Send(http,a.SessionToken,"/cards/upgrade",failedUpgrade,ct)).Status==HttpStatusCode.OK,"Rolled-back upgrade id must be safely retryable.");
        await Totals(a.Account.Id,976,12,82,3,5);
        pass("failure at receipt insert rolls back wallet, ticket/card grants, level and ledger; the same ids then succeed exactly once");

        var marker="operation-cancel-"+Guid.NewGuid().ToString("N");
        var cs=new NpgsqlConnectionStringBuilder(config[DbConnectionFactory.ConnectionStringKey]!){ApplicationName=marker};
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{[DbConnectionFactory.ConnectionStringKey]=cs.ConnectionString}).Build();
        var service=new CardUpgradeService(new DbConnectionFactory(cfg));
        var authenticated=new AuthenticatedAccount(a.Account,a.Wallet,new CollectionSummaryDto(0,Array.Empty<OwnedCardDto>()));
        snapshot=await Snapshot(db,a.Account.Id,ct);
        await using var observer = new NpgsqlConnection(config[DbConnectionFactory.ConnectionStringKey]!);
        await observer.OpenAsync(ct);
        await using(var block=await db.BeginTransactionAsync(ct))
        {
            using var canceled=CancellationTokenSource.CreateLinkedTokenSource(ct);
            await using(var command=new NpgsqlCommand("select account_id from user_wallets where account_id=@id for update",db,block))
            { command.Parameters.AddWithValue("id",a.Account.Id);await command.ExecuteScalarAsync(ct); }
            var waiting=service.UpgradeCardAsync(authenticated,new UpgradeCardRequest("firebolt",Guid.NewGuid().ToString("D"),3),canceled.Token);
            try
            {
                var blocked=false;
                for(var i=0;i<100;i++)
                {
                    await using var query=new NpgsqlCommand("select count(*) from pg_stat_activity where application_name=@name and wait_event_type='Lock'",observer);
                    query.Parameters.AddWithValue("name",marker);
                    if((long)(await query.ExecuteScalarAsync(ct))!>0){blocked=true;break;}
                    await Task.Delay(20,ct);
                }
                Require(blocked,"Upgrade must reach wallet lock before cancellation.");
                canceled.Cancel();
                try { await waiting;throw new Exception("Canceled upgrade succeeded."); }catch(OperationCanceledException){ }
            }
            finally { canceled.Cancel();await block.RollbackAsync(CancellationToken.None); }
        }
        Require(await Snapshot(db,a.Account.Id,ct)==snapshot,"Canceled lock wait changed data.");
        pass("cancellation while waiting for a wallet lock leaves no charge, upgrade or receipt");

        await Sql("update user_wallets set resource_gold=0 where account_id=@a",("a",a.Account.Id));
        snapshot=await Snapshot(db,a.Account.Id,ct);
        await Error("/wallet/purchase-ticket",new {requestId=Guid.NewGuid().ToString("D"),ticketCount=1},409,"insufficient_resource_gold");
        await Error("/cards/upgrade",new {requestId=Guid.NewGuid().ToString("D"),cardId="firebolt",expectedUpgradeLevel=3},409,"insufficient_resource_gold");
        Require((await Send(http,a.SessionToken,"/wallet/purchase-ticket",ticket,ct)).Status==HttpStatusCode.OK && (await Send(http,a.SessionToken,"/cards/upgrade",upgrade,ct)).Status==HttpStatusCode.OK && await Snapshot(db,a.Account.Id,ct)==snapshot,"Zero balance must block new requests but still replay committed ones.");
        pass("insufficient balance rejects new operations while previously committed purchase and upgrade remain replayable without funds");


        const string resolvePath = "/account/operations/resolve";
        snapshot = await Snapshot(db,a.Account.Id,ct);
        foreach (var id in new[] { ticketId, upgradeId })
        {
            var resolved = await Send(http,a.SessionToken,resolvePath,new { requestId=id },ct);
            Require(resolved.Status == HttpStatusCode.OK && resolved.Body.Field("status").GetString()=="completed"
                && resolved.Body.Field("accountId").GetString()==a.Account.Id.ToString("D"),
                "Committed operation must resolve as completed with authenticated ownership.");
        }
        Require(await Snapshot(db,a.Account.Id,ct)==snapshot,"Resolving committed operations changed economy.");
        pass("lost purchase/upgrade responses resolve from committed records without resubmitting or charging");

        foreach (var kind in new[] { "ticket", "upgrade" })
        {
            var id = Guid.NewGuid().ToString("D");
            var cancellations = await Task.WhenAll(Enumerable.Range(0,8).Select(_ =>
                Send(http,a.SessionToken,resolvePath,new { requestId=id },ct)));
            Require(cancellations.All(r=>r.Status==HttpStatusCode.OK && r.Body.Field("status").GetString()=="cancelled"),
                "Absent operations must cancel idempotently.");
            if(kind=="ticket") await Error("/wallet/purchase-ticket",new {requestId=id,ticketCount=1},400,"operation_cancelled");
            else await Error("/cards/upgrade",new {requestId=id,cardId="firebolt",expectedUpgradeLevel=3},400,"operation_cancelled");
        }
        Require(await Snapshot(db,a.Account.Id,ct)==snapshot,"Cancelled requests or delayed arrivals changed economy.");
        pass("concurrent absent-operation resolution cancels permanently; late ticket/upgrade arrivals cannot charge");

        var cancelledByB = await Send(http,b.SessionToken,resolvePath,new {requestId=ticketId},ct);
        Require(cancelledByB.Status==HttpStatusCode.OK && cancelledByB.Body.Field("status").GetString()=="completed",
            "Each account resolves only its own receipt for a reused id.");
        var foreign = await Send(http,b.SessionToken,resolvePath,new {requestId=upgradeId},ct);
        Require(foreign.Status==HttpStatusCode.OK && foreign.Body.Field("status").GetString()=="cancelled",
            "Another account must not discover the original account's receipt.");
        var original = await Send(http,a.SessionToken,resolvePath,new {requestId=upgradeId},ct);
        Require(original.Body.Field("status").GetString()=="completed","Another account's cancellation affected the original.");
        await Error(resolvePath,new {requestId=""},400,"invalid_request_id");
        Require((await Send(http,"invalid-token",resolvePath,new {requestId=Guid.NewGuid().ToString("D")},ct)).Status==HttpStatusCode.Unauthorized,
            "Unauthenticated cancellation must fail.");
        pass("resolution validates request ids and authentication and remains scoped to the owning account");

        await Sql("update user_wallets set resource_gold=100 where account_id=@a",("a",a.Account.Id));
        for(var race=0;race<8;race++)
        {
            var id=Guid.NewGuid().ToString("D");
            var buy=Send(http,a.SessionToken,"/wallet/purchase-ticket",new {requestId=id,ticketCount=1},ct);
            var cancel=Send(http,a.SessionToken,resolvePath,new {requestId=id},ct);
            await Task.WhenAll(buy,cancel);
            var purchase=await buy;var resolution=await cancel;
            Require(resolution.Status==HttpStatusCode.OK,"Race resolution failed.");
            var completed=resolution.Body.Field("status").GetString()=="completed";
            Require(completed ? purchase.Status==HttpStatusCode.OK : Code(purchase.Body)=="operation_cancelled",
                "Completion/cancellation race reported contradictory outcomes.");
            await using var check=new NpgsqlCommand("""
                select (select count(*) from account_operation_receipts where account_id=@id and request_id=@r),
                       (select count(*) from account_operation_cancellations where account_id=@id and request_id=@r)
                """,db);
            check.Parameters.AddWithValue("id",a.Account.Id);check.Parameters.AddWithValue("r",Guid.Parse(id));
            await using var reader=await check.ExecuteReaderAsync(ct);await reader.ReadAsync(ct);
            Require(reader.GetInt64(0)==(completed?1:0) && reader.GetInt64(1)==(completed?0:1),
                "A request must have exactly one terminal outcome.");
        }
        await Sql("update user_wallets set resource_gold=0 where account_id=@a",("a",a.Account.Id));
        pass("eight purchase-versus-cancel races each produce exactly one durable terminal outcome");

        // Synthetic tokens only; runner reuses these after stopping/restarting its own server.
        await File.WriteAllTextAsync(ReplayFile,JsonSerializer.Serialize(new[]{
            new ReplayRequest(a.SessionToken,"/wallet/purchase-ticket",JsonSerializer.Serialize(ticket),a.Account.Id),
            new ReplayRequest(a.SessionToken,"/cards/upgrade",JsonSerializer.Serialize(upgrade),a.Account.Id)}),ct);

        async Task Error(string path,object payload,int status,string code)
        {var r=await Send(http,a.SessionToken,path,payload,ct);Require((int)r.Status==status && Code(r.Body)==code,$"Expected {status}/{code}, got {r.Status}/{Code(r.Body)}.");}
        async Task Sql(string text,params (string Key,object Value)[] args)
        {await using var command=new NpgsqlCommand(text,db);foreach(var (key,value)in args)command.Parameters.AddWithValue(key,value);await command.ExecuteNonQueryAsync(ct);}
        async Task Totals(Guid id,long gold,int tickets,int copies,int level,int receipts)
        {
            await using var command=new NpgsqlCommand("""
                select w.resource_gold,w.tickets,c.copy_count,c.upgrade_level,
                  (select count(*) from account_operation_receipts where account_id=@id),
                  (select count(*) from account_transactions where account_id=@id and transaction_type in ('upgrade','purchase'))
                from user_wallets w join user_card_collection c on c.account_id=w.account_id
                where w.account_id=@id and c.card_id='firebolt'
                """,db);
            command.Parameters.AddWithValue("id",id);await using var reader=await command.ExecuteReaderAsync(ct);Require(await reader.ReadAsync(ct),"Fixture missing.");
            Require(reader.GetInt64(0)==gold && reader.GetInt32(1)==tickets && reader.GetInt32(2)==copies && reader.GetInt32(3)==level && reader.GetInt64(4)==receipts && reader.GetInt64(5)==receipts,"Wallet/card/receipt/ledger totals differ from exactly-once amounts.");
        }
    }

    public static async Task ReplayAfterRestart(HttpClient http,NpgsqlConnection db,CancellationToken ct)
    {
        var requests=JsonSerializer.Deserialize<ReplayRequest[]>(await File.ReadAllTextAsync(ReplayFile,ct))!;
        foreach(var request in requests)
        {
            var before=await Snapshot(db,request.AccountId,ct);
            var reply=await Send(http,request.Token,request.Path,JsonSerializer.Deserialize<JsonElement>(request.Body),ct);
            Require(reply.Status==HttpStatusCode.OK && reply.Body.Field("replayed").GetBoolean() && reply.Body.Field("wallet").Field("resourceGold").GetInt64()==0 && await Snapshot(db,request.AccountId,ct)==before,"Restart retry charged again or failed to retrieve receipt.");
        }

        var owner=requests[0];
        await using(var query=new NpgsqlCommand("select request_id from account_operation_cancellations where account_id=@id limit 1",db))
        {
            query.Parameters.AddWithValue("id",owner.AccountId);
            var cancelled=(Guid)(await query.ExecuteScalarAsync(ct))!;
            var before=await Snapshot(db,owner.AccountId,ct);
            var result=await Send(http,owner.Token,"/account/operations/resolve",new {requestId=cancelled.ToString("D")},ct);
            var late=await Send(http,owner.Token,"/wallet/purchase-ticket",new {requestId=cancelled.ToString("D"),ticketCount=1},ct);
            Require(result.Status==HttpStatusCode.OK && result.Body.Field("status").GetString()=="cancelled"
                && Code(late.Body)=="operation_cancelled" && await Snapshot(db,owner.AccountId,ct)==before,
                "Restart lost cancellation tombstone or allowed a delayed charge.");
        }
        Console.WriteLine("PASS: cancellation survives server restart and prevents a delayed original request.");
        Console.WriteLine("PASS: 2 committed operations replay after a real isolated server process restart with zero balance and unchanged wallet/collection/ledger/receipts.");
    }
    static async Task<(HttpStatusCode Status,JsonElement Body)> Send(HttpClient http,string token,string path,object payload,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json")};
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.SendAsync(request,ct);
        var text=await response.Content.ReadAsStringAsync(ct);
        return(response.StatusCode,string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<JsonElement>(text));
    }
    static string? Code(JsonElement body)=>body.ValueKind==JsonValueKind.Object && body.TryGetProperty("error",out var error)?error.Field("code").GetString():null;
    static async Task<string> Snapshot(NpgsqlConnection db,Guid id,CancellationToken ct)
    {
        await using var command=new NpgsqlCommand("""
            select jsonb_build_object(
              'wallet',(select to_jsonb(w) from user_wallets w where account_id=@id),
              'collection',(select jsonb_agg(to_jsonb(c) order by card_id) from user_card_collection c where account_id=@id),
              'ledger',(select jsonb_agg(to_jsonb(t) order by id) from account_transactions t where account_id=@id),
              'receipts',(select jsonb_agg(to_jsonb(r) order by request_id) from account_operation_receipts r where account_id=@id))::text
            """,db);
        command.Parameters.AddWithValue("id",id);return (string)(await command.ExecuteScalarAsync(ct))!;
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
