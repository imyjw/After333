using OnlineBattleCommandType = Project333.PvpServer.Messages.OnlineBattleCommandType;
using OnlineBattleEnvelope = Project333.PvpServer.Messages.OnlineBattleEnvelope;
using OnlineBattleMessageType = Project333.PvpServer.Messages.OnlineBattleMessageType;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;

using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(8));
var ct = limit.Token;
var connection = Environment.GetEnvironmentVariable("PROJECT333_DB_CONNECTION") ?? "";
var cs = new NpgsqlConnectionStringBuilder(connection);
var data = Environment.GetEnvironmentVariable("AFTER333_AUTHORITY_TEST_DATA") ?? "";
if(cs.Host != "127.0.0.1" || cs.Port != 15439 || cs.Database != "after333_run_authority" || cs.Username != "after333_authority_test" || !Path.GetFullPath(data).StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("Only fresh isolated test DB permitted");
await using var db = new NpgsqlConnection(connection);
await db.OpenAsync(ct);
await using(var check = new NpgsqlCommand("select current_setting('data_directory'), (select count(*) from accounts)",db))
await using(var reader = await check.ExecuteReaderAsync(ct)) {
    await reader.ReadAsync(ct);
    if(!string.Equals(Path.GetFullPath(reader.GetString(0)),Path.GetFullPath(data),StringComparison.OrdinalIgnoreCase) || reader.GetInt64(1)!=0) throw new Exception("Cluster identity or empty DB guard failed");
}
using var http=new HttpClient { BaseAddress=new Uri("http://127.0.0.1:17339"), Timeout=TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.Add("X-Project333-Client-Version","0.1.0-dev");
var stages = new[]{4,10,20,40,40,40};
var output=Path.GetDirectoryName(data)!;
using var process=Process.GetProcessById(int.Parse(Environment.GetEnvironmentVariable("AFTER333_LOAD_SERVER_PID")!));
var samples=new ConcurrentQueue<object>();
using var samplingStop=new CancellationTokenSource();
var sampling=Task.Run(async()=> {try {while(!samplingStop.IsCancellationRequested){process.Refresh(); samples.Enqueue(new{Utc=DateTime.UtcNow,WorkingMB=process.WorkingSet64/1048576d,PrivateMB=process.PrivateMemorySize64/1048576d,CpuSeconds=process.TotalProcessorTime.TotalSeconds,Handles=process.HandleCount,Threads=process.Threads.Count}); await Task.Delay(500,samplingStop.Token);}}catch(OperationCanceledException){} });
var summaries=new List<object>();
var totalMatches=0;
try {
for(var wave=0;wave<stages.Length;wave++) {
    var count=stages[wave]; Console.WriteLine($"START wave={wave+1} clients={count}");
    var preparation=Stopwatch.StartNew();
    var accounts=new Account[count];
    // Account and 33-card draft creation are real HTTP; cap setup concurrency separately.
    await Parallel.ForEachAsync(Enumerable.Range(0,count),new ParallelOptions{MaxDegreeOfParallelism=4,CancellationToken=ct},async(i,c)=> accounts[i]=await Create(i));
    var setupSeconds=preparation.Elapsed.TotalSeconds;
    var joinTimes=new ConcurrentBag<double>();var commandTimes=new ConcurrentBag<double>();var reconnectTimes=new ConcurrentBag<double>();
    var clients=accounts.Select(a=>new Peer(a)).ToArray();
    process.Refresh();var cpuBefore=process.TotalProcessorTime.TotalSeconds; var timer=Stopwatch.StartNew();
    try {
        await Task.WhenAll(clients.Select(async p=> {var t=Stopwatch.StartNew();await p.Open(ct); await p.Join(false,ct); await p.State(_=>true,ct);joinTimes.Add(t.Elapsed.TotalMilliseconds);}));
        var pairs=clients.GroupBy(p=>p.Match).Select(g=>g.ToArray()).ToArray();
        if(pairs.Length!=count/2 || pairs.Any(p=>p.Length!=2 || p[0].Seat==p[1].Seat)) throw new Exception("Lost or duplicate matchmaking seats");
        Console.WriteLine($"MATCHED wave={wave+1} matches={pairs.Length}");
        await Task.WhenAll(pairs.Select(async pair=> {
            await pair[0].Command(OnlineBattleCommandType.PassMulligan,ct);
            await pair[1].Command(OnlineBattleCommandType.PassMulligan,ct);
            var state=await pair[0].State(s=>s.Phase==PhaseType.Main,ct);
            await pair[1].State(s=>s.Phase==PhaseType.Main,ct);
            for(var turn=0;turn<12;turn++) {
                var active=pair.Single(p=>(int)p.Seat==(int)state.ActivePlayerId);
                // Actual simultaneous melee damage and state broadcasts on every turn.
                var before=state.CombatLogLatestSequence;var t=Stopwatch.StartNew();
                await active.Command(OnlineBattleCommandType.Attack,ct);
                var states=await Task.WhenAll(pair.Select(p=>p.State(s=>s.CombatLogLatestSequence>before,ct)));
                commandTimes.Add(t.Elapsed.TotalMilliseconds);state=states[0];
                if(turn==3) {
                    var reconnect=pair[0]; var other=pair[1];
                    reconnect.Socket.Abort();reconnect.Socket.Dispose();
                    await other.Read(m=>m.BattleEvents.Any(e=>e.EventType.ToString()=="ReconnectGraceStarted"),ct);
                    t.Restart();await reconnect.Open(ct);await reconnect.Join(true,ct);
                    var restored=await reconnect.State(s=>s.TurnNumber==state.TurnNumber,ct);
                    if(JsonSerializer.Serialize(restored.Player,Peer.Json)!=JsonSerializer.Serialize(states[0].Player,Peer.Json) || JsonSerializer.Serialize(restored.Occupants,Peer.Json)!=JsonSerializer.Serialize(states[0].Occupants,Peer.Json)) throw new Exception("Reconnect changed hand/resources/board");
                    reconnectTimes.Add(t.Elapsed.TotalMilliseconds);
                }
                t.Restart();var previous=state.TurnNumber;
                await active.Command(OnlineBattleCommandType.EndTurn,ct);
                states=await Task.WhenAll(pair.Select(p=>p.State(s=>s.TurnNumber>previous,ct)));
                commandTimes.Add(t.Elapsed.TotalMilliseconds);state=states[0];
                await Task.Delay(150,ct);
            }
            var loser=pair.Single(p=>p.Seat==PlayerIdDto.Player);
            await loser.Command(OnlineBattleCommandType.Surrender,ct);
            var ended=await Task.WhenAll(pair.Select(p=>p.State(s=>s.IsEnded,ct)));
            if(ended.Any(s=>!s.HasWinner || (int)s.WinnerId!=(int)PlayerIdDto.AI)) throw new Exception("Unexpected winner");
        }));
        totalMatches+=pairs.Length;
        // Result commit can follow the final socket notification; poll within a fixed bound.
        var persisted=false;
        for(var retry=0;retry<100;retry++) {
            await using var q=new NpgsqlCommand("select (select count(*) from battle_result_receipts), (select count(*) from battle_run_results), (select coalesce(sum(wins),0) from draft_runs),(select coalesce(sum(losses),0) from draft_runs)",db);
            await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);
            if(r.GetInt64(0)==totalMatches && r.GetInt64(1)==2*totalMatches && r.GetInt64(2)==totalMatches && r.GetInt64(3)==totalMatches){persisted=true;break;}
            await Task.Delay(100,ct);
        }
        if(!persisted)throw new Exception("Missing or duplicate result totals");
        foreach(var p in clients) {
            await using var q=new NpgsqlCommand("select wins,losses from draft_runs where id=@id",db);q.Parameters.AddWithValue("id",p.Account.Run);
            await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);
            if(r.GetInt32(0)!=(p.Seat==PlayerIdDto.AI?1:0) || r.GetInt32(1)!=(p.Seat==PlayerIdDto.Player?1:0)) throw new Exception("Wrong account result");
        }
        process.Refresh();
        var summary=new{Wave=wave+1,Clients=count,Matches=pairs.Length,SetupSeconds=setupSeconds,Seconds=timer.Elapsed.TotalSeconds,JoinMs=Stats(joinTimes),CommandMs=Stats(commandTimes),ReconnectMs=Stats(reconnectTimes),CpuCoreSeconds=process.TotalProcessorTime.TotalSeconds-cpuBefore,PrivateMB=process.PrivateMemorySize64/1048576d,WorkingMB=process.WorkingSet64/1048576d,ResultsVerified=true};
        summaries.Add(summary);Console.WriteLine(JsonSerializer.Serialize(summary));
    }finally{foreach(var p in clients){p.Socket.Abort();p.Socket.Dispose();}}
    await Task.Delay(2000,ct);
    using var response=await http.GetAsync("/sessions",ct);response.EnsureSuccessStatusCode();
    var sessions=await response.Content.ReadAsStringAsync(ct);
    File.WriteAllText(Path.Combine(output,$"sessions-after-wave-{wave+1}.json"),sessions);
    using(var document=JsonDocument.Parse(sessions)) if(document.RootElement.GetArrayLength()!=0) throw new Exception("Sessions leaked after clients closed");
    File.WriteAllText(Path.Combine(output,"load-summary.json"),JsonSerializer.Serialize(summaries,new JsonSerializerOptions{WriteIndented=true}));
}
Console.WriteLine("SETTLING: observing 80 seconds after final disconnect without forced GC");
await Task.Delay(TimeSpan.FromSeconds(80),ct);
if(Directory.EnumerateFiles(Path.Combine(output,"ServerResultOutbox"),"*.json",SearchOption.AllDirectories).Any()) throw new Exception("Pending result outbox after settling");
Console.WriteLine($"PASS load: {stages.Sum()} clients, {totalMatches} complete matches, {totalMatches*2} per-account results, bounded reconnects");
}finally{samplingStop.Cancel();await sampling;File.WriteAllText(Path.Combine(output,"load-memory.json"),JsonSerializer.Serialize(samples,new JsonSerializerOptions{WriteIndented=true}));}

