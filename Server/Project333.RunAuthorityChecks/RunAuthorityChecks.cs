using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;
using Project333.PvpServer.BattleResults;

// This executable only accepts the runner's fresh, loopback-only synthetic cluster.
// It never receives a live connection string or a real account/token as an argument.
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var replayOnly = args.SequenceEqual(new[] { "--replay-results" });
var operationReplayOnly = args.SequenceEqual(new[] { "--replay-account-operations" });
var ct = deadline.Token;
var connectionString = Environment.GetEnvironmentVariable("PROJECT333_DB_CONNECTION") ?? "";
var expectedDataPath = Environment.GetEnvironmentVariable("AFTER333_AUTHORITY_TEST_DATA") ?? "";
var cs = new NpgsqlConnectionStringBuilder(connectionString);
Require(cs.Host == "127.0.0.1" && cs.Port == 15439 &&
        cs.Database == "after333_run_authority" && cs.Username == "after333_authority_test",
    "Refusing any database other than the runner's synthetic loopback database.");
Require(Path.GetFullPath(expectedDataPath).StartsWith(
        Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase), "The database directory must be inside the OS temporary directory.");
await using var db = new NpgsqlConnection(connectionString);
await db.OpenAsync(ct);
await using (var isolation = new NpgsqlCommand("select current_setting('data_directory'), (select count(*) from accounts);", db))
await using (var reader = await isolation.ExecuteReaderAsync(ct))
{
    Require(await reader.ReadAsync(ct), "Isolation query must return a row.");
    Require(string.Equals(Path.GetFullPath(reader.GetString(0)), Path.GetFullPath(expectedDataPath),
            StringComparison.OrdinalIgnoreCase), "The running cluster must be the newly created test cluster.");
    Require(replayOnly || operationReplayOnly || reader.GetInt64(1) == 0, "The test database must contain zero pre-existing accounts.");
}
if (replayOnly)
{
    await ResultDeliveryChecks.ReplayInChild(connectionString, expectedDataPath, ct);
    return;
}

using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:17339"), Timeout = TimeSpan.FromSeconds(12) };
http.DefaultRequestHeaders.Add("X-Project333-Client-Version", "0.1.0-dev");
if (operationReplayOnly)
{
    await AccountOperationChecks.ReplayAfterRestart(http, db, ct);
    return;
}
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    [DbConnectionFactory.ConnectionStringKey] = connectionString,
}).Build();
var runService = new RunStartService(new DbConnectionFactory(configuration), configuration);
var resultStore = new BattleResultStore(new DbConnectionFactory(configuration));
var passCount = 0;
var battleCount = 0;
var resultCount = 0;
await SavedDraftOfferChecks.Run(http, db, configuration, Pass, ct);
await RunStartAtomicityChecks.Run(http, db, configuration, Pass, ct);
BattleParticipantChecks.Run(Pass);
await ConnectionStabilityChecks.Run(Pass, ct);
await BattleInputChecks.Run(Pass, ct);
BattleConnectionLimitChecks.Run(Pass);
var a = await DraftAccount("win-cap");
var b = await DraftAccount("loss-cap");
var c = await DraftAccount("binding-original");
var d = await DraftAccount("binding-target");
await BattleConnectionLimitChecks.CheckNetwork(a, db, Pass, ct);
await MatchmakingReservationChecks.Run(db, configuration, a, b, c, d, Pass, ct);
var parallelAccounts = new[] { await DraftAccount("parallel-1"), await DraftAccount("parallel-2"),
    await DraftAccount("parallel-3"), await DraftAccount("parallel-4") };
await MatchmakingReservationChecks.CheckNetwork(parallelAccounts, Pass, ct);
var parallelStats = new List<(int Wins, int Losses)>();
foreach (var account in parallelAccounts) { var stats = await ReadStats(account); parallelStats.Add((stats.Wins, stats.Losses)); }
Require(parallelStats.All(s => s.Wins + s.Losses == 1) && parallelStats.Sum(s => s.Wins) == 2 && parallelStats.Sum(s => s.Losses) == 2,
    "Two concurrent matches must record exactly one result per original participant.");
