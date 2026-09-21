using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Project333.PvpServer.BattleSessions;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

internal static class CombatStatBuffChecks
{
    public static void Run()
    {
        var provider=PrototypeCardDefinitions.LoadCardDefinitionProvider();
        foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
        {
            var deck=Enumerable.Repeat("Goblin",33).ToArray();
            var state=new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck,deck,owner));
            state.SetPhase(PhaseType.Main);
            var target=new UnitState("buff-target","Hero",owner,new TileCoord(0,0),AttackType.Melee,39,42,true,false,0);
            state.GetBoard(owner).Place(target.Position,target);
            target.ApplyCombatStatBuff("Hero",attackBonus:33,maxHpBonus:33);
            target.ApplyCombatStatBuff("RobotFusion",attackBonus:4,maxHpBonus:7);
            var session=new BattleSession("stat-buff-offline",false);
            var capture=typeof(BattleSession).GetMethod("CreateDomainSnapshot",BindingFlags.NonPublic|BindingFlags.Instance)!;
            var restore=typeof(BattleSession).GetMethod("RestoreBattleState",BindingFlags.NonPublic|BindingFlags.Static)!;
            var json=JsonSerializer.Serialize((BattleSessionDomainSnapshot)capture.Invoke(session,new object[]{state})!);
            BattleState Restore(string text)=>(BattleState)restore.Invoke(null,new object[]{
                JsonSerializer.Deserialize<BattleSessionDomainSnapshot>(text)!,provider})!;
            var restored=Restore(json);
            var unit=restored.GetBoard(owner).GetOccupant(target.Position)!;
            if(unit.BaseAttack!=76||unit.MaxHp!=82||unit.OriginalAttack!=39||unit.CombatStatBuffs.Count!=2)
                throw new Exception("Stat buff JSON restore lost stats, provenance or level baseline.");
            if(unit.CombatStatBuffs.Single(b=>b.SourceCardId=="RobotFusion").MaxHpBonus!=7)
                throw new Exception("Stat buff source split was lost.");
            var key=AiBattleStateCopy.PositionKey(restored);
            var clone=AiBattleStateCopy.Create(restored);
            if(AiBattleStateCopy.PositionKey(clone)!=key)throw new Exception("AI clone state key mismatch.");
            clone.GetBoard(owner).GetOccupant(target.Position)!.ApplyErasure();
            if(unit.CombatStatBuffs.Count!=2)throw new Exception("AI clone mutated original source records.");
            var legacy=JsonNode.Parse(json)!;
            foreach(var name in new[]{"PlayerBoard","AIBoard"})
                foreach(var occupant in legacy[name]!["Occupants"]!.AsArray())
                    occupant!.AsObject().Remove("CombatStatBuffs");
            var old=Restore(legacy.ToJsonString()).GetBoard(owner).GetOccupant(target.Position)!;
            if(old.CombatStatBuffs.Count!=0||old.BaseAttack!=76||old.MaxHp!=82)
                throw new Exception("Legacy snapshot must preserve stats without inventing sources.");
            new ErasureService().Apply(restored,unit);
            if(unit.CombatStatBuffs.Count!=0||unit.BaseAttack!=39||unit.MaxHp!=42)
                throw new Exception("Erasure after reconnect did not reset attribution to upgraded baseline.");
        }
        Console.WriteLine("PASS combat stat buffs: both seats, full JSON provenance restore, legacy data, AI copy isolation, post-reconnect Erasure.");
    }
}
