using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using LogEntry = Project333.Runtime.Application.Online.BattleCombatLogEntryDto;

internal static class NuclearChecks
{
    public static void Run()
    {
        var provider=PrototypeCardDefinitions.LoadCardDefinitionProvider();
        foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var flow=PrototypeCardDefinitions.CreateBattleFlowController(provider);
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,owner));
            state.SetPhase(PhaseType.Main);flow.RestoreBattleState(state);
            var enemy=state.GetOpponent(owner).Id;
            var session=new BattleSession("nuclear-offline",false);
            typeof(BattleSession).GetField("_battleFlowController",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,flow);
            typeof(BattleSession).GetField("_cardDefinitionProvider",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,provider);
            var actor=owner==PlayerId.Player?PlayerIdDto.Player:PlayerIdDto.AI;
            for(var i=0;i<2;i++)
            {
                state.GetPlayer(owner).Hand.Add("Nuclear");
                state.GetPlayer(owner).Resources.Add(new ResourceSet(0,0,10,0));
                session.ApplyCommand(new ClientBattleCommandMessage {
                    ActorId=actor,CommandType=OnlineBattleCommandType.CastScriptedSpell,CardId="Nuclear"
                });
            }
            var target=new UnitState("fallout-target","Goblin",enemy,new TileCoord(0,0),AttackType.Melee,1,100,true,false,0);
            state.GetBoard(enemy).Place(target.Position,target);
            session.ApplyCommand(new ClientBattleCommandMessage {ActorId=actor,CommandType=OnlineBattleCommandType.EndTurn});
            if(target.CurrentHp!=80||target.FalloutStacks!=2)throw new Exception("Server Fallout turn-start damage failed.");
            var logs=(List<LogEntry>)typeof(BattleSession).GetField("_combatLogRecords",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
            if(!logs.Any(x=>x.ValueCause==BattleValueChangeCause.Fallout&&x.Amount==20&&x.TargetOwnerId==enemy))
                throw new Exception("Server Fallout damage log missing.");
            // Preserve accumulated exposure after control changes, plus future summon exposure.
            state.GetBoard(enemy).Remove(target.Position);target.TransferOwnership(owner);
            state.GetBoard(owner).Place(target.Position,target);target.ApplyErasure();
            var capture=typeof(BattleSession).GetMethod("CreateDomainSnapshot",BindingFlags.NonPublic|BindingFlags.Instance)!;
            var restore=typeof(BattleSession).GetMethod("RestoreBattleState",BindingFlags.NonPublic|BindingFlags.Static)!;
            var snapshot=(BattleSessionDomainSnapshot)capture.Invoke(session,new object[]{state})!;
            var json=JsonSerializer.Serialize(snapshot);
            BattleState Restore(string value)=>(BattleState)restore.Invoke(null,new object[]{
                JsonSerializer.Deserialize<BattleSessionDomainSnapshot>(value)!,provider})!;
            var restored=Restore(json);
            var restoredTarget=restored.GetBoard(owner).GetOccupant(new TileCoord(0,0))!;
            if(restored.GetBoard(enemy).FalloutStacks!=2||restoredTarget.FalloutStacks!=2||restoredTarget.CurrentHp!=80)
                throw new Exception("Full JSON domain restoration lost Fallout.");
            var future=new UnitState("future","Goblin",enemy,new TileCoord(0,0),AttackType.Melee,1,100,true,false,0);
            restored.GetBoard(enemy).Place(future.Position,future);
            if(future.FalloutStacks!=2)throw new Exception("Restored board lost future summon exposure.");
            // Old snapshots have no Fallout fields and must default to zero.
            var old=JsonNode.Parse(json)!;
            foreach(var boardName in new[]{"PlayerBoard","AIBoard"})
            {
                var board=old[boardName]!.AsObject();board.Remove("FalloutStacks");
                foreach(var occupant in board["Occupants"]!.AsArray())
                {
                    occupant!.AsObject().Remove("FalloutPlayerStacks");
                    occupant.AsObject().Remove("FalloutAiStacks");
                }
            }
            var legacy=Restore(old.ToJsonString());
            if(legacy.PlayerBoard.FalloutStacks!=0||legacy.AIBoard.FalloutStacks!=0||
                legacy.GetBoard(owner).GetOccupant(new TileCoord(0,0))!.FalloutStacks!=0)
                throw new Exception("Legacy snapshot defaults failed.");
        }
        Console.WriteLine("PASS Nuclear: both seats, server cast/end-turn, Fallout combat log, full JSON restore, transferred/future summons and legacy snapshots.");
    }
}