Pass("both concurrently matched battles persist exactly two wins and two losses to their original runs");
var initialA = await Snapshot(a.AccountId);
var initialB = await Snapshot(b.AccountId);
await BattleBindingNetworkChecks.CheckDatabase(db, runService, configuration, a, b, ct);
await ConnectionStabilityChecks.CheckDatabase(db, configuration, a, Pass, ct);
Require(await Snapshot(a.AccountId) == initialA && await Snapshot(b.AccountId) == initialB,
    "Rejected cross-account result and participant DB rewrites must preserve both runs and economies.");
Pass("DB result ownership and started-match participant guards reject account/run/deck/seat rewrites without changing run or economy data");
await BattleBindingNetworkChecks.Run(c, d, ct);
var boundC = await ReadStats(c);
var boundD = await ReadStats(d);
Require(boundC.Wins == 0 && boundC.Losses == 3 && boundD.Wins == 3 && boundD.Losses == 0,
    "All three WebSocket battles must credit their original participants after rejoin, takeover and abrupt disconnect.");
Pass("real WebSocket rejoin, live takeover, abrupt disconnect and oversized-input rejection/reconnect preserve seat, battle state and original result ownership through DB persistence");
var e = await DraftAccount("result-retries");
var f = await DraftAccount("atomic-opponent");
await ResultDeliveryChecks.Run(db, configuration, e, f, Pass, ct);

foreach (var forged in new[]
{
    (Wins: 33, Losses: 0, Deck: a.DeckId, Label: "forged 33 wins"),
    (Wins: 0, Losses: 3, Deck: a.DeckId, Label: "forged 3 losses"),
    (Wins: 33, Losses: 3, Deck: b.DeckId, Label: "forged terminal record with another account's deck"),
    (Wins: 1, Losses: 0, Deck: a.DeckId, Label: "forged partial result"),
})
{
    await ExpectError("/runs/sync-local-record", new { runId = a.RunId, deckId = forged.Deck,
        wins = forged.Wins, losses = forged.Losses }, a.Token, HttpStatusCode.Gone,
        "client_authoritative_run_result_removed");
    Require(await Snapshot(a.AccountId) == initialA && await Snapshot(b.AccountId) == initialB,
        "Rejected uploads must not modify run/deck data, wallet, collection, inventory, grants or transaction rows.");
    Pass(forged.Label + " returns 410; both accounts' run and economy snapshots are unchanged");
}
await ExpectError("/runs/sync-local-record", new { wins = 33, losses = 0 }, null,
    HttpStatusCode.Gone, "client_authoritative_run_result_removed");
using (var malformed = new HttpRequestMessage(HttpMethod.Post, "/runs/sync-local-record")
    { Content = new StringContent("{malformed", Encoding.UTF8, "application/json") })
using (var response = await http.SendAsync(malformed, ct))
{
    Require(response.StatusCode == HttpStatusCode.Gone,
        "Removed route must reject malformed content without deserializing it.");
    var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    Require(payload.Field("error").Field("code").GetString() == "client_authoritative_run_result_removed",
        "Malformed upload must return the stable tombstone error.");
}
using (var empty = new HttpRequestMessage(HttpMethod.Post, "/runs/sync-local-record"))
using (var response = await http.SendAsync(empty, ct))
{
    Require(response.StatusCode == HttpStatusCode.Gone, "Removed route must reject an empty upload.");
    var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    Require(payload.Field("error").Field("code").GetString() == "client_authoritative_run_result_removed",
        "Empty upload must return the stable tombstone error.");
}
Require(await Snapshot(a.AccountId) == initialA && await Snapshot(b.AccountId) == initialB,
    "Unauthenticated and malformed uploads must leave protected DB state unchanged.");
Pass("unauthenticated, malformed and empty uploads are tombstoned without protected DB changes");

await ExpectError("/runs/claim-rewards", new { runId = a.RunId, wins = 33, losses = 3,
    status = "completed", resourceGoldDelta = 999999, cardRewards = new { Goblin = 999999 } }, a.Token,
    HttpStatusCode.BadRequest, "run_not_completed");
