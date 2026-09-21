using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using Project333.PvpServer.BattleSessions;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Infrastructure.Data;
using Msg = Project333.PvpServer.Messages;

// Synthetic, loopback-only workload. No production endpoint or credential arguments.
static class SoakChecks
{
 static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,Converters={new JsonStringEnumConverter()}};
 static readonly HttpClient Http=new(){BaseAddress=new Uri("http://127.0.0.1:17339"),Timeout=TimeSpan.FromSeconds(30)};
 static readonly SemaphoreSlim SetupGate=new(4);
 static readonly ConcurrentDictionary<string,long> Actions=new();
 static readonly ConcurrentQueue<object> Results=new(),Samples=new();
 static readonly ConcurrentDictionary<string,bool> Completed=new();
 static readonly ConcurrentQueue<double> Joins=new(),Commands=new(),Reconnects=new(),AiWaits=new();
 static CancellationTokenSource? Stop; static int games,pvpGames,pveGames,participants,rewardClaims,draws; static string output="",connection="";
 static readonly Stopwatch Wall=new(); static string phase="ramp"; static readonly ConcurrentDictionary<int,ConcurrentQueue<double>> CommandWindows=new();
 static readonly Project333.Runtime.Infrastructure.Data.ICardDefinitionProvider Provider=PrototypeCardDefinitions.LoadCardDefinitionProvider();
 public static async Task Run(int seconds,int settle,int maxMatches)
 {
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(seconds+settle+900));var ct=deadline.Token;Stop=deadline;
  connection=Environment.GetEnvironmentVariable("PROJECT333_DB_CONNECTION")??"";
  var data=Environment.GetEnvironmentVariable("AFTER333_AUTHORITY_TEST_DATA")??"";var cs=new NpgsqlConnectionStringBuilder(connection);
  if(cs.Host!="127.0.0.1"||cs.Port!=15439||cs.Database!="after333_run_authority"||cs.Username!="after333_authority_test"||!Path.GetFullPath(data).StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Isolation settings rejected");
  output=Path.GetDirectoryName(data)!;
  await using(var db=new NpgsqlConnection(connection)){await db.OpenAsync(ct);await using var q=new NpgsqlCommand("select current_setting('data_directory'),(select count(*) from accounts)",db);await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);if(!string.Equals(Path.GetFullPath(r.GetString(0)),Path.GetFullPath(data),StringComparison.OrdinalIgnoreCase)||r.GetInt64(1)!=0)throw new Exception("Fresh isolated cluster required");}
  Http.DefaultRequestHeaders.Add("X-Project333-Client-Version","0.1.0-dev");
  using var sampleStop=new CancellationTokenSource();Wall.Start();var sampling=Sample(sampleStop.Token);_ = sampling.ContinueWith(_=>deadline.Cancel(),TaskContinuationOptions.OnlyOnFaulted);
  bool passed=false;
  try{
   Console.WriteLine($"SOAK setup seconds={seconds} settle={settle} maxMatches={maxMatches}");
   foreach(var n in new[]{4,10,20,40}.Where(n=>n<=maxMatches*2)){var pool=new Acc?[n];await PvpWave(pool,"ramp-"+n,ct);await EmptySessions(ct);}
   phase="sustained";var sustained=Stopwatch.StartNew();var aiCount=Math.Max(1,maxMatches/5);var pvpCount=maxMatches-aiCount;
   var accounts=new Acc?[pvpCount*2];
   async Task Effects(){while(sustained.Elapsed.TotalSeconds<seconds){Results.Enqueue(new{Label="engine-fixtures",Batch=ComplexEffectChecks.RunBatch()});await Task.Delay(15000,ct);}}
   async Task PvpLoop(){var wave=0;while(sustained.Elapsed.TotalSeconds<seconds){await PvpWave(accounts,"soak-"+(++wave),ct);}}
   async Task PveLoop(int worker){Acc? a=null;while(sustained.Elapsed.TotalSeconds<seconds){a=await Ready(a,true,ct);await Pve(a,ct);}}
   await Task.WhenAll(new[]{Guard(PvpLoop),Guard(Effects)}.Concat(Enumerable.Range(0,aiCount).Select(i=>Guard(()=>PveLoop(i)))));
   phase="settling";Console.WriteLine($"DRAIN games={games} participants={participants} rewardClaims={rewardClaims}; settle={settle}s");
   await EmptySessions(ct);await VerifyTotals(ct);
   await Task.Delay(TimeSpan.FromSeconds(settle),ct);
   await EmptySessions(ct);await VerifyTotals(ct);
   if(Directory.EnumerateFiles(Path.Combine(output,"ServerResultOutbox"),"*.json").Any())throw new Exception("Pending result journal after settling");
   passed=true;Console.WriteLine($"PASS SOAK pvp={pvpGames} pve={pveGames} results={games} participantResults={participants} rewards={rewardClaims} draws={draws}");
  }finally{
   sampleStop.Cancel();await sampling;
   File.WriteAllText(Path.Combine(output,"soak-summary.json"),JsonSerializer.Serialize(new{Passed=passed,Seconds=Wall.Elapsed.TotalSeconds,RequestedSoakSeconds=seconds,SettleSeconds=settle,MaxMatches=maxMatches,Games=games,PvpGames=pvpGames,PveGames=pveGames,Participants=participants,RewardClaims=rewardClaims,Draws=draws,JoinMs=Stats(Joins),CommandMs=Stats(Commands),CommandWindows=CommandWindows.OrderBy(p=>p.Key).Select(p=>new{Minute=p.Key,Latency=Stats(p.Value)}).ToArray(),ReconnectMs=Stats(Reconnects),AiTurnWaitMs=Stats(AiWaits),Actions=Actions.OrderBy(p=>p.Key).ToDictionary(p=>p.Key,p=>p.Value),Results=Results.ToArray()},new JsonSerializerOptions{WriteIndented=true}));
   File.WriteAllText(Path.Combine(output,"soak-resources.json"),JsonSerializer.Serialize(Samples,new JsonSerializerOptions{WriteIndented=true}));
  }
 }
 static async Task Guard(Func<Task> action){try{await action();}catch(Exception ex){Console.WriteLine("WORKER FAILURE: "+ex);Stop?.Cancel();throw;}}
 static async Task Sample(CancellationToken ct)
 {
  using var process=Process.GetProcessById(int.Parse(Environment.GetEnvironmentVariable("AFTER333_LOAD_SERVER_PID")!));
  using var harness=Process.GetCurrentProcess();int count=0;
  try{while(!ct.IsCancellationRequested){
   process.Refresh();harness.Refresh();
   await using var db=new NpgsqlConnection(connection);await db.OpenAsync(ct);
   await using var q=new NpgsqlCommand("select count(*),count(*) filter(where state='active'),count(*) filter(where state='idle in transaction'),count(*) filter(where wait_event_type='Lock'),pg_database_size(current_database()) from pg_stat_activity where datname=current_database()",db);
   await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);
   using var sessions=JsonDocument.Parse(await Http.GetStringAsync("/sessions",ct));
   var sample=new{Phase=phase,Utc=DateTimeOffset.UtcNow,Seconds=Wall.Elapsed.TotalSeconds,PrivateMB=process.PrivateMemorySize64/1048576d,WorkingMB=process.WorkingSet64/1048576d,CpuSeconds=process.TotalProcessorTime.TotalSeconds,Threads=process.Threads.Count,Handles=process.HandleCount,HarnessPrivateMB=harness.PrivateMemorySize64/1048576d,HarnessCpuSeconds=harness.TotalProcessorTime.TotalSeconds,DbConnections=r.GetInt64(0),DbActive=r.GetInt64(1),DbIdleTransaction=r.GetInt64(2),DbLockWait=r.GetInt64(3),DbMB=r.GetInt64(4)/1048576d,Sessions=sessions.RootElement.GetArrayLength(),Completed=Volatile.Read(ref games),PendingResults=Directory.EnumerateFiles(Path.Combine(output,"ServerResultOutbox"),"*.json").Count()};
   Samples.Enqueue(sample);
   if(++count%3==0){Console.WriteLine("METRIC "+JsonSerializer.Serialize(sample));File.WriteAllText(Path.Combine(output,"soak-resources-live.json"),JsonSerializer.Serialize(Samples));}
   if(sample.PrivateMB>2048)throw new Exception("Isolated server exceeded 2GiB memory safety bound");
   await Task.Delay(5000,ct);
  }}catch(OperationCanceledException)when(ct.IsCancellationRequested){}
 }
 static async Task<Acc> Ready(Acc? old,bool pve,CancellationToken ct)
 {
  if(old!=null&&old.Wins<33&&old.Losses<3)return old;
  if(old!=null){var before=await Post("/me",null,old.Token,ct);var claim=await Post("/runs/claim-rewards",new{runId=old.Run},old.Token,ct);var reward=Field(claim,"reward");var gold=Field(reward,"resourceGoldDelta").GetInt64();var cards=Field(reward,"cardRewards").EnumerateObject().Sum(p=>p.Value.GetInt32());if(gold!=old.Wins*3||cards!=old.Wins*3)throw new Exception("Earned reward mismatch");var after=await Post("/me",null,old.Token,ct);if(Field(Field(after,"wallet"),"resourceGold").GetInt64()!=Field(Field(before,"wallet"),"resourceGold").GetInt64()+gold)throw new Exception("Reward wallet mismatch");await Expect("/runs/claim-rewards",new{runId=old.Run},old.Token,409,ct);var again=await Post("/me",null,old.Token,ct);if(after.GetRawText()!=again.GetRawText())throw new Exception("Duplicate reward changed account");Interlocked.Increment(ref rewardClaims);}
  await SetupGate.WaitAsync(ct);
  try{
   var auth=await Post("/auth/register",new{gameId="soak."+Guid.NewGuid().ToString("N")[..16],password="Synthetic_Only_333!",displayName="soak",clientVersion="0.1.0-dev"},null,ct);
   var token=Field(auth,"sessionToken").GetString()!;var id=Field(Field(auth,"account"),"id").GetGuid();
   var run=await Post("/runs/start",new{mode=pve?"pve":"pvp"},token,ct);var runId=Field(Field(run,"activeRun"),"id").GetGuid();var offer=Field(run,"currentOfferCardIds");Guid deck=default;
   for(var pick=0;pick<33;pick++){var selected=offer.EnumerateArray().Select(e=>e.GetString()!).OrderBy(CardRank).First();var result=await Post("/runs/select-draft-card",new{runId,cardId=selected,pickIndex=pick},token,ct);offer=Field(result,"currentOfferCardIds");if(pick==32)deck=Field(Field(result,"deck"),"id").GetGuid();}
   return new Acc(id,token,runId,deck);
  }finally{SetupGate.Release();}
 }
 static int CardRank(string id)=>id switch{"NuclearPowerPlant"=>0,"RedDragon"=>1,"BiochemicalBomb"=>2,"Firewall"=>3,"ManaPond"=>4,"GoldMiner"=>5,"ManaWeaver"=>6,_=>20};
 static async Task PvpWave(Acc?[] pool,string label,CancellationToken ct)
 {
  var prep=Stopwatch.StartNew();await Task.WhenAll(Enumerable.Range(0,pool.Length).Select(async i=>pool[i]=await Ready(pool[i],false,ct)));
  var peers=pool.Select(a=>new Peer(a!,false)).ToArray();var timer=Stopwatch.StartNew();Console.WriteLine($"WAVE {label} clients={peers.Length} setupSeconds={prep.Elapsed.TotalSeconds:F1}");
  try{
   await Task.WhenAll(peers.Select(async p=>{var t=Stopwatch.StartNew();await p.Open(ct);await p.Join(false,ct);await p.State(_=>true,ct);Joins.Enqueue(t.Elapsed.TotalMilliseconds);}));
   var pairs=peers.GroupBy(p=>p.Match).Select(g=>g.ToArray()).ToArray();if(pairs.Any(p=>p.Length!=2||p[0].Seat==p[1].Seat))throw new Exception("Matchmaking seat mismatch");
   await Task.WhenAll(pairs.Select(pair=>Guard(()=>PlayPair(pair,ct))));
   Results.Enqueue(new{Label=label,Clients=peers.Length,Matches=pairs.Length,Seconds=timer.Elapsed.TotalSeconds,SetupSeconds=prep.Elapsed.TotalSeconds});
  }finally{await Task.WhenAll(peers.Select(p=>p.Close()));}
 }
 static async Task PlayPair(Peer[] pair,CancellationToken ct)
 {
  foreach(var p in pair)await p.Command(new(){CommandType=Msg.OnlineBattleCommandType.PassMulligan},ct);
  var states=await Task.WhenAll(pair.Select(p=>p.State(s=>s.Phase==PhaseType.Main,ct)));var turn=0;
  while(!states[0].IsEnded&&turn<20){
   var active=Array.FindIndex(pair,p=>(int)p.Seat==(int)states[0].ActivePlayerId);
   for(var action=0;action<8&&!states[0].IsEnded;action++){
    var cmd=Choose(states[active],pair[active].Seat,action>=7);var previous=states[0];var t=Stopwatch.StartNew();await pair[active].Command(cmd,ct);
    states=await Task.WhenAll(pair.Select(p=>p.State(s=>Advanced(previous,s,cmd),ct)));Measure(t.Elapsed.TotalMilliseconds);Count(cmd.CommandType+":"+cmd.CardId);
    if(cmd.CommandType==Msg.OnlineBattleCommandType.EndTurn)break;
    await Task.Delay(120,ct);
   }
   if(turn++==3&&!states[0].IsEnded){
    var before=states[0];await pair[0].Close();await pair[1].Read(m=>m.BattleEvents.Any(e=>e.EventType.ToString()=="ReconnectGraceStarted"),ct);
    var t=Stopwatch.StartNew();await pair[0].Open(ct);await pair[0].Join(true,ct);var restored=await pair[0].State(s=>s.TurnNumber==before.TurnNumber,ct);CheckRestored(before,restored);Reconnects.Enqueue(t.Elapsed.TotalMilliseconds);states[0]=restored;
   }
   await Task.Delay(250,ct);
  }
  if(!states[0].IsEnded){await pair[0].Command(new(){CommandType=Msg.OnlineBattleCommandType.Surrender},ct);states=await Task.WhenAll(pair.Select(p=>p.State(s=>s.IsEnded,ct)));}
  foreach(var p in pair)await Persist(p,states[0],ct);
  Done(pair[0].Match,false,states[0].IsDraw,2);
 }
 static async Task Pve(Acc a,CancellationToken ct)
 {
  var p=new Peer(a,true);var timer=Stopwatch.StartNew();
  try{
   await p.Open(ct);await p.Join(false,ct);var state=await p.State(_=>true,ct);Joins.Enqueue(timer.Elapsed.TotalMilliseconds);
   await p.Command(new(){CommandType=Msg.OnlineBattleCommandType.PassMulligan},ct);
   state=await p.State(s=>s.IsEnded||s.Phase==PhaseType.Main,ct);var ownTurns=0;
   while(!state.IsEnded&&ownTurns<12){
    if((int)state.ActivePlayerId!=(int)p.Seat){var wait=Stopwatch.StartNew();state=await p.State(s=>s.IsEnded||s.Phase==PhaseType.Main&&(int)s.ActivePlayerId==(int)p.Seat,ct);AiWaits.Enqueue(wait.Elapsed.TotalMilliseconds);if(state.IsEnded)break;}
    if(ownTurns==2){var before=state;await p.Close();await Task.Delay(500,ct);var t=Stopwatch.StartNew();await p.Open(ct);await p.Join(true,ct);state=await p.State(s=>s.TurnNumber==before.TurnNumber,ct);CheckRestored(before,state);Reconnects.Enqueue(t.Elapsed.TotalMilliseconds);}
    for(var action=0;action<8&&!state.IsEnded;action++){
     var cmd=Choose(state,p.Seat,action>=7);var previous=state;var t=Stopwatch.StartNew();await p.Command(cmd,ct);state=await p.State(s=>Advanced(previous,s,cmd),ct);Measure(t.Elapsed.TotalMilliseconds);Count(cmd.CommandType+":"+cmd.CardId);
     if(cmd.CommandType==Msg.OnlineBattleCommandType.EndTurn)break;await Task.Delay(120,ct);
    }
    ownTurns++;
   }
   if(!state.IsEnded){await p.Command(new(){CommandType=Msg.OnlineBattleCommandType.Surrender},ct);state=await p.State(s=>s.IsEnded,ct);}
   await Persist(p,state,ct);Done(p.Match,true,state.IsDraw,1);Results.Enqueue(new{Label="pve",Matches=1,Seconds=timer.Elapsed.TotalSeconds});
  }finally{await p.Close();}
 }
 static void Done(string match,bool pve,bool draw,int players){if(!Completed.TryAdd(match,true))throw new Exception("Duplicate match completion");Interlocked.Increment(ref games);if(!draw)Interlocked.Add(ref participants,players);if(pve)Interlocked.Increment(ref pveGames);else Interlocked.Increment(ref pvpGames);if(draw)Interlocked.Increment(ref draws);}
 static async Task Persist(Peer p,BattleStateViewDto end,CancellationToken ct)
 {
  if(end.IsDraw){}else if((int)end.WinnerId==(int)p.Seat)p.Account.Wins++;else p.Account.Losses++;
  await using var db=new NpgsqlConnection(connection);await db.OpenAsync(ct);
  for(var retry=0;retry<100;retry++){await using var q=new NpgsqlCommand("select wins,losses,exists(select 1 from battle_result_receipts where match_id=@match) from draft_runs where id=@id",db);q.Parameters.AddWithValue("id",p.Account.Run);q.Parameters.AddWithValue("match",p.Match);await using var r=await q.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)&&r.GetInt32(0)==p.Account.Wins&&r.GetInt32(1)==p.Account.Losses&&r.GetBoolean(2))return;await Task.Delay(100,ct);}throw new Exception("Missing/duplicate/wrong run result");
 }
 static async Task VerifyTotals(CancellationToken ct){await using var db=new NpgsqlConnection(connection);await db.OpenAsync(ct);await using var q=new NpgsqlCommand("select (select count(*) from battle_result_receipts),(select count(*) from battle_run_results),(select coalesce(sum(wins+losses),0) from draft_runs)",db);await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);if(r.GetInt64(0)!=games||r.GetInt64(1)!=participants||r.GetInt64(2)!=participants)throw new Exception("Aggregate result duplication or loss");}
 static async Task EmptySessions(CancellationToken ct){for(var i=0;i<100;i++){using var d=JsonDocument.Parse(await Http.GetStringAsync("/sessions",ct));if(d.RootElement.GetArrayLength()==0)return;await Task.Delay(200,ct);}throw new Exception("Session cleanup timeout");}
 static void CheckRestored(BattleStateViewDto a,BattleStateViewDto b){if(a.ViewerId!=b.ViewerId||a.ActivePlayerId!=b.ActivePlayerId||a.Phase!=b.Phase||JsonSerializer.Serialize(a.Player,Json)!=JsonSerializer.Serialize(b.Player,Json)||JsonSerializer.Serialize(a.Occupants,Json)!=JsonSerializer.Serialize(b.Occupants,Json)||JsonSerializer.Serialize(a.PersistentEffects,Json)!=JsonSerializer.Serialize(b.PersistentEffects,Json))throw new Exception("Reconnect changed visible state");}
 static bool Advanced(BattleStateViewDto before,BattleStateViewDto after,Msg.ClientBattleCommandMessage cmd)=>after.IsEnded||after.TurnNumber>before.TurnNumber||(cmd.CommandType!=Msg.OnlineBattleCommandType.EndTurn&&after.CombatLogLatestSequence>before.CombatLogLatestSequence);
 static Msg.ClientBattleCommandMessage Choose(BattleStateViewDto view,Msg.PlayerIdDto seat,bool forceEnd)
 {
  if(forceEnd)return new(){CommandType=Msg.OnlineBattleCommandType.EndTurn};
  foreach(var card in view.Player.VisibleHandCards.OrderBy(c=>CardRank(c.CardId))){
   var definition=Provider.GetRequired(card.CardId);var cost=definition.Cost;var resources=view.Player.Resources;
   if(cost.Gold+Math.Max(0,cost.Mana-resources.Mana)+Math.Max(0,cost.Qi-resources.Qi)+Math.Max(0,cost.Power-resources.Power)>resources.Gold)continue;
   var coord=Enumerable.Range(0,10).Select(i=>new TileCoord(i/2,i%2)).FirstOrDefault(c=>!view.Occupants.Any(o=>(int)o.OwnerId==(int)seat&&o.Coord.Column==c.Column&&o.Coord.Row==c.Row));
   // TileCoord is a value type; explicitly check occupancy again if the board is full.
   if(definition is UnitCardDefinition||definition is BuildingCardDefinition){
    if(view.Occupants.Any(o=>(int)o.OwnerId==(int)seat&&o.Coord.Column==coord.Column&&o.Coord.Row==coord.Row))continue;
    IBattleCommand local=definition is UnitCardDefinition?new PlayUnitCardCommand(card.CardId,coord,card.RuntimeId):new PlayBuildingCardCommand(card.CardId,coord,card.RuntimeId);
    if(Valid(view,local))return new(){CommandType=definition is UnitCardDefinition?Msg.OnlineBattleCommandType.PlayUnitCard:Msg.OnlineBattleCommandType.PlayBuildingCard,CardId=card.CardId,HandCardRuntimeId=card.RuntimeId,TargetCoord=new(){Column=coord.Column,Row=coord.Row}};
   }else if(card.CardId is "ManaStone" or "ManaStoneBundle" or "PowerBank"){
    if(Valid(view,new CastScriptedSpellCommand(card.CardId,card.RuntimeId)))return new(){CommandType=Msg.OnlineBattleCommandType.CastScriptedSpell,CardId=card.CardId,HandCardRuntimeId=card.RuntimeId};
   }else if(card.CardId is "Firewall" or "BiochemicalBomb" or "firebolt"){
    var target=new TileCoord(2,1);IBattleCommand local=card.CardId=="firebolt"?new CastDamageSpellCommand(card.CardId,PlayerId.AI,target,card.RuntimeId):new CastScriptedSpellCommand(card.CardId,PlayerId.AI,target,card.RuntimeId);
    if(Valid(view,local))return new(){CommandType=card.CardId=="firebolt"?Msg.OnlineBattleCommandType.CastDamageSpell:Msg.OnlineBattleCommandType.CastScriptedSpell,CardId=card.CardId,HandCardRuntimeId=card.RuntimeId,HasTarget=true,TargetOwnerId=seat==Msg.PlayerIdDto.Player?Msg.PlayerIdDto.AI:Msg.PlayerIdDto.Player,TargetCoord=new(){Column=target.Column,Row=target.Row}};
   }
  }
  var state=BattleStateViewProjector.CreateLocalPerspectiveState(view);
  foreach(var o in view.Occupants.Where(o=>(int)o.OwnerId==(int)seat&&o.RemainingAttacksThisTurn>0&&!o.HasSummoningSickness&&!o.IsDrained&&!o.IsSealbound)){
   var source=new TileCoord(o.Coord.Column,o.Coord.Row);
   foreach(var target in new TargetingService().GetLegalTargets(state,PlayerId.Player,source))if(Valid(view,new AttackCommand(source,target)))return new(){CommandType=Msg.OnlineBattleCommandType.Attack,SourceCoord=new(){Column=source.Column,Row=source.Row},TargetCoord=new(){Column=target.Column,Row=target.Row}};
  }
  return new(){CommandType=Msg.OnlineBattleCommandType.EndTurn};
 }
 static bool Valid(BattleStateViewDto view,IBattleCommand cmd){try{var flow=PrototypeCardDefinitions.CreateBattleFlowController(Provider);flow.RestoreBattleState(BattleStateViewProjector.CreateLocalPerspectiveState(view));flow.ExecuteCommand(PlayerId.Player,cmd);return true;}catch(InvalidOperationException){return false;}catch(ArgumentException){return false;}}
 static void Measure(double milliseconds){Commands.Enqueue(milliseconds);CommandWindows.GetOrAdd((int)Wall.Elapsed.TotalMinutes,_=>new()).Enqueue(milliseconds);}
 static void Count(string action)=>Actions.AddOrUpdate(action,1,(_,v)=>v+1);
 static async Task<JsonElement> Post(string path,object? body,string? token,CancellationToken ct){using var request=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,path);if(body!=null)request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);using var response=await Http.SendAsync(request,ct);if(!response.IsSuccessStatusCode)throw new Exception($"HTTP {path}: {(int)response.StatusCode}");using var d=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return d.RootElement.Clone();}
 static async Task Expect(string path,object body,string token,int code,CancellationToken ct){using var r=new HttpRequestMessage(HttpMethod.Post,path){Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json")};r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);using var response=await Http.SendAsync(r,ct);if((int)response.StatusCode!=code)throw new Exception("Unexpected rejection status");}
 static JsonElement Field(JsonElement e,string name)=>e.EnumerateObject().First(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)).Value;
 static object Stats(IEnumerable<double> values){var a=values.Order().ToArray();double P(double p)=>a.Length==0?0:a[Math.Clamp((int)Math.Ceiling(a.Length*p)-1,0,a.Length-1)];return new{Count=a.Length,P50=P(.5),P95=P(.95),P99=P(.99),Max=a.Length==0?0:a[^1]};}
 sealed class Acc(Guid id,string token,Guid run,Guid deck){public Guid Id=id,Run=run,Deck=deck;public string Token=token;public int Wins,Losses;}
 sealed class Peer(Acc account,bool pve)
 {
  public Acc Account=account;public Msg.PlayerIdDto Seat;public string Match=pve?"soak-"+Guid.NewGuid().ToString("N"):"";string connectionId="";long sequence;ClientWebSocket socket=new();CancellationTokenSource? life;Task? heartbeat;readonly SemaphoreSlim sendGate=new(1);
  public async Task Open(CancellationToken ct){socket=new();socket.Options.SetRequestHeader("X-Project333-Client-Version","0.1.0-dev");await socket.ConnectAsync(new Uri("ws://127.0.0.1:17339/battle"),ct);life=CancellationTokenSource.CreateLinkedTokenSource(ct);heartbeat=Task.Run(async()=>{try{while(!life.IsCancellationRequested){await Task.Delay(3000,life.Token);await Send(new(){MessageType=Msg.OnlineBattleMessageType.KeepAlive},life.Token);}}catch(OperationCanceledException){}catch(WebSocketException)when(life.IsCancellationRequested){}},life.Token);}
  public async Task Join(bool reconnect,CancellationToken ct){await Send(new(){MessageType=Msg.OnlineBattleMessageType.JoinMatch,UseMatchmakingQueue=!pve,UseServerAiOpponent=pve,MatchId=Match,SessionToken=Account.Token,PlayerToken=Account.Id.ToString(),RunId=Account.Run.ToString(),DeckId=Account.Deck.ToString(),IsReconnectAttempt=reconnect,PreviousConnectionId=reconnect?connectionId:""},ct);var assigned=await Read(m=>m.HasAssignedSeat&&m.AccountId==Account.Id.ToString(),ct);if(reconnect&&(assigned.MatchId!=Match||assigned.AssignedSeatId!=Seat))throw new Exception("Reconnect seat/match mismatch");Match=assigned.MatchId;Seat=assigned.AssignedSeatId;connectionId=assigned.ConnectionId;sequence=0;}
  public Task Command(Msg.ClientBattleCommandMessage cmd,CancellationToken ct){cmd.MatchId=Match;cmd.PlayerToken=Account.Id.ToString();cmd.AccountId=Account.Id.ToString();cmd.ActorId=Seat;cmd.Sequence=++sequence;return Send(new(){MessageType=Msg.OnlineBattleMessageType.ClientCommand,ClientCommand=cmd},ct);}
  async Task Send(Msg.OnlineBattleEnvelope m,CancellationToken ct){await sendGate.WaitAsync(ct);try{await socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(m,Json)),WebSocketMessageType.Text,true,ct);}finally{sendGate.Release();}}
  public async Task<BattleStateViewDto> State(Func<BattleStateViewDto,bool> predicate,CancellationToken ct)=>(await Read(m=>m.StateView!=null&&predicate(m.StateView),ct)).StateView!;
  public async Task<Msg.OnlineBattleEnvelope> Read(Func<Msg.OnlineBattleEnvelope,bool> predicate,CancellationToken ct){using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(120));var buffer=new byte[32768];for(var n=0;n<1000;n++){using var stream=new MemoryStream();WebSocketReceiveResult part;do{part=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),deadline.Token);if(part.MessageType==WebSocketMessageType.Close)throw new Exception("Unexpected socket close: "+part.CloseStatusDescription);stream.Write(buffer,0,part.Count);if(stream.Length>4*1024*1024)throw new Exception("Response too large");}while(!part.EndOfMessage);var m=JsonSerializer.Deserialize<Msg.OnlineBattleEnvelope>(stream.ToArray(),Json)!;if(m.Error!=null)throw new Exception("Battle error: "+m.Error.Code);if(pve){foreach(var e in m.BattleEvents.Where(e=>e.SourceOwnerId==Msg.PlayerIdDto.AI))Count("ObservedServerAI:"+e.EventType+":"+e.SourceCardId);}if(m.StateView!=null){foreach(var o in m.StateView.Occupants)if(o.OwnerId!=m.StateView.ViewerId&&o.CardId=="RedDragon")Count("ObservedOpponent:RedDragon");}if(predicate(m))return m;}throw new Exception("Expected message absent");}
  public async Task Close(){life?.Cancel();socket.Abort();if(heartbeat!=null){try{await heartbeat;}catch(OperationCanceledException){}}socket.Dispose();life?.Dispose();heartbeat=null;life=null;}
 }
}