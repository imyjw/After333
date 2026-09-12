using Project333.PvpServer.BattleSessions;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;
using Project333.Runtime.Infrastructure.Data;

static class SavedDraftOfferChecks
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public static async Task Run(HttpClient http, NpgsqlConnection db, IConfiguration config, Action<string> pass, CancellationToken ct)
    {
        // Match the isolated runner's ticket price; normal registration grants two tickets.
        config = new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(
            new Dictionary<string, string?> { ["PROJECT333_DRAFT_RUN_TICKET_COST"] = "1" }).Build();
        var original = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        var auth = await Register();
        var account = new AuthenticatedAccount(auth.Account, auth.Wallet, new CollectionSummaryDto(0, Array.Empty<OwnedCardDto>()));
        RunStartService Service(JsonCardDefinitionDatabase catalog) => new(new DbConnectionFactory(config), config, catalog);
        var start = await Service(original).StartDraftRunAsync(account, new StartRunRequest("pve"), ct);
        var runId = start.ActiveRun.Id;
        var saved = start.CurrentOfferCardIds.ToArray();
        var latest = JsonSerializer.Deserialize<JsonCardDefinitionDatabase>(JsonSerializer.Serialize(original))!;
        foreach (var card in latest.Cards)
        {
            if (saved.Contains(card.Id)) { card.Rarity = CardRarity.Common; card.IncludeInDraft = false; card.Health += 123; }
            else if (card.IncludeInDraft && card.Id != "Master") card.Rarity = CardRarity.Unique;
        }
        var service = Service(latest);
        var before = await Snapshot();
        foreach (var state in await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Service(latest).GetAuthoritativeDraftStateAsync(account, new(runId.ToString()), ct))))
            Require(state.CurrentOfferCardIds.SequenceEqual(saved), "Resume rerolled/reordered the saved offer after catalog changes.");
        Require(before == await Snapshot(), "Reading a stored offer rewrote run state.");
        pass("saved opening offer survives catalog reorder/rarity/draft-eligibility changes and concurrent read without DB rewrite");

        var outsider = latest.Cards.First(c => c.IncludeInDraft && c.Id != "Master").Id;
        await Reject(() => service.SelectAuthoritativeDraftCardAsync(account, new(runId.ToString(), outsider, 0), ct), "card_not_in_draft_offer");
        Require(before == await Snapshot(), "Invalid selection changed run state.");
        var first = await service.SelectAuthoritativeDraftCardAsync(account, new(runId.ToString(), saved[0], 0), ct);
        Require(first.DraftPickCardIds.SequenceEqual(new[]{saved[0]}), "Previously offered card became unselectable.");
        Require(first.CurrentOfferCardIds.All(id => latest.Cards.Single(c => c.Id == id).IncludeInDraft && latest.Cards.Single(c => c.Id == id).Rarity == CardRarity.Unique), "Next offer did not use latest eligibility and rarity.");
        pass("selection validates the stored offer; its disabled/reclassified card remains selectable and next offer uses current catalog");

        await Reject(() => service.SelectAuthoritativeDraftCardAsync(account, new(runId.ToString(), saved[0], 0), ct), "stale_draft_pick_index");
        var stranger = await Register();
        await Reject(() => service.GetAuthoritativeDraftStateAsync(new(stranger.Account,stranger.Wallet,new(0,Array.Empty<OwnedCardDto>())), new(runId.ToString()), ct), "run_not_found");
        pass("stale picks and other-account access remain rejected");

        await Sql("update draft_runs set current_offer_card_ids='[]'::jsonb where id=@id");
        var initialized = await service.GetAuthoritativeDraftStateAsync(account,new(runId.ToString()),ct);
        var alternate = JsonSerializer.Deserialize<JsonCardDefinitionDatabase>(JsonSerializer.Serialize(latest))!;
        alternate.Cards.Reverse();
        var resumed = await Service(alternate).GetAuthoritativeDraftStateAsync(account,new(runId.ToString()),ct);
        Require(resumed.CurrentOfferCardIds.SequenceEqual(initialized.CurrentOfferCardIds), "Legacy empty offer was not initialized once.");
        pass("legacy empty offer is saved once and stays fixed after a new service/catalog order");

        await Sql("update draft_runs set current_offer_card_ids='[\"duplicate\",\"duplicate\",\"third\"]'::jsonb where id=@id");
        var broken = await Snapshot();
        await Reject(() => service.GetAuthoritativeDraftStateAsync(account,new(runId.ToString()),ct),"invalid_server_draft_offer");
        Require(broken == await Snapshot(), "Corrupt saved offer was silently regenerated.");
        pass("invalid saved offers fail without replacement");

        // Historical accepted picks are IDs, not frozen definitions. Simulate a previously
        // legal 32-pick history before a common card with three copies becomes Legendary.
        var pool = original.Cards.Where(c => c.IncludeInDraft && c.Rarity != CardRarity.Legendary && c.Id != "Master").Take(14).Select(c => c.Id).ToArray();
        var history = new[]{saved[0]}.Concat(pool.Take(10).SelectMany(id => Enumerable.Repeat(id,3))).Concat(new[]{pool[10]}).ToArray();
        await Sql("delete from draft_run_picks where run_id=@id");
        for(var i=0;i<history.Length;i++)
        {
            await using var insert=new NpgsqlCommand("insert into draft_run_picks(run_id,pick_index,offered_card_ids,selected_card_id) values(@id,@index,cast(@offer as jsonb),@card)",db);
            insert.Parameters.AddWithValue("id",runId);insert.Parameters.AddWithValue("index",i);
            insert.Parameters.AddWithValue("offer",JsonSerializer.Serialize(new[]{history[i],pool[12],pool[13]}));insert.Parameters.AddWithValue("card",history[i]);
            await insert.ExecuteNonQueryAsync(ct);
        }
        latest.Cards.Single(c=>c.Id==pool[0]).Rarity=CardRarity.Legendary;
        await using(var offer=new NpgsqlCommand("update draft_runs set current_offer_card_ids=cast(@offer as jsonb) where id=@id",db))
        {offer.Parameters.AddWithValue("id",runId);offer.Parameters.AddWithValue("offer",JsonSerializer.Serialize(pool.Skip(11).Take(3)));await offer.ExecuteNonQueryAsync(ct);}
        var final = await Service(latest).SelectAuthoritativeDraftCardAsync(account,new(runId.ToString(),pool[11],32),ct);
        Require(final.IsComplete && final.DraftPickCardIds.SequenceEqual(history.Append(pool[11])) && final.CurrentOfferCardIds.Count==0, "Final deck lost/reclassified historical picks.");
        Require(final.DraftPickCardIds.Count(id=>id==pool[0])==3 && latest.Cards.Single(c=>c.Id==saved[0]).Health==original.Cards.Single(c=>c.Id==saved[0]).Health+123,"Latest definitions or accepted copies changed.");
        pass("33-card completion retains historical IDs/copies after rarity updates; no card-definition snapshot is stored");

        async Task Sql(string sql){await using var q=new NpgsqlCommand(sql,db);q.Parameters.AddWithValue("id",runId);await q.ExecuteNonQueryAsync(ct);}
        async Task<string> Snapshot(){await using var q=new NpgsqlCommand("select to_jsonb(r)::text from draft_runs r where id=@id",db);q.Parameters.AddWithValue("id",runId);return (string)(await q.ExecuteScalarAsync(ct))!;}
        async Task<GameIdAuthResponse> Register()
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/auth/register"){Content=new StringContent(JsonSerializer.Serialize(new{gameId="offers."+Guid.NewGuid().ToString("N")[..12],password="Synthetic_Only_333!",displayName="saved-offer-check",clientVersion="0.1.0-dev"}),Encoding.UTF8,"application/json")};
            using var response=await http.SendAsync(request,ct);response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<GameIdAuthResponse>(await response.Content.ReadAsStringAsync(ct),Json)!;
        }
    }
    static async Task Reject(Func<Task> action,string code){try{await action();}catch(RunServiceException ex)when(ex.Code==code){return;}throw new Exception("Expected "+code);}
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}