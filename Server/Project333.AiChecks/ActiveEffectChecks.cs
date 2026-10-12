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
        var provider = new InMemoryCardDefinitionProvider(new CardDefinition[] {
            new BuildingCardDefinition("PowerPlant", "발전소", new Project333.Runtime.Domain.Resources.ResourceSet(0,0,1,0), false, 0, 30)
        });
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
            var logs = (List<Project333.Runtime.Application.Online.BattleCombatLogEntryDto>)typeof(BattleSession)
                .GetField("_combatLogRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            var resources=state.GetPlayer(owner).Resources;
            resources.Spend(resources.Clone());
            if(!ActiveEffectService.CanOfferActivation(state,owner,plant.Position) || ActiveEffectService.CanActivate(state,owner,plant.Position))
                throw new Exception("No-gold active must be visible but unavailable to AI.");
            try {session.ApplyCommand(command);throw new Exception("No-gold active was accepted.");}
            catch(InvalidOperationException ex) { if(ex.Message!="자원이 부족합니다.") throw; }
            if(resources.Gold!=0 || resources.Power!=0 || plant.LastActiveEffectTurnNumber!=-1)
                throw new Exception("No-gold rejection changed resources or usage.");
            if (logs.Count != 0) throw new Exception("Rejected no-gold active wrote a combat log.");
            resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(0,0,0,3));
            var before=state.GetPlayer(owner).Resources.Gold;
            session.ApplyCommand(command);
            if(state.GetPlayer(owner).Resources.Gold!=before-1||state.GetPlayer(owner).Resources.Power!=2)
                throw new Exception("Server active must atomically spend 1 gold and grant 2 power.");
            var activeLog = logs.Single(entry => entry.EntryType == Project333.Runtime.Application.Online.BattleCombatLogEntryType.ActiveEffectActivated);
            if (activeLog.SourceOwnerId != owner || activeLog.SourceCardId != "PowerPlant" ||
                activeLog.SourceCoord?.Column != 0 || activeLog.SourceCoord.Row != 0)
                throw new Exception("Active log lost source identity, ownership or coordinates.");
            if (logs.Count != 3 || logs[1].ResourceType != Project333.Runtime.Application.Online.BattleCombatLogResourceType.Gold ||
                logs[1].Amount != 1 || logs[2].ResourceType != Project333.Runtime.Application.Online.BattleCombatLogResourceType.Power || logs[2].Amount != 2)
                throw new Exception("Active spend/gain logs were lost on the eventless command path.");
            var persisted = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(
                JsonSerializer.Serialize(session.CreatePersistenceSnapshot("active-log-check")))!;
            if (persisted.CombatLogEntries!.Count != 3 || persisted.CombatLogEntries[0].EntryType != activeLog.EntryType)
                throw new Exception("Persistence snapshot lost active combat history.");
            try {session.ApplyCommand(command);throw new Exception("Duplicate active was accepted.");}
            catch(InvalidOperationException){}
            if(state.GetPlayer(owner).Resources.Gold!=before-1||state.GetPlayer(owner).Resources.Power!=2)
                throw new Exception("Rejected duplicate changed resources.");
            if (logs.Count != 3) throw new Exception("Rejected duplicate wrote combat history.");
            var snapshot=(BattleSessionOccupantSnapshot)capture.Invoke(null,new object[]{plant})!;
            var saved=JsonSerializer.Deserialize<BattleSessionOccupantSnapshot>(JsonSerializer.Serialize(snapshot))!;
            var restored=(BuildingState)restore.Invoke(null,new object[]{saved,owner,provider})!;
            if(restored.LastActiveEffectTurnNumber!=state.TurnNumber)throw new Exception("Snapshot lost active usage.");
            state.GetBoard(owner).Remove(plant.Position);
            state.GetBoard(owner).Place(restored.Position,restored);
            if(ActiveEffectService.CanActivate(state,owner,restored.Position))throw new Exception("Restore refreshed active.");
            state.RestoreRuntimeState(state.TurnNumber+2,owner,PhaseType.Main);
            // The AI dispatch path must emit the same successful activation record.
            typeof(BattleSession).GetMethod("AppendRuntimeCommandEvents", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(session, new object[] { new List<BattleEventDto>(), owner,
                    new Project333.Runtime.Application.Commands.ActivateEffectCommand(restored.Position, restored.RuntimeId) });
            if (logs.Count(entry => entry.EntryType == Project333.Runtime.Application.Online.BattleCombatLogEntryType.ActiveEffectActivated) != 2)
                throw new Exception("AI dispatch did not log successful activation exactly once.");
            if(state.GetPlayer(owner).Resources.Power!=4)throw new Exception("Next owner turn did not refresh active.");
        }
        Console.WriteLine("PASS Active effects: server command dispatch, both seats, duplicate rejection, JSON persistence and next-turn refresh.");
    }
}
