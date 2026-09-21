using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using LogEntry = Project333.Runtime.Application.Online.BattleCombatLogEntryDto;
using LogType = Project333.Runtime.Application.Online.BattleCombatLogEntryType;

internal static class HeroKillChecks
{
    public static void Run()
    {
        var provider=PrototypeCardDefinitions.LoadCardDefinitionProvider();
        foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var flow=PrototypeCardDefinitions.CreateBattleFlowController(provider);
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,owner));
            state.SetPhase(PhaseType.Main);
            flow.RestoreBattleState(state);
            var hero=new UnitState("hero","Hero",owner,new TileCoord(0,0),AttackType.Melee,33,33,true,false,0,
                damageType:DamageType.Fixed,hasRush:true);
            hero.HasSummoningSickness=false;
            state.GetBoard(owner).Place(hero.Position,hero);
            var enemyOwner=state.GetOpponent(owner).Id;
            var victim=new UnitState("victim","Goblin",enemyOwner,new TileCoord(0,0),AttackType.Melee,0,10,true,false,0);
            state.GetBoard(enemyOwner).Place(victim.Position,victim);
            var session=new BattleSession("hero-offline",false);
            typeof(BattleSession).GetField("_battleFlowController",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,flow);
            typeof(BattleSession).GetField("_cardDefinitionProvider",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,provider);
            session.ApplyCommand(new ClientBattleCommandMessage {
                ActorId=owner==PlayerId.Player?PlayerIdDto.Player:PlayerIdDto.AI,
                CommandType=OnlineBattleCommandType.Attack,
                SourceCoord=new TileCoordDto{Column=0,Row=0},TargetCoord=new TileCoordDto{Column=0,Row=0}
            });
            if(!((hero.BaseAttack==66&&hero.MaxHp==33)||(hero.BaseAttack==33&&hero.MaxHp==66)))
                throw new Exception("Hero server kill bonus is not exactly one +33 stat.");
            var logs=(List<LogEntry>)typeof(BattleSession).GetField("_combatLogRecords",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
            var bonus=logs.Single(x=>x.EntryType==LogType.HeroGrowth);
            if(bonus.TargetOwnerId!=owner || bonus.AttackBonus+bonus.HpBonus!=33)
                throw new Exception("Hero attack combat log is missing or wrong.");
            var capture=typeof(BattleSession).GetMethod("CreateOccupantSnapshot",BindingFlags.NonPublic|BindingFlags.Static)!;
            var restore=typeof(BattleSession).GetMethod("RestoreOccupant",BindingFlags.NonPublic|BindingFlags.Static)!;
            var snapshot=(BattleSessionOccupantSnapshot)capture.Invoke(null,new object[]{hero})!;
            var saved=JsonSerializer.Deserialize<BattleSessionOccupantSnapshot>(JsonSerializer.Serialize(snapshot))!;
            var restored=(UnitState)restore.Invoke(null,new object[]{saved,owner,provider})!;
            if(restored.BaseAttack!=hero.BaseAttack || restored.MaxHp!=hero.MaxHp || restored.CurrentHp!=hero.CurrentHp ||
                !restored.HasRush || restored.InvincibleEffects.Count!=0)
                throw new Exception("Hero snapshot did not preserve kill stats and Rush.");
        }
        Console.WriteLine("PASS Hero: server attack command, both seats, kill-bonus combat log and JSON restore.");
    }
}