Require(await Snapshot(a.AccountId) == initialA, "Unfinished reward claim must leave the protected DB state unchanged.");
Pass("forged completion cannot unlock rewards; unfinished run claim is rejected without mutations");

// Surrender is resolved by the actual shared battle engine. Only its server-produced
// BattleRunResultRecord values are passed to the same service used by Program.cs.
// No SQL statement in this harness updates run counters or completes a run.
await ResolveBattle(a, b, aWins: false, recordB: true);
Require((await ReadStats(a)).Losses == 1 && (await ReadStats(b)).Wins == 1,
    "Server battle loss/win must increment exactly one counter on the respective run.");
await ResolveBattle(a, b, aWins: true, recordB: true);
var afterOne = await ReadStats(a);
Require(afterOne.Wins == 1 && afterOne.Losses == 1 && afterOne.Status == "in_progress" &&
        afterOne.ResourceGold == a.InitialGold && afterOne.Cards == a.InitialCards && afterOne.Grants == 0,
    "Ordinary authoritative result must progress the run without granting premature rewards.");
Pass("actual BattleSession surrender results increment the saved winner and loser runs exactly once");

var beforeInProgressClaim = await Snapshot(a.AccountId);
await ExpectError("/runs/claim-rewards", new { runId = a.RunId }, a.Token,
    HttpStatusCode.BadRequest, "run_not_completed");
Require(await Snapshot(a.AccountId) == beforeInProgressClaim, "In-progress claim must not mutate protected state.");
Pass("in-progress server run still rejects premature reward claims");

await ResolveBattle(a, b, aWins: true, recordB: true);
await ResolveBattle(a, b, aWins: true, recordB: true);
var loser = await ReadStats(b);
Require(loser.Wins == 1 && loser.Losses == 3 && loser.Status == "completed" && loser.Grants == 0,
    "Three server-produced losses must complete the second run without automatically paying rewards.");
await VerifyRewardClaim(b, expectedWins: 1, expectedLosses: 3, expectedGold: 3, expectedCards: 3);
Pass("three authoritative losses complete a run; one earned win pays exactly 3 gold and 3 cards once");

for (var win = 4; win <= 33; win++)
    await ResolveBattle(a, b, aWins: true, recordB: false);
var winner = await ReadStats(a);
Require(winner.Wins == 33 && winner.Losses == 1 && winner.Status == "completed" && winner.Grants == 0,
    "33 server-produced wins must complete the first run without automatically paying rewards.");
await VerifyRewardClaim(a, expectedWins: 33, expectedLosses: 1, expectedGold: 99, expectedCards: 99);
Pass("33 authoritative wins complete a run; earned completion pays exactly 99 gold and 99 cards once");

var finalA = await Snapshot(a.AccountId);
try
{
    await resultStore.ApplyAsync(ResultDeliveryChecks.SoloWin(a), ct);
    throw new InvalidOperationException("Completed run accepted another battle result.");
}
catch (RunServiceException ex) when (ex.Code == "run_result_not_recorded") { }
Require(await Snapshot(a.AccountId) == finalA, "Completed run must reject further results without changing rewards.");
Pass("completed run rejects additional server results without changing its record or rewards");

Console.WriteLine($"PASS: {passCount} checks; {battleCount} progression battles and 5 WebSocket battles plus participant/delivery/connection/matchmaking regressions; {resultCount} progression results; 12 synthetic accounts; zero live DB/server access.");

async Task<TestAccount> DraftAccount(string label)
{
    var auth = await Post("/auth/register", new
    {
        gameId = "authority." + Guid.NewGuid().ToString("N")[..12],
        password = "Synthetic_Only_333!", displayName = label, clientVersion = "0.1.0-dev"
    }, null);
    var token = auth.Field("sessionToken").GetString()!;
    var accountId = auth.Field("account").Field("id").GetGuid();
    var run = await Post("/runs/start", new { mode = "pve" }, token);
    var runId = run.Field("activeRun").Field("id").GetGuid();
    var offer = run.Field("currentOfferCardIds");
    var cards = new List<string>();
    Guid deckId = default;
    for (var pick = 0; pick < 33; pick++)
    {
        var card = offer[0].GetString()!;
        cards.Add(card);
        var selection = await Post("/runs/select-draft-card", new { runId, cardId = card, pickIndex = pick }, token);
        offer = selection.Field("currentOfferCardIds");
        if (pick == 32) deckId = selection.Field("deck").Field("id").GetGuid();
    }
    var account = new TestAccount(accountId, token, runId, deckId, cards, 0, 0);
    var stats = await ReadStats(account);
    Require(stats.Wins == 0 && stats.Losses == 0 && stats.Status == "ready", "Authoritative draft must start at 0/0 ready.");
    return account with { InitialGold = stats.ResourceGold, InitialCards = stats.Cards };
}