async Task<Account> Create(int i){
    var auth=await Post("/auth/register",new{gameId="load."+Guid.NewGuid().ToString("N")[..16],password="Synthetic_Only_333!",displayName="load-"+i,clientVersion="0.1.0-dev"},null);
    var token=Field(auth,"sessionToken").GetString()!;var id=Field(Field(auth,"account"),"id").GetGuid();
    var run=await Post("/runs/start",new{mode="pvp"},token);var runId=Field(Field(run,"activeRun"),"id").GetGuid();var offer=Field(run,"currentOfferCardIds");Guid deck=default;
    for(var pick=0;pick<33;pick++){var result=await Post("/runs/select-draft-card",new{runId,cardId=offer[0].GetString(),pickIndex=pick},token);offer=Field(result,"currentOfferCardIds");if(pick==32)deck=Field(Field(result,"deck"),"id").GetGuid();}
    return new Account(id,token,runId,deck);
}
async Task<JsonElement> Post(string path,object body,string? token){using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json")};if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);using var response=await http.SendAsync(request,ct);if(!response.IsSuccessStatusCode)throw new Exception($"HTTP {path}: {(int)response.StatusCode}");using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return document.RootElement.Clone();}
static JsonElement Field(JsonElement e,string name)=>e.EnumerateObject().First(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)).Value;
static object Stats(IEnumerable<double> values){var a=values.Order().ToArray();double P(double p)=>a[Math.Clamp((int)Math.Ceiling(a.Length*p)-1,0,a.Length-1)];return new{Count=a.Length,P50=P(.5),P95=P(.95),P99=P(.99),Max=a[^1]};}
record Account(Guid Id,string Token,Guid Run,Guid Deck);
sealed class Peer(Account account){
    public static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,Converters={new JsonStringEnumConverter()}};
    public Account Account=account; public ClientWebSocket Socket=new();public string Match="";public string ConnectionId="";public PlayerIdDto Seat;long sequence;
    public async Task Open(CancellationToken ct){Socket=new();Socket.Options.SetRequestHeader("X-Project333-Client-Version","0.1.0-dev");await Socket.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"),ct);}
    public async Task Join(bool reconnect,CancellationToken ct){await Send(new(){MessageType=OnlineBattleMessageType.JoinMatch,UseMatchmakingQueue=true,UseServerAiOpponent=false,MatchId=Match,SessionToken=Account.Token,PlayerToken=Account.Id.ToString(),RunId=Account.Run.ToString(),DeckId=Account.Deck.ToString(),IsReconnectAttempt=reconnect,PreviousConnectionId=reconnect?ConnectionId:""},ct);var assigned=await Read(m=>m.HasAssignedSeat && m.AccountId==Account.Id.ToString(),ct);if(reconnect && (assigned.MatchId!=Match || assigned.AssignedSeatId!=Seat))throw new Exception("Reconnect changed match/seat");Match=assigned.MatchId;Seat=assigned.AssignedSeatId;ConnectionId=assigned.ConnectionId;sequence=0;}
    public Task Command(OnlineBattleCommandType type,CancellationToken ct)=>Send(new(){MessageType=OnlineBattleMessageType.ClientCommand,ClientCommand=new(){MatchId=Match,PlayerToken=Account.Id.ToString(),AccountId=Account.Id.ToString(),ActorId=Seat,Sequence=++sequence,CommandType=type,SourceCoord=new(){Column=2,Row=1},TargetCoord=new(){Column=2,Row=1},TargetOwnerId=Seat==PlayerIdDto.Player?PlayerIdDto.AI:PlayerIdDto.Player}},ct);
    Task Send(OnlineBattleEnvelope e,CancellationToken ct)=>Socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(e,Json)),WebSocketMessageType.Text,true,ct);
    public async Task<BattleStateViewDto> State(Func<BattleStateViewDto,bool> predicate,CancellationToken ct)=>(await Read(m=>m.StateView!=null && predicate(m.StateView),ct)).StateView!;
    public async Task<OnlineBattleEnvelope> Read(Func<OnlineBattleEnvelope,bool> predicate,CancellationToken ct){using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(30));var buffer=new byte[32768];for(var n=0;n<300;n++){using var stream=new MemoryStream();WebSocketReceiveResult part;do{part=await Socket.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token);if(part.MessageType==WebSocketMessageType.Close)throw new Exception("Unexpected socket close");stream.Write(buffer,0,part.Count);if(stream.Length>4*1024*1024)throw new Exception("Oversized response");}while(!part.EndOfMessage);var m=JsonSerializer.Deserialize<OnlineBattleEnvelope>(stream.ToArray(),Json)!;if(m.Error!=null)throw new Exception("Battle error: "+m.Error.Code);if(predicate(m))return m;}throw new Exception("Response not observed");}
}