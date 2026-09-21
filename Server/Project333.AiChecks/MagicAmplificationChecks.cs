using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

internal static class MagicAmplificationChecks
{
    public static void Run()
    {
        var provider=PrototypeCardDefinitions.LoadCardDefinitionProvider();
        const string cardId=MagicAmplificationCircleRules.CardId;
        foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,owner));
            state.SetPhase(PhaseType.Main);
            var flow=PrototypeCardDefinitions.CreateBattleFlowController(provider);flow.RestoreBattleState(state);
            var session=new BattleSession("amplification-offline",false);
            typeof(BattleSession).GetField("_battleFlowController",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,flow);
            typeof(BattleSession).GetField("_cardDefinitionProvider",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,provider);
            var actor=owner==PlayerId.Player?PlayerIdDto.Player:PlayerIdDto.AI;
            for(var i=0;i<2;i++)
            {
                state.GetPlayer(owner).Hand.Add(cardId);
                state.GetPlayer(owner).Resources.Add(new ResourceSet(1,0,0,0));
                session.ApplyCommand(new ClientBattleCommandMessage {ActorId=actor,CommandType=OnlineBattleCommandType.CastScriptedSpell,CardId=cardId});
            }
            if(SpellPowerRules.GetTotal(state,owner)!=20)throw new Exception("Server cast did not stack SpellPower.");
            var capture=typeof(BattleSession).GetMethod("CreateDomainSnapshot",BindingFlags.NonPublic|BindingFlags.Instance)!;
            var restore=typeof(BattleSession).GetMethod("RestoreBattleState",BindingFlags.NonPublic|BindingFlags.Static)!;
            BattleState RoundTrip()
            {
                var snapshot=(BattleSessionDomainSnapshot)capture.Invoke(session,new object[]{state})!;
                var saved=JsonSerializer.Deserialize<BattleSessionDomainSnapshot>(JsonSerializer.Serialize(snapshot))!;
                return (BattleState)restore.Invoke(null,new object[]{saved,provider})!;
            }
            state=RoundTrip();flow.RestoreBattleState(state);
            if(SpellPowerRules.GetTotal(state,owner)!=20)throw new Exception("Reconnect lost SpellPower.");
            var enemy=state.GetOpponent(owner).Id;
            var target=new UnitState("target","Goblin",enemy,new TileCoord(0,0),AttackType.Melee,1,100,true,false,0);
            state.GetBoard(enemy).Place(target.Position,target);
            state.GetPlayer(owner).Hand.Add("firebolt");state.GetPlayer(owner).Resources.Add(new ResourceSet(1,0,0,0));
            session.ApplyCommand(new ClientBattleCommandMessage {
                ActorId=actor,CommandType=OnlineBattleCommandType.CastDamageSpell,CardId="firebolt",
                TargetOwnerId=enemy==PlayerId.Player?PlayerIdDto.Player:PlayerIdDto.AI,
                TargetCoord=new TileCoordDto{Column=0,Row=0},HasTarget=true
            });
            if(target.CurrentHp!=70)throw new Exception("Restored SpellPower did not amplify server damage.");
            session.ApplyCommand(new ClientBattleCommandMessage{ActorId=actor,CommandType=OnlineBattleCommandType.EndTurn});
            if(SpellPowerRules.GetTotal(state,owner)!=0||SpellPowerRules.GetTotal(RoundTrip(),owner)!=0)
                throw new Exception("Expired SpellPower resurrected on reconnect.");
        }
        Console.WriteLine("PASS MagicAmplification: both seats, cast/stack, mid-turn JSON restore, amplified server spell and expiry after end-turn/reconnect.");
    }
}
