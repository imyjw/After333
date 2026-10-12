using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class FirewallPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void SixteenNativeFullCellsHaveChronologicalNamesPointSamplingAndGroundOrigin()
        {
            var frames = Resources.LoadAll<Sprite>(FirewallVfx.FramesResourcePath).OrderBy(s => s.name).ToArray();
            Assert.That(frames.Length, Is.EqualTo(16));
            for (var index = 0; index < frames.Length; index++)
            {
                Assert.That(frames[index].name, Is.EqualTo("FlameWall_" + index.ToString("D2")));
                Assert.That(frames[index].rect, Is.EqualTo(new Rect(index % 4 * 512, (3 - index / 4) * 512, 512, 512)));
                Assert.That(frames[index].pivot, Is.EqualTo(new Vector2(256,16)));
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[0]));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(FirewallVfx.DurationSeconds, Is.EqualTo(16f / 12f));
        }

        [TestCase(PlayerId.Player, 0)] [TestCase(PlayerId.Player, 1)]
        [TestCase(PlayerId.AI, 0)] [TestCase(PlayerId.AI, 1)]
        public void EachSelectedTileHasItsOwnUprightSquareFlameWithGroundOrigin(PlayerId owner, int row)
        {
            using (var fixture = new Fixture(false))
            {
                foreach (var size in new[] {new Vector2(2340,1080), new Vector2(1280,720), new Vector2(1080,2340)})
                {
                    fixture.Layout(size);
                    Assert.That(fixture.Play(owner,row), Is.EqualTo(FirewallVfx.DurationSeconds));
                    fixture.AssertGeometry(owner,row);
                    Assert.That(fixture.Image.raycastTarget, Is.False);
                    Assert.That(fixture.Image.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                    Assert.That(fixture.Effect.GetComponents<AudioSource>(), Is.Empty);
                }
                fixture.Layout(new Vector2(2340,1080));
                foreach(var tiles in new[]{fixture.Board.PlayerTileViews,fixture.Board.AITileViews})
                {
                    var center=tiles.Aggregate(Vector2.zero,(sum,t)=>sum+((RectTransform)t.transform).anchoredPosition)/tiles.Length;
                    foreach(var tile in tiles)
                    {
                        var rect=(RectTransform)tile.transform;
                        rect.anchoredPosition=center+(Vector2)(Quaternion.Euler(0,0,6)*(rect.anchoredPosition-center));
                        rect.localRotation=Quaternion.Euler(0,0,6);
                    }
                }
                fixture.CanvasRect.localRotation = Quaternion.Euler(0,0,-11);
                Assert.That(fixture.Play(owner,row), Is.GreaterThan(0));
                fixture.AssertGeometry(owner,row);
            }
        }

        [TestCase(PlayerId.Player, 0)] [TestCase(PlayerId.Player, 1)]
        [TestCase(PlayerId.AI, 0)] [TestCase(PlayerId.AI, 1)]
        public void SeparateTileRootCanvasesStillPlayAcrossTheWholeRow(PlayerId owner, int row)
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Layout(new Vector2(1920,1080));
                fixture.SeparateTileCanvases();
                var tiles = owner == PlayerId.Player ? fixture.Board.PlayerTileViews : fixture.Board.AITileViews;
                Assert.That(tiles.Where(t => t.Coord.Row == row)
                    .Select(t => t.GetComponentInParent<Canvas>().rootCanvas).Distinct().Count(), Is.EqualTo(5));
                Assert.That(fixture.Play(owner,row), Is.EqualTo(FirewallVfx.DurationSeconds));
                fixture.SampleAge(.55f);
                fixture.AssertGeometry(owner,row);
                Assert.That(fixture.Images.Select(i=>i.sprite.name), Is.All.EqualTo("FlameWall_06"));
                Assert.That(fixture.Image.raycastTarget, Is.False);
                var overlay=fixture.Image.GetComponentInParent<Canvas>();
                Assert.That(overlay.overrideSorting, Is.True);
                Assert.That(overlay.sortingOrder, Is.EqualTo(4907));
                Assert.That(fixture.Play(owner,1-row), Is.EqualTo(FirewallVfx.DurationSeconds));
                fixture.AssertGeometry(owner,1-row);
                Assert.That(overlay.overrideSorting, Is.True, "Reusing the overlay on a different tile Canvas preserves sorting.");
                Assert.That(overlay.sortingOrder, Is.EqualTo(4907));
            }
        }

        [TestCase(PlayerId.Player,PlayerId.Player)] [TestCase(PlayerId.Player,PlayerId.AI)]
        [TestCase(PlayerId.AI,PlayerId.Player)] [TestCase(PlayerId.AI,PlayerId.AI)]
        public void ActualSeatProjectionKeepsExplicitOwnOrEnemyBoardAndFiveTargetsWithoutImmediateHit(PlayerId localSeat, PlayerId remoteTarget)
        {
            using (var fixture = new Fixture(false))
            {
                var host = new GameObject("DormantProjection");host.SetActive(false);host.transform.SetParent(fixture.Root.transform);
                var connection = host.AddComponent<OnlineBattleConnectionTester>();Set(connection,"_localPlayerId",localSeat);
                foreach (var row in new[]{0,1})
                {
                    var cast = Cast(PlayerId.Player,remoteTarget,row);
                    var area = Area(PlayerId.Player,remoteTarget,row);
                    var projected = (List<BattleEventDto>)typeof(OnlineBattleConnectionTester).GetMethod("ProjectBattleEventsToLocalPerspective",Hidden)
                        .Invoke(connection,new object[]{new[]{cast,area}});
                    var owner = remoteTarget == localSeat ? PlayerId.Player : PlayerId.AI;
                    foreach (var source in projected)
                    {
                        var request = fixture.OnlineRequest(source,new[]{source});
                        AssertTargets(request,owner,row);
                        Assert.That(((IList)Property(request,"Impacts")).Count,Is.Zero,"A successful wall cue is not damage.");
                        Assert.That(((IList)Property(request,"ValuePopupEvents")).Count,Is.Zero);
                        Assert.That(fixture.Estimate(request),Is.EqualTo(FirewallVfx.DurationSeconds+.1f).Within(.001f));
                    }
                }
                var restored = fixture.OnlineRequest(null,Array.Empty<BattleEventDto>());
                Assert.That(fixture.Estimate(restored),Is.Zero,"State-only restoration never guesses a wall cue from persistent effects.");
            }
        }

        [TestCase(PlayerId.Player,PlayerId.Player)] [TestCase(PlayerId.Player,PlayerId.AI)]
        [TestCase(PlayerId.AI,PlayerId.Player)] [TestCase(PlayerId.AI,PlayerId.AI)]
        public void RealCastHasNoImmediateDamageAndTwoGlobalTurnStartsMatchOnlineWithFreshPopupOwnership(PlayerId caster,PlayerId owner)
        {
            using (var fixture = new Fixture(false))
            {
                var state=fixture.State;var target=state.GetPlayer(owner).Master;var row=target.Position.Row;
                state.RestoreRuntimeState(1,caster,PhaseType.Main);
                state.GetPlayer(caster).Hand.Add("Firewall");state.GetPlayer(caster).Resources.Add(new ResourceSet(0,0,3,0));
                fixture.Gateway.ExecuteCommand(caster,new CastScriptedSpellCommand("Firewall",owner,new TileCoord(2,row)));
                Assert.That(target.CurrentHp,Is.EqualTo(333));Assert.That(state.ValuePopupEvents,Is.Empty);
                for(var tick=0;tick<3;tick++)
                {
                    // Deliberately preserve the preceding turn's popup cache.
                    state.RestoreRuntimeState(tick+2,tick%2==0?PlayerId.AI:PlayerId.Player,PhaseType.TurnStart);
                    var before=Capture(state);var hp=target.CurrentHp;
                    new TurnStartService().ResolveTurnStart(state);
                    var local=Build(before,Capture(state),Clone(state));var steps=(IList)Property(local,"AreaSpellSteps");
                    Assert.That(steps.Count,Is.EqualTo(tick<2?1:0));
                    if(tick>=2) { Assert.That(target.CurrentHp,Is.EqualTo(hp));Assert.That(((IList)Property(local,"ValuePopupEvents")).Count,Is.Zero);continue; }
                    AssertTargets(steps[0],owner,row);
                    var popups=((IList)Property(steps[0],"ValuePopupEvents")).Cast<BattleValuePopupEvent>().ToArray();
                    Assert.That(popups.Length,Is.EqualTo(1));Assert.That(popups[0].Amount,Is.EqualTo(hp-target.CurrentHp));
                    Assert.That(((IList)Property(local,"ValuePopupEvents")).Count,Is.Zero);
                    Assert.That(((IList)Property(local,"Impacts")).Count,Is.Zero);
                    Assert.That(((IList)Property(steps[0],"Impacts")).Count,Is.EqualTo(1));
                    var area=state.AreaSpellEffectEvents.Single();var wire=Area(caster,owner,row);
                    var online=fixture.OnlineRequest(wire,new[]{wire,Damage(popups.Single())});
                    AssertTargets(online,owner,row);
                    Assert.That(((IList)Property(online,"ValuePopupEvents")).Cast<BattleValuePopupEvent>().Sum(p=>p.Amount),Is.EqualTo(popups.Sum(p=>p.Amount)));
                    Assert.That(area.ValuePopupEventIndex,Is.EqualTo(tick));
                }
                // Snapshotting an unresolved turn with the same old transient list is not a new trigger.
                state.SetPhase(PhaseType.TurnStart);var stale=Capture(state);
                var repeated=Build(stale,Capture(state),Array.Empty<BattleValuePopupEvent>());
                Assert.That(((IList)Property(repeated,"AreaSpellSteps")).Count,Is.Zero);
            }
        }

        [Test]
        public void EmptyRowsAtSameRawIndexKeepCreationOrderAndLaterExactDeathStaysWithLethalWall()
        {
            using (var fixture=new Fixture(false))
            {
                var state=fixture.State;state.SetPhase(PhaseType.TurnStart);
                var before=Capture(state);
                state.RecordAreaSpellEffect(new BattleAreaSpellEffectEvent("firewall","Firewall",PlayerId.AI,PlayerId.Player,Row(1),0));
                state.RecordAreaSpellEffect(new BattleAreaSpellEffectEvent("firewall","Firewall",PlayerId.Player,PlayerId.AI,Row(1),0));
                state.SetPhase(PhaseType.Main);
                var request=Build(before,Capture(state),Clone(state));var steps=(IList)Property(request,"AreaSpellSteps");
                Assert.That(steps.Count,Is.EqualTo(2));AssertTargets(steps[0],PlayerId.Player,1);AssertTargets(steps[1],PlayerId.AI,1);
                foreach(var step in steps)Assert.That(((IList)Property(step,"ValuePopupEvents")).Count,Is.Zero);
                var lethal=new BattleEventDto{EventType=BattleEventType.DamageApplied,TargetOwnerId=PlayerId.AI,
                    TargetCoord=TileCoordDto.FromDomain(new TileCoord(0,1)),TargetRuntimeId="victim",RuntimeId="victim",SourceCardId="Firewall",Amount=33,HpBefore=12,HpAfter=-21};
                var removed=new BattleEventDto{EventType=BattleEventType.OccupantRemoved,TargetOwnerId=PlayerId.AI,
                    TargetCoord=lethal.TargetCoord,TargetRuntimeId="victim",RuntimeId="victim"};
                var groups=Groups(new[]{Area(PlayerId.Player,PlayerId.AI,1),lethal,Area(PlayerId.Player,PlayerId.AI,0),removed});
                Assert.That(groups[0].Contains(removed),Is.True);Assert.That(groups[1].Contains(removed),Is.False);
                Assert.That(Property(((IList)Property(fixture.OnlineRequest(groups[0][0],groups[0]),"Impacts"))[0],"Died"),Is.True);
                lethal.TargetRuntimeId="replacement";
                groups=Groups(new[]{Area(PlayerId.Player,PlayerId.AI,1),lethal,Area(PlayerId.Player,PlayerId.AI,0),removed});
                Assert.That(groups[0].Contains(removed),Is.False,"Net removal must match the exact damaged occupant.");
            }
        }

        [Test]
        public void RevivalPendingDemonKingKeepsCommonHurtWithoutFalseLocalOrOnlineDeath()
        {
            using(var fixture=new Fixture(false))
            {
                var state=fixture.State;var position=new TileCoord(0,1);
                var demon=new UnitState("fire-demon",DemonKingRules.CardId,PlayerId.AI,position,AttackType.Melee,33,33,true,false,0);
                state.AIBoard.Place(position,demon);
                state.PersistentEffects.Add(new PersistentEffectState("Firewall",PlayerId.Player,"firewall",1,"",new ResourceSet(),
                    targetRow:1,remainingTriggers:2,effectDamage:33,effectDamageType:DamageType.Magic));
                state.SetPhase(PhaseType.TurnStart);var before=Capture(state);new TurnStartService().ResolveTurnStart(state);
                Assert.That(demon.IsDemonKingRevivalPending,Is.True);Assert.That(state.AIBoard.GetOccupant(position),Is.SameAs(demon));
                var local=Build(before,Capture(state),Clone(state));var step=((IList)Property(local,"AreaSpellSteps"))[0];
                var ids=(IDictionary)Property(step,"AreaImpactRuntimeIds");
                var impact=((IList)Property(step,"Impacts")).Cast<object>().Single(i=>(string)ids[i]==demon.RuntimeId);
                Assert.That(Property(impact,"TookDamage"),Is.True);Assert.That(Property(impact,"Died"),Is.False);
                var wire=Area(PlayerId.Player,PlayerId.AI,1);
                var online=fixture.OnlineRequest(wire,new[]{wire,Damage(state.ValuePopupEvents.Single(p=>p.RuntimeId==demon.RuntimeId))});
                Assert.That(Property(((IList)Property(online,"Impacts"))[0],"Died"),Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RebindingDuringOnlineWallSkipsOldPopupHurtAndDeathWhileTileWallRemains()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var fixture=new Fixture())
            {
                var tile=fixture.Board.AITileViews.Single(t=>t.Coord==new TileCoord(0,1));
                var view=tile.gameObject.AddComponent<TileTextView>();Set(view,"_lastVisualRuntimeId","old-target");
                var area=Area(PlayerId.Player,PlayerId.AI,1);
                var damage=new BattleEventDto{EventType=BattleEventType.DamageApplied,TargetOwnerId=PlayerId.AI,TargetCoord=TileCoordDto.FromDomain(tile.Coord),
                    RuntimeId="old-target",TargetRuntimeId="old-target",SourceCardId="Firewall",Amount=33,HpBefore=10,HpAfter=-23};
                var removed=new BattleEventDto{EventType=BattleEventType.OccupantRemoved,TargetOwnerId=PlayerId.AI,TargetCoord=damage.TargetCoord,
                    RuntimeId="old-target",TargetRuntimeId="old-target"};
                var request=fixture.OnlineRequest(area,new[]{area,damage,removed});
                var identities=(IDictionary)Property(request,"AreaImpactRuntimeIds");
                var impacts=(IList)Property(request,"Impacts");Assert.That(impacts.Count,Is.EqualTo(1));
                Assert.That(identities[impacts[0]],Is.EqualTo("old-target"),"The final merged impact has the exact wire identity.");
                var sequence=(IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayImpactAnimationSequence",Hidden).Invoke(fixture.Bootstrapper,new[]{request});
                Assert.That(sequence.MoveNext(),Is.True);var wall=(IEnumerator)sequence.Current;Assert.That(wall.MoveNext(),Is.True);
                fixture.AssertGeometry(PlayerId.AI,1);Set(view,"_lastVisualRuntimeId","replacement");
                Assert.That(wall.MoveNext(),Is.False);Assert.That(sequence.MoveNext(),Is.False,"No popup or reaction wait is scheduled for the replacement.");
                Assert.That(view.GetComponents<AudioSource>(),Is.Empty);fixture.AssertGeometry(PlayerId.AI,1);
                fixture.Bootstrapper.CancelCombatPresentation();fixture.AssertHidden();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActualPixelsCoverAllFiveOriginsAndLeaveOtherRowClearOnBothBoards()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var fixture=new Fixture())
            {
                foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})foreach(var row in new[]{0,1})
                {
                    var baseline=fixture.RenderBaseline();
                    try
                    {
                        Assert.That(fixture.Play(owner,row),Is.GreaterThan(0));fixture.SampleAge(.45f);
                        fixture.AssertGeometry(owner,row);fixture.CaptureAndAssertPixels(owner,row,baseline);
                    }
                    finally{Object.DestroyImmediate(baseline);}
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SuccessfulLocalCastOnEmptyOwnRowStartsOnceBusyFailedAndCancelledCastsDoNotReplay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var fixture=new Fixture())
            {
                fixture.State.Player.Hand.Add("Firewall");fixture.State.Player.Resources.Add(new ResourceSet(0,0,3,0));
                Assert.That(fixture.Bootstrapper.TryUsePlayerCardOnTile("Firewall",PlayerId.Player,new TileCoord(4,1),out _),Is.True);
                fixture.AssertGeometry(PlayerId.Player,1);Assert.That(fixture.State.Player.Master.CurrentHp,Is.EqualTo(333));
                Assert.That(fixture.Bootstrapper.TryUsePlayerCardOnTile("Firewall",PlayerId.AI,new TileCoord(0,0),out _),Is.False);
                Assert.That(fixture.State.PersistentEffects.Count,Is.EqualTo(1));
                fixture.Bootstrapper.CancelCombatPresentation();fixture.AssertHidden();
                var timeout=Time.unscaledTime+FirewallVfx.DurationSeconds+1;
                while(fixture.Bootstrapper.IsBusy&&Time.unscaledTime<timeout)yield return null;
                fixture.State.Player.Hand.Add("Firewall");
                LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("Play card interaction failed.*"));
                Assert.That(fixture.Bootstrapper.TryUsePlayerCardOnTile("Firewall",PlayerId.AI,new TileCoord(0,0),out _),Is.False);
                fixture.AssertHidden();Assert.That(fixture.State.PersistentEffects.Count,Is.EqualTo(1));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LocalAiConfirmedFirewallCommandUsesItsActualTargetAndNoImmediateDamage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var fixture=new Fixture())
            {
                var state=fixture.State;state.RestoreRuntimeState(1,PlayerId.AI,PhaseType.Main);
                foreach(var card in state.AI.Hand.Cards.ToArray())state.AI.Hand.Remove(card.CardId,card.RuntimeId);
                state.AI.Hand.Add("Firewall");
                state.AI.Resources.Add(new ResourceSet(0,0,3,0));
                foreach(var column in new[]{0,1,3,4})
                {
                    var position=new TileCoord(column,0);
                    state.PlayerBoard.Place(position,new UnitState("fire-victim-"+column,"Goblin",PlayerId.Player,position,AttackType.Melee,1,30,true,false,0));
                }
                var ai=new AiDecisionService(fixture.Provider,new TargetingService(),ZeroCardUpgradeLevelProvider.Instance,
                    new AiSearchOptions{MaxDepth=1,BeamWidth=5,MaxSimulations=1200,MaxMilliseconds=500});
                var command=ai.GetNextCommand(state) as CastScriptedSpellCommand;
                Assert.That(command,Is.Not.Null);Assert.That(command.CardId,Is.EqualTo("Firewall"));
                Set(fixture.Bootstrapper,"_aiDecisionService",ai);
                var sequence=(IEnumerator)typeof(BattleBootstrapper).GetMethod("RunAiTurnSequence",Hidden).Invoke(fixture.Bootstrapper,null);
                Assert.That(sequence.MoveNext(),Is.True);
                Assert.That(state.PersistentEffects.Single().EffectId,Is.EqualTo("firewall"));
                var impactSequence=(IEnumerator)sequence.Current;
                fixture.Bootstrapper.StartCoroutine(impactSequence);
                var timeout=Time.unscaledTime+2;
                while(!fixture.IsPlaying&&Time.unscaledTime<timeout)yield return null;
                Assert.That(fixture.IsPlaying,Is.True);fixture.AssertGeometry(command.TargetOwnerId,command.TargetCoord.Row);
                Assert.That(state.ValuePopupEvents,Is.Empty);Assert.That(state.Player.Master.CurrentHp,Is.EqualTo(333));
                fixture.Bootstrapper.CancelCombatPresentation();fixture.Bootstrapper.StopAllCoroutines();fixture.AssertHidden();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator OnlineCastAndTickAreSequentialCancellationDisableAndInvalidTileIdentityCleanUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var fixture=new Fixture())
            {
                var events=new[]{Cast(PlayerId.Player,PlayerId.Player,1),Area(PlayerId.AI,PlayerId.AI,0)};
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(events),Is.EqualTo(2*(FirewallVfx.DurationSeconds+.1f)).Within(.001f));
                fixture.AssertGeometry(PlayerId.Player,1);
                var timeout=Time.unscaledTime+5;
                while((PlayerId)Get(fixture.Effect,"_owner")!=PlayerId.AI&&Time.unscaledTime<timeout)yield return null;
                fixture.AssertGeometry(PlayerId.AI,0);
                fixture.Bootstrapper.CancelCombatPresentation();fixture.AssertHidden();
                fixture.Bootstrapper.PlayOnlineBattleEventAnimations(events);fixture.Bootstrapper.CancelCombatPresentation();
                yield return new WaitForSecondsRealtime(FirewallVfx.DurationSeconds+.1f);fixture.AssertHidden();
                Assert.That(fixture.Play(PlayerId.Player,0),Is.GreaterThan(0));fixture.Effect.enabled=false;fixture.AssertHidden();fixture.Effect.enabled=true;
                fixture.Play(PlayerId.Player,0);fixture.Board.PlayerTileViews[0].Configure(PlayerId.AI,0,0);fixture.SampleAge(.1f);fixture.AssertHidden();
                fixture.Board.PlayerTileViews[0].Configure(PlayerId.Player,0,0);fixture.Board.PlayerTileViews[2].gameObject.SetActive(false);
                Assert.That(fixture.Play(PlayerId.Player,0),Is.Zero);fixture.AssertHidden();
                fixture.Board.PlayerTileViews[2].gameObject.SetActive(true);fixture.Play(PlayerId.AI,1);fixture.SampleAge(FirewallVfx.DurationSeconds+.01f);fixture.AssertHidden();
                Assert.That(fixture.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()),Is.Zero);
            }
            yield return new ExitPlayMode();
        }

        private static IEnumerable<TileCoord> Row(int row)=>Enumerable.Range(0,5).Select(column=>new TileCoord(column,row));
        private static BattleEventDto Cast(PlayerId caster,PlayerId owner,int row)=>new BattleEventDto
        {EventType=BattleEventType.SpellCast,CardId="Firewall",SourceCardId="Firewall",SourceOwnerId=caster,TargetOwnerId=owner,TargetCoord=TileCoordDto.FromDomain(new TileCoord(3,row))};
        private static BattleEventDto Area(PlayerId caster,PlayerId owner,int row)=>new BattleEventDto
        {EventType=BattleEventType.AreaSpellEffectTriggered,EffectId="firewall",SourceCardId="Firewall",SourceOwnerId=caster,TargetOwnerId=owner,TargetCoords=Row(row).Select(TileCoordDto.FromDomain).ToList()};
        private static BattleEventDto Damage(BattleValuePopupEvent popup)=>new BattleEventDto
        {EventType=BattleEventType.DamageApplied,SourceCardId=popup.SourceCardId,SourceOwnerId=popup.SourceOwnerId,TargetOwnerId=popup.OwnerId,
            TargetCoord=TileCoordDto.FromDomain(popup.Coord),RuntimeId=popup.RuntimeId,TargetRuntimeId=popup.RuntimeId,Amount=popup.Amount,
            HpBefore=popup.HpBefore,HpAfter=popup.HpAfter,DamageType=popup.DamageType,ValueCause=popup.Cause};
        private static object Capture(BattleState state)=>typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot",Static).Invoke(null,new object[]{state});
        private static IReadOnlyList<BattleValuePopupEvent> Clone(BattleState state)=>(IReadOnlyList<BattleValuePopupEvent>)typeof(BattleBootstrapper).GetMethod("CloneValuePopupEvents",Static).Invoke(null,new object[]{state});
        private static object Build(object before,object after,IReadOnlyList<BattleValuePopupEvent> events)=>typeof(BattleBootstrapper).GetMethod("BuildImpactAnimationRequest",Static).Invoke(null,new object[]{before,after,events,null,null});
        private static List<IReadOnlyList<BattleEventDto>> Groups(IReadOnlyList<BattleEventDto> events)=>(List<IReadOnlyList<BattleEventDto>>)typeof(BattleBootstrapper).GetMethod("BuildOnlineAnimationGroups",Static).Invoke(null,new object[]{events});
        private static object Property(object instance,string name)=>instance.GetType().GetProperty(name).GetValue(instance);
        private static object Get(object instance,string name)=>instance.GetType().GetField(name,Hidden).GetValue(instance);
        private static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,Hidden).SetValue(instance,value);
        private static void AssertTargets(object request,PlayerId owner,int row)
        {
            Assert.That(Property(request,"SpellEffectId"),Is.EqualTo("firewall"));
            var targets=(IList)Property(request,"SpellEffectTargets");Assert.That(targets.Count,Is.EqualTo(5));
            Assert.That(targets.Cast<object>().Select(t=>(TileCoord)Property(t,"Coord")),Is.EqualTo(Row(row)));
            Assert.That(targets.Cast<object>().All(t=>(PlayerId)Property(t,"OwnerId")==owner),Is.True);
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root=new GameObject("FirewallPresentationFixture");
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly FirewallVfx Effect;
            internal readonly LocalBattleGateway Gateway;
            internal readonly RectTransform CanvasRect;
            internal readonly BoardPresenter Board;
            internal readonly ICardDefinitionProvider Provider;
            internal float TileWidth;
            internal BattleState State=>Gateway.CurrentBattleState;
            internal Image[] Images=>Root.GetComponentsInChildren<Image>(true).Where(i=>i.name.StartsWith("FirewallFlameWall_")).OrderBy(i=>i.name).ToArray();
            internal Image Image=>Images.FirstOrDefault();
            internal bool IsPlaying=>(bool)Get(Effect,"_playing");
            private readonly Camera _camera;
            private readonly RenderTexture _render;
            private readonly string _oldToken;
            internal Fixture(bool active=true)
            {
                var cameraObject=new GameObject("PreviewCamera",typeof(Camera));cameraObject.transform.SetParent(Root.transform);
                _camera=cameraObject.GetComponent<Camera>();_camera.transform.position=new Vector3(0,0,-10);
                _camera.orthographic=true;_camera.orthographicSize=5.4f;_camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.035f,.045f,.06f);
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {_render=new RenderTexture(2340,1080,24);_render.Create();_camera.targetTexture=_render;}
                var canvas=new GameObject("BattleCanvas",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(Root.transform,false);canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_camera;
                CanvasRect=(RectTransform)canvas.transform;CanvasRect.localScale=Vector3.one*.01f;
                var boardHost=new GameObject("Board");boardHost.SetActive(false);boardHost.transform.SetParent(Root.transform);
                Board=boardHost.AddComponent<BoardPresenter>();Set(Board,"_useResponsiveLayout",false);
                Set(Board,"_playerTileViews",MakeTiles(PlayerId.Player));Set(Board,"_aiTileViews",MakeTiles(PlayerId.AI));
                Effect=new GameObject("FirewallEffectHost").AddComponent<FirewallVfx>();Effect.transform.SetParent(Root.transform);
                var host=new GameObject("Coordinator");host.SetActive(false);host.transform.SetParent(Root.transform);
                Bootstrapper=host.AddComponent<BattleBootstrapper>();Set(Bootstrapper,"_startOnAwake",false);Set(Bootstrapper,"_autoRunAiTurns",false);
                Set(Bootstrapper,"_returnToDraftSceneWhenRunSessionActive",false);
                var screenHost=new GameObject("DormantScreen");screenHost.SetActive(false);screenHost.transform.SetParent(Root.transform);
                var screen=screenHost.AddComponent<BattleScreenPresenter>();Set(screen,"_boardPresenter",Board);Set(Bootstrapper,"_battleScreenPresenter",screen);Set(Bootstrapper,"_firewallVfx",Effect);
                Provider=JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
                var flow=new BattleFlowController(new BattleSetupService(),new MulliganService(),new TurnStartService(),
                    new BattleCommandProcessor(new PlayCardService(Provider),new SpellService(Provider),new MoveService(),new AttackService(new TargetingService()),new EndTurnService()));
                Gateway=new LocalBattleGateway(flow);var deck=Enumerable.Repeat("Goblin",33).ToArray();Gateway.StartBattle(new BattleSetupRequest(deck,deck,PlayerId.Player));State.SetPhase(PhaseType.Main);
                Set(Bootstrapper,"_battleGateway",Gateway);Set(Bootstrapper,"_cardDefinitionProvider",Provider);_oldToken=AccountSessionState.SessionToken;
                if(active){typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null,"synthetic-firewall-presentation-session");host.SetActive(true);}
                Layout(new Vector2(2340,1080));
            }
            private TileView[] MakeTiles(PlayerId owner)
            {
                var tiles=new TileView[10];
                for(var index=0;index<tiles.Length;index++)
                {
                    var tile=new GameObject(owner+"Tile"+index,typeof(RectTransform),typeof(Image),typeof(TileView)).GetComponent<TileView>();
                    tile.transform.SetParent(CanvasRect,false);tile.Configure(owner,index%5,index/5);
                    var tileImage=tile.GetComponent<Image>();tileImage.color=new Color(.1f,.16f,.21f,.8f);tileImage.raycastTarget=false;
                    var unit=new GameObject("ReferenceUnit",typeof(RectTransform),typeof(Image)).GetComponent<Image>();unit.transform.SetParent(tile.transform,false);unit.raycastTarget=false;
                    unit.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Unit/A-301/Idle.png").OfType<Sprite>().First();tiles[index]=tile;
                }
                return tiles;
            }
            internal void SeparateTileCanvases()
            {
                foreach (var tile in Board.PlayerTileViews.Concat(Board.AITileViews))
                {
                    var canvas = new GameObject(tile.name+"Canvas",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
                    canvas.transform.SetParent(Root.transform,false);
                    canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_camera;
                    var rect=(RectTransform)canvas.transform;
                    rect.sizeDelta=CanvasRect.sizeDelta;rect.localScale=CanvasRect.localScale;
                    rect.position=CanvasRect.position;rect.rotation=CanvasRect.rotation;
                    tile.transform.SetParent(rect,false);
                }
                Canvas.ForceUpdateCanvases();
            }
            internal void Layout(Vector2 size)
            {
                CanvasRect.sizeDelta=size;
                var layout=BoardResponsiveLayoutCalculator.Calculate((int)size.x,(int)size.y,new Rect(0,0,size.x,size.y),.9f,24f,48f,new Vector2(8,8),1f,170f);TileWidth=layout.TileSize.x;
                foreach(var tile in Board.PlayerTileViews.Concat(Board.AITileViews))
                {var rect=(RectTransform)tile.transform;rect.sizeDelta=layout.TileSize;rect.anchoredPosition=layout.GetTileCenter(tile.OwnerId,tile.Coord);((RectTransform)tile.transform.Find("ReferenceUnit")).sizeDelta=layout.TileSize*(96f*1.35f/80f);}
                Canvas.ForceUpdateCanvases();
            }
            internal float Play(PlayerId owner,int row)=>(float)typeof(FirewallVfx).GetMethod("Play",Hidden).Invoke(Effect,new object[]{Board,owner,row});
            internal void SampleAge(float age){Set(Effect,"_startedAt",Time.unscaledTime-age);typeof(FirewallVfx).GetMethod("LateUpdate",Hidden).Invoke(Effect,null);Canvas.ForceUpdateCanvases();}
            internal object OnlineRequest(BattleEventDto source,IReadOnlyList<BattleEventDto> events)=>typeof(BattleBootstrapper).GetMethod("BuildOnlineImpactAnimationRequest",Hidden).Invoke(Bootstrapper,new object[]{source?.CardId??source?.SourceCardId,source,events});
            internal float Estimate(object request)=>(float)typeof(BattleBootstrapper).GetMethod("EstimateImpactAnimationDuration",Hidden).Invoke(Bootstrapper,new[]{request});
            internal void AssertGeometry(PlayerId owner,int row)
            {
                var images=Images;
                Assert.That(images.Length,Is.EqualTo(5),"One independent flame is required for each affected tile.");
                var tiles=(owner==PlayerId.Player?Board.PlayerTileViews:Board.AITileViews).Where(t=>t.Coord.Row==row).OrderBy(t=>t.Coord.Column).ToArray();
                for(var i=0;i<tiles.Length;i++)
                {
                    var image=images[i];var rect=image.rectTransform;var tile=tiles[i];
                    Assert.That(image.gameObject.activeInHierarchy,Is.True);
                    Assert.That(image.raycastTarget,Is.False);
                    Assert.That(rect.rect.width,Is.EqualTo(rect.rect.height).Within(.002f),"Flames retain square source cells without stretching.");
                    Assert.That(rect.rect.width,Is.EqualTo(images[0].rectTransform.rect.width).Within(.002f));
                    Assert.That(rect.localScale,Is.EqualTo(Vector3.one));
                    Assert.That(Vector3.Dot(rect.up,_camera.transform.up),Is.GreaterThan(.99999f),"Every flame is screen-upright, even on rotated tile Canvases.");
                    var point=rect.InverseTransformPoint(tile.transform.position);
                    Assert.That(Mathf.Abs(point.x),Is.LessThan(.002f),"The flame ground belongs to this tile's center line.");
                    Assert.That(point.y,Is.GreaterThan(0));
                    Assert.That(point.y,Is.LessThan(rect.rect.yMax),"The flame rises through its own tile.");
                    var other=(owner==PlayerId.Player?Board.PlayerTileViews:Board.AITileViews).Single(t=>t.Coord.Row!=row&&t.Coord.Column==tile.Coord.Column);
                    Assert.That(Mathf.Abs(rect.InverseTransformPoint(other.transform.position).x),Is.GreaterThan(rect.rect.width*.5f),"No flame reaches the other row's tile center.");
                    var corners=new Vector3[4];rect.GetWorldCorners(corners);
                    foreach(var corner in corners)
                    {
                        var local=CanvasRect.InverseTransformPoint(corner);
                        Assert.That(local.x,Is.InRange(CanvasRect.rect.xMin-.01f,CanvasRect.rect.xMax+.01f));
                        Assert.That(local.y,Is.InRange(CanvasRect.rect.yMin-.01f,CanvasRect.rect.yMax+.01f));
                    }
                }
                Assert.That(images[0].rectTransform.rect.width,Is.LessThan(TileWidth*1.6f),"One flame must never span the five-tile line.");
            }
            internal void AssertHidden(){Assert.That(Images.All(i=>!i.gameObject.activeInHierarchy),Is.True);Assert.That(IsPlaying,Is.False);}
            internal Texture2D RenderBaseline()
            {
                typeof(FirewallVfx).GetMethod("Cancel",Hidden).Invoke(Effect,null);
                Assert.That(_render,Is.Not.Null,"The render check must run with the isolated Editor's graphics device.");
                Canvas.ForceUpdateCanvases();_camera.Render();var old=RenderTexture.active;
                var texture=new Texture2D(_render.width,_render.height,TextureFormat.RGB24,false);
                try{RenderTexture.active=_render;texture.ReadPixels(new Rect(0,0,_render.width,_render.height),0,0);texture.Apply();return texture;}
                finally{RenderTexture.active=old;}
            }
            internal void CaptureAndAssertPixels(PlayerId owner,int row,Texture2D baseline)
            {
                Assert.That(_render,Is.Not.Null,"The render check must run with the isolated Editor's graphics device.");
                Canvas.ForceUpdateCanvases();_camera.Render();var old=RenderTexture.active;var texture=new Texture2D(_render.width,_render.height,TextureFormat.RGB24,false);
                try
                {
                    RenderTexture.active=_render;texture.ReadPixels(new Rect(0,0,_render.width,_render.height),0,0);texture.Apply();
                    var path=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../Tools/Art/Preview/Firewall-"+owner+"-Row"+row+".png"));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());
                    var tiles=(owner==PlayerId.Player?Board.PlayerTileViews:Board.AITileViews).Where(t=>t.Coord.Row==row);
                    foreach(var tile in tiles)
                    {
                        var image=Images[tile.Coord.Column];
                        var flamePoint=image.rectTransform.TransformPoint(new Vector3(0,image.rectTransform.rect.height*.35f));
                        var selected=CountWarmPixels(texture,flamePoint,TileWidth*.2f)-CountWarmPixels(baseline,flamePoint,TileWidth*.2f);
                        Assert.That(selected,Is.GreaterThan(15),"New fire pixels must cover column "+tile.Coord.Column+" of "+owner+" row "+row);
                        var other=(owner==PlayerId.Player?Board.PlayerTileViews:Board.AITileViews).Single(t=>t.Coord.Row!=row&&t.Coord.Column==tile.Coord.Column);
                        Assert.That(CountChangedPixels(texture,baseline,other.transform.position,TileWidth*.2f),Is.Zero,"The other row's actual pixels match the wall-free render exactly.");
                    }
                    foreach(var image in Images)
                    {var renderer=image.GetComponent<CanvasRenderer>();Assert.That(renderer.GetMesh().vertexCount,Is.GreaterThan(0));Assert.That(renderer.materialCount,Is.GreaterThan(0));}
                }
                finally{RenderTexture.active=old;Object.DestroyImmediate(texture);}
            }
            private int CountWarmPixels(Texture2D texture,Vector3 world,float radius)
            {
                var point=_camera.WorldToViewportPoint(world);var x=Mathf.RoundToInt(point.x*texture.width);var y=Mathf.RoundToInt(point.y*texture.height);var count=0;
                var range=Mathf.Max(2,Mathf.RoundToInt(radius));
                for(var px=Mathf.Max(0,x-range);px<Mathf.Min(texture.width,x+range);px+=2)for(var py=Mathf.Max(0,y-range);py<Mathf.Min(texture.height,y+range);py+=2)
                {var color=texture.GetPixel(px,py);if(color.r>.4f&&color.r>color.g*1.15f&&color.b<.4f)count++;}
                return count;
            }
            private int CountChangedPixels(Texture2D texture,Texture2D baseline,Vector3 world,float radius)
            {
                var point=_camera.WorldToViewportPoint(world);var x=Mathf.RoundToInt(point.x*texture.width);var y=Mathf.RoundToInt(point.y*texture.height);var count=0;
                var range=Mathf.Max(2,Mathf.RoundToInt(radius));
                for(var px=Mathf.Max(0,x-range);px<Mathf.Min(texture.width,x+range);px++)for(var py=Mathf.Max(0,y-range);py<Mathf.Min(texture.height,y+range);py++)
                    if(texture.GetPixel(px,py)!=baseline.GetPixel(px,py))count++;
                return count;
            }
            public void Dispose(){typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null,_oldToken);Object.DestroyImmediate(Root);if(_render!=null){_render.Release();Object.DestroyImmediate(_render);}}
        }
    }
}
