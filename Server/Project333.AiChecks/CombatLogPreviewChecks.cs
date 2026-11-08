using System.Reflection;
using System.Text.Json;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Messages;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using ClientCommand=Project333.PvpServer.Messages.ClientBattleCommandMessage;
using CommandType=Project333.PvpServer.Messages.OnlineBattleCommandType;
using TileCoordDto=Project333.PvpServer.Messages.TileCoordDto;

// Entirely offline: fake sockets, in-memory sessions, no listener/database/account operations.
internal static class CombatLogPreviewChecks
{
    public static void Run()
    {
        var cases=0;
        foreach(var actor in new[]{PlayerId.Player,PlayerId.AI})
        foreach(var lethal in new[]{false,true})
        {
            using var socketA=new DeckCheckSocket();using var socketB=new DeckCheckSocket();
            var a=new BattleClientConnection("history-a",socketA){PlayerToken="a"};
            var b=new BattleClientConnection("history-b",socketB){PlayerToken="b"};
            a.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin",33));b.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin",33));
            var session=new BattleSession("history-"+actor+lethal,false);
            Require(session.TryAddConnection(a,out _)&&session.TryAddConnection(b,out _),"Seats");
            Require(session.TryStartBattleIfReady(out _),"Start");
            var state=State(session);state.RestoreRuntimeState(4,actor,PhaseType.Main);
            var source=state.GetPlayer(actor).Master;var target=state.GetOpponent(actor).Master;
            source.CurrentHp=lethal?3:323;target.IncreaseBaseAttack(30);source.RemainingAttacksThisTurn=1;
            session.ApplyCommand(new ClientCommand{ActorId=actor==PlayerId.Player?PlayerIdDto.Player:PlayerIdDto.AI,
                CommandType=CommandType.Attack,SourceCoord=new TileCoordDto{Column=source.Position.Column,Row=source.Position.Row},TargetCoord=new TileCoordDto{Column=target.Position.Column,Row=target.Position.Row}});
            var record=session.CreatePersistenceSnapshot("history-check").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.Attack);
            var sourcePreview=record.SourcePreview??throw new Exception("Missing source preview");
            var targetPreview=record.TargetPreview??throw new Exception("Missing target preview");
            Require(sourcePreview.Attack==3 && sourcePreview.Hp==(lethal?3:323),"Pre-attack source snapshot");
            Require(targetPreview.Attack==33 && targetPreview.Hp==333,"Pre-attack target snapshot");
            Require(sourcePreview.DamageTaken==(lethal?3:33) && targetPreview.DamageTaken==3,"Actual HP damage (overkill is clamped by the existing damage rules)");
            Require(sourcePreview.Died==lethal && !targetPreview.Died,"Lethal marker");
            foreach(var connection in new[]{a,b})
            {
                var view=session.CreateStateViewEnvelope(connection).StateView!;
                var projected=view.CombatLogEntries.Last(e=>e.EntryType==BattleCombatLogEntryType.Attack);
                Require(projected.SourcePreview?.Hp==(lethal?3:323) && projected.TargetPreview?.Attack==33,"Viewer snapshot");
                Require(view.Opponent.VisibleHandCards.Count==0 && view.Opponent.VisibleHandCardIds.Count==0,"Hidden hand privacy");
                projected.SourcePreview!.Hp=999;
                Require(sourcePreview.Hp==(lethal?3:323),"Projection must not mutate history");
            }
            var saved=JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("history-json")))!;
            var restored=new BattleSession(session.MatchId,false);
            Require(restored.RestoreFromPersistenceSnapshot(saved),"Restore session");
            var recovered=restored.CreatePersistenceSnapshot("history-recovered").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.Attack);
            Require(recovered.SourcePreview?.Hp==(lethal?3:323) && recovered.TargetPreview?.DamageTaken==3,"Reconnect history");
            cases++;
        }
        RunSingleCardPreviewChecks();
        RunDirectedPreviewChecks();
        RunResourceDrawGenerationChecks();
        Console.WriteLine($"PASS combat-log previews: {cases} attack cases + 17 single-card preview types + directed spell/damage/healing/trap/mark previews + resource/draw/generation previews with private card backs, both viewer seats, death/damage/heal, private hands and full persistence restore.");
    }

    private static void RunResourceDrawGenerationChecks()
    {
        using var socketA=new DeckCheckSocket();using var socketB=new DeckCheckSocket();
        var a=new BattleClientConnection("rdg-a",socketA){PlayerToken="a"};
        var b=new BattleClientConnection("rdg-b",socketB){PlayerToken="b"};
        a.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin",33));b.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin",33));
        a.CardUpgradeLevels["Goblin"]=5;
        var session=new BattleSession("resource-draw-generation",false);
        Require(session.TryAddConnection(a,out _)&&session.TryAddConnection(b,out _),"RDG seats");
        Require(session.TryStartBattleIfReady(out _),"RDG start");
        var state=State(session);state.RestoreRuntimeState(4,PlayerId.Player,PhaseType.Main);
        state.Player.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(50,50,50,50));
        state.AI.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(50,50,50,50));
        var playerViewer=a.AssignedSeatId==PlayerIdDto.Player?a:b;var aiViewer=ReferenceEquals(playerViewer,a)?b:a;

        // #17 ResourceGained: ManaStone shows a stat-less single spell card.
        state.Player.Hand.Add("ManaStone");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.CastScriptedSpell,CardId="ManaStone"});
        var gained=session.CreatePersistenceSnapshot("rdg-gain").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.ResourceGained&&e.SourceCardId=="ManaStone");
        Require(gained.TryGetSingleCardPreview(out var gainedPreview,out var gainedOwner)&&gainedOwner==PlayerId.Player&&gainedPreview.CardId=="ManaStone"&&!gainedPreview.HasCombatStats,"#17 ResourceGained single source preview");

        // #19 CardDrawn: JarOfGreed owner sees real cards, the opponent sees card backs only.
        state.Player.Hand.Add("JarOfGreed");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.CastScriptedSpell,CardId="JarOfGreed"});
        var drawn=session.CreatePersistenceSnapshot("rdg-draw").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.CardDrawn&&e.SourceCardId=="JarOfGreed");
        Require(drawn.TryGetDirectedPreview(out var drawSource,out var drawTargets)&&drawSource.CardId=="JarOfGreed"&&!drawSource.HasCombatStats&&
                drawTargets.Count==2&&drawTargets.All(t=>t.CardId=="Goblin"&&t.HasCombatStats),"#19 canonical drawn cards");
        var ownerDraw=session.CreateStateViewEnvelope(playerViewer).StateView!.CombatLogEntries.Last(e=>e.EntryType==BattleCombatLogEntryType.CardDrawn&&e.SourceCardId=="JarOfGreed");
        Require(ownerDraw.TargetPreviews.Count==2&&ownerDraw.TargetPreviews.All(t=>t.CardId=="Goblin"),"#19 owner sees drawn cards");
        var otherDraw=session.CreateStateViewEnvelope(aiViewer).StateView!.CombatLogEntries.Last(e=>e.EntryType==BattleCombatLogEntryType.CardDrawn&&e.SourceCardId=="JarOfGreed");
        Require(otherDraw.TargetPreviews.Count==2&&otherDraw.TargetPreviews.All(t=>t.CardId==BattleCombatLogCardSnapshotDto.CardBackCardId&&!t.HasCombatStats)&&
                otherDraw.TargetPreview?.CardId==BattleCombatLogCardSnapshotDto.CardBackCardId&&!JsonSerializer.Serialize(otherDraw).Contains("\"Goblin\""),"#19 opponent sees only card backs");

        // #20 CardGenerated: RobotFactory owner sees the robot, the opponent sees a card back without TargetCardId.
        var factoryCoord=new Project333.Runtime.Domain.Board.TileCoord(4,1);
        state.PlayerBoard.Place(factoryCoord,new BuildingState("factory-rdg","RobotFactory",PlayerId.Player,factoryCoord,false,0,50));
        state.RestoreRuntimeState(5,PlayerId.AI,PhaseType.Main);
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.AI,CommandType=CommandType.EndTurn});
        var generated=session.CreatePersistenceSnapshot("rdg-gen").CombatLogEntries!.LastOrDefault(e=>e.EntryType==BattleCombatLogEntryType.CardGenerated);
        Require(generated!=null&&generated.TryGetDirectedPreview(out var genSource,out var genTargets)&&genSource.CardId=="RobotFactory"&&
                genTargets.Count==1&&genTargets[0].CardId==generated.TargetCardId&&genTargets[0].HasCombatStats,"#20 canonical generated robot");
        var ownerGen=session.CreateStateViewEnvelope(playerViewer).StateView!.CombatLogEntries.Last(e=>e.EntryType==BattleCombatLogEntryType.CardGenerated);
        Require(ownerGen.TargetPreviews.Single().CardId==generated!.TargetCardId,"#20 owner sees generated robot");
        var otherGen=session.CreateStateViewEnvelope(aiViewer).StateView!.CombatLogEntries.Last(e=>e.EntryType==BattleCombatLogEntryType.CardGenerated);
        Require(otherGen.TargetCardId==""&&otherGen.TargetPreviews.Single().CardId==BattleCombatLogCardSnapshotDto.CardBackCardId&&
                otherGen.TargetPreview?.CardId==BattleCombatLogCardSnapshotDto.CardBackCardId,"#20 opponent sees card back");

        // #17/#18: every resource entry recorded in this session carries a source preview.
        var resources=session.CreatePersistenceSnapshot("rdg-resources").CombatLogEntries!
            .Where(e=>e.EntryType==BattleCombatLogEntryType.ResourceGained||e.EntryType==BattleCombatLogEntryType.ResourceSpent).ToList();
        Require(resources.Count>0&&resources.Where(e=>!string.IsNullOrWhiteSpace(e.SourceCardId)).All(e=>e.HasHoldPreview),"#17/#18 resource source previews");

        var saved=JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("rdg-json")))!;
        var restored=new BattleSession(session.MatchId,false);
        Require(restored.RestoreFromPersistenceSnapshot(saved),"RDG restore");
        var restoredDraw=restored.CreatePersistenceSnapshot("rdg-restored").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.CardDrawn&&e.SourceCardId=="JarOfGreed");
        Require(restoredDraw.TargetPreviews.Count==2,"RDG restored drawn previews");
    }

    private static void RunSingleCardPreviewChecks()
    {
        using var socketA=new DeckCheckSocket();using var socketB=new DeckCheckSocket();
        var a=new BattleClientConnection("single-a",socketA){PlayerToken="a"};
        var b=new BattleClientConnection("single-b",socketB){PlayerToken="b"};
        a.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin",33));b.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin",33));
        a.CardUpgradeLevels["Goblin"]=1;
        var session=new BattleSession("single-previews",false);
        Require(session.TryAddConnection(a,out _)&&session.TryAddConnection(b,out _),"Single preview seats");
        Require(session.TryStartBattleIfReady(out _),"Single preview start");
        var state=State(session);state.RestoreRuntimeState(4,PlayerId.Player,PhaseType.Main);
        state.Player.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(20,20,20,20));
        state.AI.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(20,20,20,20));

        // Place an OrcChieftain on Player board first so any newly summoned Orc gets +10/+10 on board.
        state.PlayerBoard.Place(new Project333.Runtime.Domain.Board.TileCoord(4,0),new UnitState("chieftain-1","OrcChieftain",PlayerId.Player,new Project333.Runtime.Domain.Board.TileCoord(4,0),AttackType.Melee,3,5,true,false,0,new Project333.Runtime.Domain.Resources.ResourceSet(),races:new[]{UnitRace.Orc}));
        state.Player.Hand.Add("OrcWarrior");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.PlayUnitCard,CardId="OrcWarrior",TargetCoord=new TileCoordDto{Column=0,Row=0}});
        var entries=session.CreatePersistenceSnapshot("single-summon").CombatLogEntries!;
        var unitSummoned=entries.Last(e=>e.EntryType==BattleCombatLogEntryType.UnitSummoned);
        var orcOnBoard=state.PlayerBoard.GetOccupant(new Project333.Runtime.Domain.Board.TileCoord(0,0))!;
        // Hand stats (OriginalAttack/OriginalMaxHp) are unaffected by OrcChieftain's board aura.
        Require(unitSummoned.TryGetSingleCardPreview(out var unitPreview,out var unitOwner) && unitOwner==PlayerId.Player,"UnitSummoned preview");
        Require(unitPreview.Attack==orcOnBoard.OriginalAttack && unitPreview.Hp==orcOnBoard.OriginalMaxHp &&
                unitPreview.Attack==unitPreview.OriginalAttack && unitPreview.Hp==unitPreview.OriginalMaxHp &&
                orcOnBoard.Attack==orcOnBoard.OriginalAttack+OrcChieftainRules.AuraAttackBonus,"UnitSummoned stores hand stats before board aura");

        // BuildingConstructed (#3) + ActiveEffectActivated (#10)
        state.Player.Hand.Add("PowerPlant");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.PlayBuildingCard,CardId="PowerPlant",TargetCoord=new TileCoordDto{Column=1,Row=1}});
        var buildingConstructed=session.CreatePersistenceSnapshot("single-building").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.BuildingConstructed);
        var plant=state.PlayerBoard.GetOccupant(new Project333.Runtime.Domain.Board.TileCoord(1,1))!;
        Require(buildingConstructed.TryGetSingleCardPreview(out var buildingPreview,out _) &&
                buildingPreview.Attack==plant.OriginalAttack && buildingPreview.Hp==plant.OriginalMaxHp,"BuildingConstructed hand stats");

        plant.ApplyCombatStatBuff("TestBuff",attackBonus:2,maxHpBonus:3);
        plant.CurrentHp-=1;
        var expectedActiveHp=plant.CurrentHp;
        var expectedActiveAtk=plant.Attack;
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.ActivateEffect,OccupantRuntimeId=plant.RuntimeId,SourceCoord=new TileCoordDto{Column=1,Row=1}});
        var activeRecord=session.CreatePersistenceSnapshot("single-active").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.ActiveEffectActivated);
        Require(activeRecord.TryGetSingleCardPreview(out var activePreview,out _) &&
                activePreview.Attack==expectedActiveAtk && activePreview.Hp==expectedActiveHp &&
                activePreview.OriginalAttack==plant.OriginalAttack && activePreview.OriginalMaxHp==plant.OriginalMaxHp,"ActiveEffectActivated before-stats");

        // Status changes (#21~#34) on existing occupant (with GaebangChief gold bonus check for after-state)
        var chief=new UnitState("chief-1","GaebangChief",PlayerId.Player,new Project333.Runtime.Domain.Board.TileCoord(0,1),AttackType.Melee,10,60,true,true,1,new Project333.Runtime.Domain.Resources.ResourceSet(),hasHiding:true);
        state.PlayerBoard.Place(new Project333.Runtime.Domain.Board.TileCoord(0,1),chief);
        var captureMethod=typeof(BattleSession).GetMethod("CaptureBattleSnapshot",BindingFlags.NonPublic|BindingFlags.Static)!;
        var statusMethod=typeof(BattleSession).GetMethod("AppendStatusChangeCombatLogs",BindingFlags.NonPublic|BindingFlags.Instance)!;

        // With gold on Player, GaebangChief has +10 attack per gold while active, and 10 attack when suppressed (Drained/Erasure/Sealbound).
        var snap0=captureMethod.Invoke(null,new object?[]{state})!;
        chief.IsDrained=true;
        var snap1=captureMethod.Invoke(null,new object?[]{state})!;
        statusMethod.Invoke(session,new[]{snap0,snap1});
        var drainedApplied=session.CreatePersistenceSnapshot("s-21").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.DrainedApplied);
        var hidingRevealed=session.CreatePersistenceSnapshot("s-32").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.HidingRevealed);
        Require(drainedApplied.TryGetSingleCardPreview(out var p21,out _) && p21.Attack==10 && p21.OriginalAttack==10,"#21 DrainedApplied after-stats");
        Require(hidingRevealed.TryGetSingleCardPreview(out var p32,out _) && p32.CardId=="GaebangChief","#32 HidingRevealed after-stats");

        chief.RestoreSuppressionStates(isDrained:false,isErasure:false,isSealbound:false,sealboundOwnerTurnStartsRemaining:0,hidingRevealed:false);
        var snap2=captureMethod.Invoke(null,new object?[]{state})!;
        statusMethod.Invoke(session,new[]{snap1,snap2});
        var drainedCleared=session.CreatePersistenceSnapshot("s-22").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.DrainedCleared);
        var hidingApplied=session.CreatePersistenceSnapshot("s-31").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.HidingApplied);
        Require(drainedCleared.TryGetSingleCardPreview(out var p22,out _) && p22.Attack>p22.OriginalAttack,"#22 DrainedCleared restores gold bonus in after-stats");
        Require(hidingApplied.TryGetSingleCardPreview(out var p31,out _) && p31.CardId=="GaebangChief","#31 HidingApplied after-stats");

        chief.ApplyStun(1);chief.ApplyFreeze(1);chief.ApplyHuanShu();
        var snap3=captureMethod.Invoke(null,new object?[]{state})!;
        statusMethod.Invoke(session,new[]{snap2,snap3});
        Require(session.CreatePersistenceSnapshot("s-25").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.StunApplied).HasHoldPreview,"#25 StunApplied");
        Require(session.CreatePersistenceSnapshot("s-27").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.FreezeApplied).HasHoldPreview,"#27 FreezeApplied");
        Require(session.CreatePersistenceSnapshot("s-33").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.HuanShuApplied).HasHoldPreview,"#33 HuanShuApplied");

        chief.ClearActionDebuffs();chief.ClearHuanShu();
        var snap4=captureMethod.Invoke(null,new object?[]{state})!;
        statusMethod.Invoke(session,new[]{snap3,snap4});
        Require(session.CreatePersistenceSnapshot("s-26").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.StunCleared).HasHoldPreview,"#26 StunCleared");
        Require(session.CreatePersistenceSnapshot("s-28").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.FreezeCleared).HasHoldPreview,"#28 FreezeCleared");
        Require(session.CreatePersistenceSnapshot("s-34").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.HuanShuCleared).HasHoldPreview,"#34 HuanShuCleared");

        chief.ApplyErasure();
        var snap5=captureMethod.Invoke(null,new object?[]{state})!;
        statusMethod.Invoke(session,new[]{snap4,snap5});
        var erasureApplied=session.CreatePersistenceSnapshot("s-23").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.ErasureApplied);
        Require(erasureApplied.TryGetSingleCardPreview(out var p23,out _) && p23.Attack==10,"#23 ErasureApplied after-stats");

        chief.RestoreSuppressionStates(isDrained:false,isErasure:false,isSealbound:true,sealboundOwnerTurnStartsRemaining:1,hidingRevealed:true);
        var snap6=captureMethod.Invoke(null,new object?[]{state})!;
        statusMethod.Invoke(session,new[]{snap5,snap6});
        Require(session.CreatePersistenceSnapshot("s-24").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.ErasureCleared).HasHoldPreview,"#24 ErasureCleared");
        Require(session.CreatePersistenceSnapshot("s-29").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.SealboundApplied).HasHoldPreview,"#29 SealboundApplied");

        chief.ResolveSealboundOwnerTurnStart();
        var snap7=captureMethod.Invoke(null,new object?[]{state})!;
        statusMethod.Invoke(session,new[]{snap6,snap7});
        var sealReleased=session.CreatePersistenceSnapshot("s-30").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.SealboundReleased);
        Require(sealReleased.TryGetSingleCardPreview(out var p30,out _) && p30.Attack>p30.OriginalAttack,"#30 SealboundReleased after-stats");

        // Verify viewer projection and JSON persistence for all 17 single-card preview types
        var allSingleTypes=new[]{
            BattleCombatLogEntryType.UnitSummoned,BattleCombatLogEntryType.BuildingConstructed,BattleCombatLogEntryType.ActiveEffectActivated,
            BattleCombatLogEntryType.DrainedApplied,BattleCombatLogEntryType.DrainedCleared,BattleCombatLogEntryType.ErasureApplied,BattleCombatLogEntryType.ErasureCleared,
            BattleCombatLogEntryType.StunApplied,BattleCombatLogEntryType.StunCleared,BattleCombatLogEntryType.FreezeApplied,BattleCombatLogEntryType.FreezeCleared,
            BattleCombatLogEntryType.SealboundApplied,BattleCombatLogEntryType.SealboundReleased,BattleCombatLogEntryType.HidingApplied,BattleCombatLogEntryType.HidingRevealed,
            BattleCombatLogEntryType.HuanShuApplied,BattleCombatLogEntryType.HuanShuCleared
        };
        var saved=JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("single-json")))!;
        var restored=new BattleSession(session.MatchId,false);
        Require(restored.RestoreFromPersistenceSnapshot(saved),"Restore single preview session");
        var restoredEntries=restored.CreatePersistenceSnapshot("single-restored").CombatLogEntries!;
        foreach(var t in allSingleTypes)
        {
            var match=restoredEntries.LastOrDefault(e=>e.EntryType==t);
            Require(match!=null && match.HasHoldPreview,$"Missing or non-previewable entry for {t}");
        }
        foreach(var conn in new[]{a,b})
        {
            var view=session.CreateStateViewEnvelope(conn).StateView!;
            foreach(var t in allSingleTypes)
            {
                var projected=view.CombatLogEntries.Last(e=>e.EntryType==t);
                Require(projected.TryGetSingleCardPreview(out var p,out var owner) && owner==PlayerId.Player,"Projected single preview "+t);
                var originalHp=p.Hp;
                p.Hp=999;
                var canonical=session.CreatePersistenceSnapshot("check-"+t).CombatLogEntries!.Last(e=>e.EntryType==t);
                Require(canonical.TryGetSingleCardPreview(out var canonicalPreview,out _) && canonicalPreview.Hp==originalHp,"Projection must not mutate "+t);
            }
        }
    }

    private static void RunDirectedPreviewChecks()
    {
        using var socketA=new DeckCheckSocket();using var socketB=new DeckCheckSocket();
        var a=new BattleClientConnection("dir-a",socketA){PlayerToken="a"};
        var b=new BattleClientConnection("dir-b",socketB){PlayerToken="b"};
        a.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin",33));b.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin",33));
        a.CardUpgradeLevels["Goblin"]=5;
        var session=new BattleSession("directed-previews",false);
        Require(session.TryAddConnection(a,out _)&&session.TryAddConnection(b,out _),"Directed preview seats");
        Require(session.TryStartBattleIfReady(out _),"Directed preview start");
        var state=State(session);state.RestoreRuntimeState(4,PlayerId.Player,PhaseType.Main);
        state.Player.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(50,50,50,50));
        state.AI.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(50,50,50,50));

        // 0. Upgraded card summon must NOT emit UpgradeApplied combat log entry
        state.Player.Hand.Add("Goblin");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.PlayUnitCard,CardId="Goblin",TargetCoord=new TileCoordDto{Column=0,Row=0}});
        var summonLogs=session.CreatePersistenceSnapshot("dir-upgrade-check").CombatLogEntries!;
        Require(!summonLogs.Any(e=>e.EntryType==BattleCombatLogEntryType.UpgradeApplied),"Upgraded card summon must never emit UpgradeApplied log");

        // 1. Untargeted spell: ManaStone -> Single-card preview without combat stats
        state.Player.Hand.Add("ManaStone");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.CastScriptedSpell,CardId="ManaStone"});
        var manaStoneLog=session.CreatePersistenceSnapshot("dir-manastone").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.SpellCast && e.SourceCardId=="ManaStone");
        Require(manaStoneLog.TryGetSingleCardPreview(out var manaStonePreview,out var manaStoneOwner) && manaStoneOwner==PlayerId.Player && !manaStonePreview.HasCombatStats,"Untargeted ManaStone uses single-card preview without stats");
        Require(!manaStoneLog.TryGetDirectedPreview(out _,out _),"Untargeted ManaStone is not directed");

        // 1b. BiochemicalBomb SpellCast -> Single-card preview only (no targets on SpellCast), while turn-start Damage is directed
        state.Player.Hand.Add("BiochemicalBomb");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.CastScriptedSpell,CardId="BiochemicalBomb",HasTarget=true,TargetOwnerId=PlayerIdDto.AI,TargetCoord=new TileCoordDto{Column=0,Row=0}});
        var bioCastLog=session.CreatePersistenceSnapshot("dir-biocast").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.SpellCast && e.SourceCardId=="BiochemicalBomb");
        Require(bioCastLog.TryGetSingleCardPreview(out var bioPreview,out var bioOwner) && bioOwner==PlayerId.Player && bioPreview.CardId=="BiochemicalBomb" && !bioPreview.HasCombatStats,"BiochemicalBomb SpellCast must show only single spell card");
        Require(!bioCastLog.TryGetDirectedPreview(out _,out _),"BiochemicalBomb SpellCast must not include target cards");

        // 2. Single-target spell: Daehwandan -> Directed preview on SpellCast and AttackBuffApplied
        state.Player.Hand.Add("Daehwandan");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.CastScriptedSpell,CardId="Daehwandan"});
        var daehwandanLogs=session.CreatePersistenceSnapshot("dir-daehwandan").CombatLogEntries!;
        var daehwandanCast=daehwandanLogs.Last(e=>e.EntryType==BattleCombatLogEntryType.SpellCast && e.SourceCardId=="Daehwandan");
        var daehwandanBuff=daehwandanLogs.Last(e=>e.EntryType==BattleCombatLogEntryType.AttackBuffApplied && e.SourceCardId=="Daehwandan");
        Require(daehwandanCast.TryGetDirectedPreview(out var dSrc,out var dTargets) && dSrc.CardId=="Daehwandan" && !dSrc.HasCombatStats && dTargets.Count==1 && string.Equals(dTargets[0].CardId,"Master",StringComparison.OrdinalIgnoreCase) && dTargets[0].Attack==3,"Daehwandan SpellCast directed preview");
        Require(daehwandanBuff.TryGetDirectedPreview(out var bSrc,out var bTargets) && bSrc.CardId=="Daehwandan" && bTargets.Count==1 && bTargets[0].Attack==3,"Daehwandan AttackBuffApplied directed preview");

        // 3. Single-target damage spell: firebolt -> Directed preview with DamageTaken/Died on both SpellCast and Damage
        var enemyUnit=new UnitState("enemy-1","Goblin",PlayerId.AI,new Project333.Runtime.Domain.Board.TileCoord(0,0),AttackType.Melee,2,2,true,false,0,new Project333.Runtime.Domain.Resources.ResourceSet());
        state.AIBoard.Place(new Project333.Runtime.Domain.Board.TileCoord(0,0),enemyUnit);
        state.Player.Hand.Add("firebolt");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.CastDamageSpell,CardId="firebolt",TargetOwnerId=PlayerIdDto.AI,TargetCoord=new TileCoordDto{Column=0,Row=0}});
        var fireboltLogs=session.CreatePersistenceSnapshot("dir-firebolt").CombatLogEntries!;
        var fireboltCast=fireboltLogs.Last(e=>e.EntryType==BattleCombatLogEntryType.SpellCast && e.SourceCardId=="firebolt");
        var fireboltDmg=fireboltLogs.Last(e=>e.EntryType==BattleCombatLogEntryType.Damage && e.SourceCardId=="firebolt");
        Require(fireboltCast.TryGetDirectedPreview(out _,out var fbCastTargets) && fbCastTargets.Count==1 && fbCastTargets[0].DamageTaken==2 && fbCastTargets[0].Died,"firebolt SpellCast target died");
        Require(fireboltDmg.TryGetDirectedPreview(out _,out var fbDmgTargets) && fbDmgTargets.Count==1 && fbDmgTargets[0].DamageTaken==2 && fbDmgTargets[0].Died,"firebolt Damage target died");

        // 4. Multi-target spell: Firewall -> Directed preview with multiple targets on SpellCast
        state.AIBoard.Place(new Project333.Runtime.Domain.Board.TileCoord(1,0),new UnitState("e-2","Goblin",PlayerId.AI,new Project333.Runtime.Domain.Board.TileCoord(1,0),AttackType.Melee,2,30,true,false,0,new Project333.Runtime.Domain.Resources.ResourceSet()));
        state.AIBoard.Place(new Project333.Runtime.Domain.Board.TileCoord(2,0),new UnitState("e-3","Goblin",PlayerId.AI,new Project333.Runtime.Domain.Board.TileCoord(2,0),AttackType.Melee,2,10,true,false,0,new Project333.Runtime.Domain.Resources.ResourceSet()));
        state.Player.Hand.Add("Firewall");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.CastScriptedSpell,CardId="Firewall",HasTarget=true,TargetOwnerId=PlayerIdDto.AI,TargetCoord=new TileCoordDto{Column=1,Row=0}});
        var firewallCast=session.CreatePersistenceSnapshot("dir-firewall").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.SpellCast && e.SourceCardId=="Firewall");
        Require(firewallCast.TryGetDirectedPreview(out var fwSrc,out var fwTargets) && fwSrc.CardId=="Firewall" && fwTargets.Count>=2,"Firewall multi-target directed preview");
        Require(fwTargets.Any(t=>t.Died) && fwTargets.Any(t=>!t.Died && t.DamageTaken>0),"Firewall targets include both damaged survivor and dead unit");

        // 5. Trap secrecy (#40 TrapInstalled has no preview, #41 TrapTriggered has directed preview)
        state.Player.Hand.Add("ExplosiveReactiveArmour");
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.InstallTrap,CardId="ExplosiveReactiveArmour"});
        var trapInstalled=session.CreatePersistenceSnapshot("dir-trap-install").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.TrapInstalled);
        Require(!trapInstalled.HasHoldPreview && trapInstalled.SourcePreview==null,"TrapInstalled must not have hold preview");

        // Trigger ExplosiveReactiveArmour via AI melee attack on Player Master
        state.RestoreRuntimeState(5,PlayerId.AI,PhaseType.Main);
        var aiAttacker=(UnitState)state.AIBoard.GetOccupant(new Project333.Runtime.Domain.Board.TileCoord(1,0))!;
        aiAttacker.HasSummoningSickness=false;aiAttacker.RemainingAttacksThisTurn=1;
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.AI,CommandType=CommandType.Attack,SourceCoord=new TileCoordDto{Column=1,Row=0},TargetCoord=new TileCoordDto{Column=2,Row=1}});
        var trapTriggered=session.CreatePersistenceSnapshot("dir-trap-trig").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.TrapTriggered);
        Require(trapTriggered.HasHoldPreview && trapTriggered.TryGetDirectedPreview(out var trapSrc,out var trapTargets) &&
                trapSrc.CardId=="ExplosiveReactiveArmour" && !trapSrc.HasCombatStats && trapTargets.Count==1 && trapTargets[0].Died,"TrapTriggered directed preview with destruction outcome");

        // 6. Healing (#12 BlueDragon end-of-turn heal)
        state.RestoreRuntimeState(6,PlayerId.Player,PhaseType.Main);
        state.Player.Master.CurrentHp=300;
        var blueDragon=new UnitState("bd-1","BlueDragon",PlayerId.Player,new Project333.Runtime.Domain.Board.TileCoord(3,1),AttackType.Ranged,5,40,true,false,0,new Project333.Runtime.Domain.Resources.ResourceSet());
        blueDragon.CurrentHp=30;
        state.PlayerBoard.Place(new Project333.Runtime.Domain.Board.TileCoord(3,1),blueDragon);
        session.ApplyCommand(new ClientCommand{ActorId=PlayerIdDto.Player,CommandType=CommandType.EndTurn});
        var healEntry=session.CreatePersistenceSnapshot("dir-heal").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.Healing && e.SourceCardId=="BlueDragon");
        Require(healEntry.TryGetDirectedPreview(out var healSrc,out var healTargets) && healSrc.CardId=="BlueDragon" && healSrc.HasCombatStats && healTargets.Count>=1 && healTargets[0].HealAmount>0,"BlueDragon Healing directed preview with HealAmount");

        // Verify viewer projection and JSON persistence for TargetPreviews
        var saved=JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(JsonSerializer.Serialize(session.CreatePersistenceSnapshot("dir-json")))!;
        var restored=new BattleSession(session.MatchId,false);
        Require(restored.RestoreFromPersistenceSnapshot(saved),"Restore directed preview session");
        var restoredFirewall=restored.CreatePersistenceSnapshot("dir-restored").CombatLogEntries!.Last(e=>e.EntryType==BattleCombatLogEntryType.SpellCast && e.SourceCardId=="Firewall");
        Require(restoredFirewall.TryGetDirectedPreview(out _,out var restoredFwTargets) && restoredFwTargets.Count==fwTargets.Count,"Restored TargetPreviews count");
        foreach(var conn in new[]{a,b})
        {
            var view=session.CreateStateViewEnvelope(conn).StateView!;
            var projectedFw=view.CombatLogEntries.Last(e=>e.EntryType==BattleCombatLogEntryType.SpellCast && e.SourceCardId=="Firewall");
            Require(projectedFw.TryGetDirectedPreview(out _,out var projTargets) && projTargets.Count==fwTargets.Count,"Projected TargetPreviews count");
            var origHp=fwTargets[0].Hp;
            projTargets[0].Hp=999;
            Require(fwTargets[0].Hp==origHp,"Projection must not mutate TargetPreviews");
        }
    }
    private static BattleState State(BattleSession session)=>((BattleFlowController)typeof(BattleSession).GetField("_battleFlowController",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).CurrentBattleState!;
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