async Task ResolveBattle(TestAccount first, TestAccount second, bool aWins, bool recordB)
{
    var session = new BattleSession("run-authority-check-" + Guid.NewGuid().ToString("N"), useServerAiOpponent: false);
    var ca = Connection(first, true);
    var cb = Connection(second, recordB);
    Require(session.TryAddConnection(ca, out var errorA), errorA);
    Require(session.TryAddConnection(cb, out var errorB), errorB);
    Require(session.TryStartBattleIfReady(out var startError), startError);
    Require(session.IsBattleStarted && !session.IsBattleEnded, "Synthetic battle must begin before surrender.");
    var surrendering = aWins ? cb : ca;
    session.ApplyCommand(new ClientBattleCommandMessage
    {
        MatchId = session.MatchId, ActorId = surrendering.AssignedSeatId,
        CommandType = OnlineBattleCommandType.Surrender,
        PlayerToken = surrendering.PlayerToken, Sequence = 1,
    });
    Require(session.IsBattleEnded, "Real surrender command must end the shared-engine battle.");
    var result = session.GetFinalResult()!;
    var records = result.Runs;
    Require(records.Count == (recordB ? 2 : 1), "Only battle seats bound to this test's active runs must produce results.");
    Require(records.Single(r => r.RunId == first.RunId.ToString()).Won == aWins,
        "Authoritative record must match the battle winner.");
    Require(await resultStore.ApplyAsync(result, ct), "New result must be applied.");
    resultCount += records.Count;
    Require(!await resultStore.ApplyAsync(session.GetFinalResult()!, ct), "Repeated result retrieval must be safe after a DB commit.");
    battleCount++;
    ca.Socket.Dispose();
    cb.Socket.Dispose();
}

async Task VerifyRewardClaim(TestAccount account, int expectedWins, int expectedLosses, int expectedGold, int expectedCards)
{
    var claim = await Post("/runs/claim-rewards", new { runId = account.RunId, wins = 33, losses = 3,
        status = "completed", resourceGoldDelta = 999999, ticketDelta = 999999,
        cardRewards = new { Goblin = 999999 } }, account.Token);
    var reward = claim.Field("reward");
    Require(reward.Field("resourceGoldDelta").GetInt64() == expectedGold &&
            reward.Field("ticketDelta").GetInt32() == 0 &&
            reward.Field("cardRewards").EnumerateObject().Sum(p => p.Value.GetInt32()) == expectedCards,
        "Reward payload must match the server-earned wins.");
    var stats = await ReadStats(account);
    Require(stats.Wins == expectedWins && stats.Losses == expectedLosses && stats.Claimed &&
            stats.ResourceGold == account.InitialGold + expectedGold &&
            stats.Cards == account.InitialCards + expectedCards && stats.Grants == 1,
        "Persisted wallet, collection, run and reward ledger must match the earned reward exactly.");
    var snapshot = await Snapshot(account.AccountId);
    await ExpectError("/runs/claim-rewards", new { runId = account.RunId }, account.Token,
        HttpStatusCode.Conflict, "reward_already_claimed");
    Require(await Snapshot(account.AccountId) == snapshot,
        "Repeated reward claim must not change protected rows or pay a second reward.");
}

async Task<JsonElement> Post(string route, object body, string? token)
{
    using var request = Request(route, body, token);
    using var response = await http.SendAsync(request, ct);
    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        throw new InvalidOperationException($"{route} must succeed; HTTP {(int)response.StatusCode}, code {error.Field("error").Field("code").GetString()}.");
    }
    return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
}

