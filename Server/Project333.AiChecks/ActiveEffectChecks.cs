using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.PvpServer.Messages;
using Project333.Runtime.Infrastructure.Data;

internal static class ActiveEffectChecks
{
    public static void Run()
    {
        var provider=PrototypeCardDefinitions.LoadCardDefinitionProvider();
        var capture=typeof(BattleSession).GetMethod("CreateOccupantSnapshot",BindingFlags.NonPublic|BindingFlags.Static)!;
        var restore=typeof(BattleSession).GetMethod("RestoreOccupant",BindingFlags.NonPublic|BindingFlags.Static)!;
        foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var flow=PrototypeCardDefinitions.CreateBattleFlowController(provider);
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,owner));
            state.SetPhase(PhaseType.Main);
            flow.RestoreBattleState(state);
            var plant=new BuildingState("active-server","PowerPlant",owner,new TileCoord(0,0),false,0,30);
            state.GetBoard(owner).Place(plant.Position,plant);
            var session=new BattleSession("active-offline",false);
            typeof(BattleSession).GetField("_battleFlowController",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,flow);
            typeof(BattleSession).GetField("_cardDefinitionProvider",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,provider);
            var command=new ClientBattleCommandMessage {
                ActorId=owner==PlayerId.Player?PlayerIdDto.Player:PlayerIdDto.AI,
                CommandType=OnlineBattleCommandType.ActivateEffect,
                SourceCoord=new TileCoordDto {Column=0,Row=0},OccupantRuntimeId=plant.RuntimeId
            };
            var resources=state.GetPlayer(owner).Resources;
            resources.Spend(resources.Clone());
            if(!ActiveEffectService.CanOfferActivation(state,owner,plant.Position) || ActiveEffectService.CanActivate(state,owner,plant.Position))
                throw new Exception("No-gold active must be visible but unavailable to AI.");
            try {session.ApplyCommand(command);throw new Exception("No-gold active was accepted.");}
            catch(InvalidOperationException ex) { if(ex.Message!="골드가 부족합니다") throw; }
            if(resources.Gold!=0 || resources.Power!=0 || plant.LastActiveEffectTurnNumber!=-1)
                throw new Exception("No-gold rejection changed resources or usage.");
            resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(0,0,0,3));
            var before=state.GetPlayer(owner).Resources.Gold;
            session.ApplyCommand(command);
            if(state.GetPlayer(owner).Resources.Gold!=before-1||state.GetPlayer(owner).Resources.Power!=2)
                throw new Exception("Server active must atomically spend 1 gold and grant 2 power.");
            try {session.ApplyCommand(command);throw new Exception("Duplicate active was accepted.");}
            catch(InvalidOperationException){}
            if(state.GetPlayer(owner).Resources.Gold!=before-1||state.GetPlayer(owner).Resources.Power!=2)
                throw new Exception("Rejected duplicate changed resources.");
            var snapshot=(BattleSessionOccupantSnapshot)capture.Invoke(null,new object[]{plant})!;
            var saved=JsonSerializer.Deserialize<BattleSessionOccupantSnapshot>(JsonSerializer.Serialize(snapshot))!;
            var restored=(BuildingState)restore.Invoke(null,new object[]{saved,owner,provider})!;
            if(restored.LastActiveEffectTurnNumber!=state.TurnNumber)throw new Exception("Snapshot lost active usage.");
            state.GetBoard(owner).Remove(plant.Position);
            state.GetBoard(owner).Place(restored.Position,restored);
            if(ActiveEffectService.CanActivate(state,owner,restored.Position))throw new Exception("Restore refreshed active.");
            state.RestoreRuntimeState(state.TurnNumber+2,owner,PhaseType.Main);
            session.ApplyCommand(command);
            if(state.GetPlayer(owner).Resources.Power!=4)throw new Exception("Next owner turn did not refresh active.");
        }
        Console.WriteLine("PASS Active effects: server command dispatch, both seats, duplicate rejection, JSON persistence and next-turn refresh.");
    }
}
