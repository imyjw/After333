using System.Diagnostics;
using Project333.PvpServer.BattleSessions;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

// Separate engine fixtures; their timing is not attributed to the network server.
static class ComplexEffectChecks
{
 public static object RunBatch()
 {
  var timer=Stopwatch.StartNew();Parallel.For(0,12,new ParallelOptions{MaxDegreeOfParallelism=4},_=>{NuclearChain();PersistentOverlap();RedDragons();});
  return new{Fixtures=36,NuclearChains=12,PersistentOverlaps=12,RedDragonGroups=12,Milliseconds=timer.Elapsed.TotalMilliseconds};
 }
 static BattleState State()
 {
  var deck=Enumerable.Repeat("Goblin",33).ToArray();var s=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,PlayerId.Player));
  s.RestoreRuntimeState(1,PlayerId.Player,PhaseType.Main);s.Player.Resources.Add(new ResourceSet(100,100,100,100));return s;
 }
 static UnitState Victim(string name,PlayerId owner,TileCoord pos,int hp=1000,int magic=0)=>new(name,name,owner,pos,AttackType.Melee,1,hp,true,false,0,magicDefense:magic);
 static void NuclearChain()
 {
  var s=State();BuildingState? first=null;
  foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})for(var col=0;col<3;col++){
   var c=new TileCoord(col,0);var plant=new BuildingState("plant-"+owner+col,"NuclearPowerPlant",owner,c,false,0,30,damageType:DamageType.None);s.GetBoard(owner).Place(c,plant);first??=plant;
  }
  var victim=Victim("chain-target",PlayerId.AI,new TileCoord(4,1));s.AIBoard.Place(victim.Position,victim);first!.CurrentHp=0;
  OccupantDestructionService.ResolveDefeatedOccupants(s);
  if(s.PlayerBoard.EnumerateOccupants().Concat(s.AIBoard.EnumerateOccupants()).Any(o=>o.CardId=="NuclearPowerPlant")||victim.CurrentHp!=820||s.Player.Master.CurrentHp!=153||s.AI.Master.CurrentHp!=153)throw new Exception("Six-plant destruction chain mismatch");
 }
 static void PersistentOverlap()
 {
  var s=State();var inside=Victim("inside",PlayerId.AI,new TileCoord(1,0),magic:4);var outside=Victim("outside",PlayerId.AI,new TileCoord(4,1));var weak=Victim("weak",PlayerId.AI,new TileCoord(0,0),hp:20);
  foreach(var o in new[]{inside,outside,weak})s.AIBoard.Place(o.Position,o);
  var flow=PrototypeCardDefinitions.CreateBattleFlowController(PrototypeCardDefinitions.LoadCardDefinitionProvider());flow.RestoreBattleState(s);
  foreach(var card in new[]{"Firewall","Firewall","BiochemicalBomb"}){s.Player.Hand.Add(card);flow.ExecuteCommand(PlayerId.Player,new CastScriptedSpellCommand(card,PlayerId.AI,new TileCoord(0,0)));}
  for(var i=0;i<4;i++){s.StartNextTurn(i%2==0?PlayerId.AI:PlayerId.Player);new TurnStartService().ResolveTurnStart(s);}
  if(inside.CurrentHp!=784||outside.CurrentHp!=1000||s.AIBoard.GetOccupant(weak.Position)!=null||s.PersistentEffects.Any(e=>!e.IsExpired))throw new Exception("Stacked Firewall/BiochemicalBomb timing/defense/removal mismatch");
 }
 static void RedDragons()
 {
  var s=State();var target=Victim("dragon-target",PlayerId.AI,new TileCoord(0,0),magic:4);s.AIBoard.Place(target.Position,target);
  var flow=PrototypeCardDefinitions.CreateBattleFlowController(PrototypeCardDefinitions.LoadCardDefinitionProvider());flow.RestoreBattleState(s);
  for(var col=0;col<3;col++){s.Player.Hand.Add("RedDragon");flow.ExecuteCommand(PlayerId.Player,new PlayUnitCardCommand("RedDragon",new TileCoord(col,0)));}
  flow.EndTurn();if(target.CurrentHp!=913||s.AI.Master.CurrentHp!=234)throw new Exception("Three Red Dragons end-turn magic damage mismatch");
 }
}