async Task ExpectError(string route, object body, string? token, HttpStatusCode status, string error)
{
    using var request = Request(route, body, token);
    using var response = await http.SendAsync(request, ct);
    Require(response.StatusCode == status, $"{route} must return HTTP {(int)status}; received {(int)response.StatusCode}.");
    var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    Require(payload.Field("error").Field("code").GetString() == error, $"{route} must return the expected stable error code {error}.");
}

HttpRequestMessage Request(string route, object body, string? token)
{
    // Existing run endpoints require a positive Content-Length before deserializing.
    var request = new HttpRequestMessage(HttpMethod.Post, route)
        { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return request;
}

async Task<string> Snapshot(Guid accountId)
{
    const string sql = """
        select jsonb_build_object(
          'runs', (select jsonb_agg(to_jsonb(t) order by id) from draft_runs t where account_id=@account),
          'decks', (select jsonb_agg(to_jsonb(t) order by id) from decks t where account_id=@account),
          'wallet', (select to_jsonb(t) from user_wallets t where account_id=@account),
          'cards', (select jsonb_agg(to_jsonb(t) order by card_id) from user_card_collection t where account_id=@account),
          'packs', (select jsonb_agg(to_jsonb(t) order by pack_id) from user_pack_inventory t where account_id=@account),
          'grants', (select jsonb_agg(to_jsonb(t) order by id) from reward_grants t where account_id=@account),
          'transactions', (select jsonb_agg(to_jsonb(t) order by id) from account_transactions t where account_id=@account)
        )::text;
        """;
    await using var command = new NpgsqlCommand(sql, db);
    command.Parameters.AddWithValue("account", accountId);
    return (string)(await command.ExecuteScalarAsync(ct))!;
}

async Task<RunStats> ReadStats(TestAccount account)
{
    const string sql = """
        select r.wins, r.losses, r.status, r.reward_claimed_at is not null, w.resource_gold,
          (select coalesce(sum(copy_count),0)::bigint from user_card_collection where account_id=@account),
          (select count(*) from reward_grants where account_id=@account)
        from draft_runs r join user_wallets w on w.account_id=r.account_id
        where r.id=@run and r.account_id=@account;
        """;
    await using var command = new NpgsqlCommand(sql, db);
    command.Parameters.AddWithValue("account", account.AccountId);
    command.Parameters.AddWithValue("run", account.RunId);
    await using var reader = await command.ExecuteReaderAsync(ct);
    Require(await reader.ReadAsync(ct), "Expected the synthetic run and wallet.");
    return new RunStats(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetBoolean(3),
        reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6));
}

static BattleClientConnection Connection(TestAccount account, bool bindRun)
{
    var id = Guid.NewGuid().ToString("N");
    var connection = new BattleClientConnection(id, new OfflineSocket())
    {
        AccountId = account.AccountId.ToString(), PlayerToken = id,
        RunId = bindRun ? account.RunId.ToString() : "",
        DeckId = bindRun ? account.DeckId.ToString() : "",
    };
    connection.PlayerDeckCardIds.AddRange(account.Cards);
    return connection;
}

void Pass(string message) { passCount++; Console.WriteLine("PASS: " + message); }
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

record TestAccount(Guid AccountId, string Token, Guid RunId, Guid DeckId, IReadOnlyList<string> Cards, long InitialGold, long InitialCards);
record RunStats(int Wins, int Losses, string Status, bool Claimed, long ResourceGold, long Cards, long Grants);

static class JsonFields
{
    public static JsonElement Field(this JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        throw new InvalidOperationException("Expected JSON field: " + name);
    }
}

sealed class OfflineSocket : WebSocket
{
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override string? SubProtocol => null;
    public override WebSocketState State => WebSocketState.Open;
    public override void Abort() { }
    public override void Dispose() { }
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? description, CancellationToken token) => Task.CompletedTask;
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? description, CancellationToken token) => Task.CompletedTask;
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token) => throw new NotSupportedException("No actual battle socket traffic in this harness.");
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken token) => throw new NotSupportedException("No actual battle socket traffic in this harness.");
}
