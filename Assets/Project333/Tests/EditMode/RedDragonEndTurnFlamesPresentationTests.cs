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
    public sealed class RedDragonEndTurnFlamesPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const string ResourcePath = "Project333/Presentation/RedDragon/EndTurnFlames";
        private const string EffectId = "redDragonEndFlames";
        private static Type EffectType => typeof(TileTextView).Assembly.GetType("Project333.Runtime.Presentation.Battle.RedDragonEndTurnFlamesVfx", true);
        [UnityTearDown] public IEnumerator LeavePlayMode() { if(UnityEngine.Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void ReusedSixteenFullCellsHaveChronologicalFramesWithoutReplacingCurrentFirewall()
        {
            var frames=Resources.LoadAll<Sprite>(ResourcePath).OrderBy(s=>s.name).ToArray();
            Assert.That(frames.Length,Is.EqualTo(16));
            for(var index=0;index<frames.Length;index++)
            {
                Assert.That(frames[index].name,Is.EqualTo("EndTurnFlames_"+index.ToString("D2")));
                Assert.That(frames[index].rect.width,Is.EqualTo(512));Assert.That(frames[index].rect.height,Is.EqualTo(512));
                Assert.That(frames[index].pivot,Is.EqualTo(new Vector2(256,16)));
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[0]));
            Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
            Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(ResourcePath,Is.Not.EqualTo(FirewallVfx.FramesResourcePath));
            Assert.That(Resources.LoadAll<Sprite>(FirewallVfx.FramesResourcePath).Length,Is.EqualTo(16));
        }

        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void TenTilesIncludingEmptyTilesShowUniformUnrotatedFireAndReuseCleanly(PlayerId owner)
        {
            using(var f=new Fixture(false))
            {
                foreach(var size in new[]{new Vector2(1920,1080),new Vector2(2340,1080),new Vector2(1280,720)})
                {
                    f.Layout(size);Assert.That(f.Play(owner),Is.EqualTo(16f/12f).Within(.001f));
                    var images=f.Images;Assert.That(images.Length,Is.EqualTo(10));Assert.That(images.Select(i=>i.sprite.name),Is.All.EqualTo("EndTurnFlames_00"));
                    f.Sample(.45f);Assert.That(images.Select(i=>i.sprite.name),Is.All.EqualTo("EndTurnFlames_05"));
                    var tiles=owner==PlayerId.Player?f.Board.PlayerTileViews:f.Board.AITileViews;
                    foreach(var image in images)
                    {
                        var rect=image.rectTransform;
                        Assert.That(image.gameObject.activeInHierarchy,Is.True);Assert.That(image.raycastTarget,Is.False);
                        Assert.That(rect.rect.width,Is.EqualTo(rect.rect.height).Within(.01f));
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(rect.eulerAngles.z,0)),Is.LessThan(.01f));
                        Assert.That(rect.pivot,Is.EqualTo(new Vector2(.5f,16f/512f)));
                        Assert.That(rect.localScale,Is.EqualTo(Vector3.one));
                        Assert.That(tiles.Any(t=>Mathf.Abs(rect.InverseTransformPoint(t.transform.position).x)<.01f),Is.True);
                    }
                    Assert.That(images[0].GetComponentInParent<CanvasGroup>().blocksRaycasts,Is.False);
                    Assert.That(f.Effect.GetComponents<AudioSource>(),Is.Empty,"Common TileTextView hurt sound remains the sole hit sound.");
                    f.Sample(1.34f);Assert.That(images.All(i=>!i.gameObject.activeInHierarchy),Is.True);
                    f.Play(owner==PlayerId.Player?PlayerId.AI:PlayerId.Player);Assert.That(f.Images,Is.EqualTo(images));
                    f.Cancel();Assert.That(images.All(i=>!i.gameObject.activeInHierarchy),Is.True);
                }
                // Runtime disable callbacks are verified by the PlayMode coroutine below.
                f.Play(owner);
                (owner==PlayerId.Player?f.Board.PlayerTileViews:f.Board.AITileViews)[0].Configure(owner==PlayerId.Player?PlayerId.AI:PlayerId.Player,0,0);
                f.Sample(.1f);Assert.That(f.Images.All(i=>!i.gameObject.activeInHierarchy),Is.True);
            }
        }

        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void RealOwnerTurnEndCreatesOneFreshCueAndDamagePacketsBelongToItOnce(PlayerId caster)
        {
            using(var f=new Fixture(false))
            {
                var state=f.State;state.RestoreRuntimeState(1,caster,PhaseType.Main);
                var enemy=Other(caster);var dragon=Add(state,caster,0,0,"RedDragon",100);
                var victim=Add(state,enemy,0,0,"Goblin",15);
                var staleMaster=state.GetPlayer(enemy).Master;
                state.ValuePopupEvents.Add(new BattleValuePopupEvent(staleMaster.RuntimeId,enemy,staleMaster.Position,false,3,caster,dragon.RuntimeId,"RedDragon",BattleValueChangeCause.RedDragon,DamageType.Magic,336,333));
                var before=Capture(state);f.Gateway.ExecuteCommand(caster,new EndTurnCommand());
                Assert.That(state.GetBoard(enemy).GetOccupant(victim.Position),Is.Null);
                Assert.That(staleMaster.CurrentHp,Is.EqualTo(313));
                var cue=state.AreaSpellEffectEvents.Single();Assert.That(cue.EffectId,Is.EqualTo(EffectId));
                Assert.That(cue.SourceOwnerId,Is.EqualTo(caster));Assert.That(cue.TargetOwnerId,Is.EqualTo(enemy));Assert.That(cue.TargetCoords.Count,Is.EqualTo(10));
                var request=Build(before,Capture(state),state.ValuePopupEvents);var steps=(IList)Property(request,"AreaSpellSteps");Assert.That(steps.Count,Is.EqualTo(1));
                AssertTargets(steps[0],enemy);
                var packets=((IList)Property(steps[0],"ValuePopupEvents")).Cast<BattleValuePopupEvent>().ToArray();
                Assert.That(packets.Select(p=>p.RuntimeId),Is.EquivalentTo(new[]{staleMaster.RuntimeId,victim.RuntimeId}));
                Assert.That(packets.Sum(p=>p.Amount),Is.EqualTo(35));
                var impacts=((IList)Property(steps[0],"Impacts")).Cast<object>().ToArray();
                Assert.That(impacts.Count(i=>(bool)Property(i,"Died")),Is.EqualTo(1));
                Assert.That(((IList)Property(request,"ValuePopupEvents")).Count,Is.Zero);
                Assert.That(((IList)Property(request,"Impacts")).Count,Is.Zero,"The parent must not replay the same hurt/death.");
                var unchanged=Capture(state);var repeated=Build(unchanged,Capture(state),state.ValuePopupEvents);
                Assert.That(((IList)Property(repeated,"AreaSpellSteps")).Count,Is.Zero,"Restoring the same cached events does not ignite the field again.");
            }
        }

        [TestCase("drained")] [TestCase("erasure")] [TestCase("sealbound")] [TestCase("removed")] [TestCase("otherTurn")]
        public void SuppressedRemovedAndNonEndingOwnerDragonsNeverCreateFireCues(string mode)
        {
            using(var f=new Fixture(false))
            {
                var state=f.State;state.RestoreRuntimeState(1,PlayerId.Player,PhaseType.Main);
                var owner=mode=="otherTurn"?PlayerId.AI:PlayerId.Player;var dragon=Add(state,owner,0,0,"RedDragon",100);
                if(mode=="drained")dragon.RestoreSuppressionStates(true,false);
                if(mode=="erasure")dragon.ApplyErasure();
                if(mode=="sealbound")dragon.EnterSealbound(2);
                if(mode=="removed")state.GetBoard(owner).Remove(dragon.Position);
                new EndTurnService().EndTurn(state);
                Assert.That(state.AreaSpellEffectEvents,Is.Empty);Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(333));
            }
        }

        [Test]
        public void MultipleDragonsKeepPacketOrderAndRevivingDemonGetsHurtWithoutFalseDeath()
        {
            using(var f=new Fixture(false))
            {
                var state=f.State;Add(state,PlayerId.Player,0,0,"RedDragon",100);Add(state,PlayerId.Player,1,0,"RedDragon",100);
                var demon=Add(state,PlayerId.AI,0,0,DemonKingRules.CardId,20);
                var before=Capture(state);f.Gateway.ExecuteCommand(PlayerId.Player,new EndTurnCommand());
                Assert.That(state.AreaSpellEffectEvents.Count,Is.EqualTo(2));
                Assert.That(state.AreaSpellEffectEvents.Select(e=>e.ValuePopupEventIndex),Is.EqualTo(new[]{0,2}));
                var request=Build(before,Capture(state),state.ValuePopupEvents);var steps=(IList)Property(request,"AreaSpellSteps");
                Assert.That(steps.Count,Is.EqualTo(2));AssertTargets(steps[0],PlayerId.AI);AssertTargets(steps[1],PlayerId.AI);
                Assert.That(((IList)Property(steps[0],"ValuePopupEvents")).Count,Is.EqualTo(2));
                Assert.That(((IList)Property(steps[1],"ValuePopupEvents")).Count,Is.EqualTo(1));
                Assert.That(demon.IsDemonKingRevivalPending,Is.True);
                var impacts=((IList)Property(steps[0],"Impacts")).Cast<object>();
                Assert.That(impacts.Where(i=>(TileCoord)Property(i,"Coord")==demon.Position).Select(i=>(bool)Property(i,"Died")),Is.All.False);
                Assert.That(state.AI.Master.CurrentHp,Is.EqualTo(293));
                f.Gateway.ResolveTurnStart();
                Assert.That(state.AreaSpellEffectEvents.Count(e=>e.EffectId==EffectId),Is.EqualTo(2),
                    "Online turn-start resolution runs before its transient area cues are exported.");
            }
        }

        [Test]
        public void SeparateNextTurnStartDoesNotReplayCachedRedDragonDamageHurtDeathOrFlames()
        {
            using(var f=new Fixture(false))
            {
                Add(f.State,PlayerId.Player,0,0,"RedDragon",100);Add(f.State,PlayerId.AI,0,0,"Goblin",15);
                f.Gateway.ExecuteCommand(PlayerId.Player,new EndTurnCommand());
                Assert.That(f.State.Phase,Is.EqualTo(PhaseType.TurnStart));
                var cue=f.State.AreaSpellEffectEvents.Single();var before=Capture(f.State);
                var hp=f.State.AI.Master.CurrentHp;
                f.Gateway.ResolveTurnStart();
                Assert.That(f.State.AreaSpellEffectEvents.Single(),Is.SameAs(cue));
                Assert.That(f.State.AI.Master.CurrentHp,Is.EqualTo(hp));
                var request=Build(before,Capture(f.State),f.State.ValuePopupEvents);
                Assert.That(((IList)Property(request,"AreaSpellSteps")).Count,Is.Zero);
                Assert.That(((IList)Property(request,"ValuePopupEvents")).Count,Is.Zero);
                Assert.That(((IList)Property(request,"Impacts")).Count,Is.Zero);
                Assert.That(f.Estimate(request),Is.Zero);
            }
        }

        [TestCase(PlayerId.Player,PlayerId.Player)] [TestCase(PlayerId.Player,PlayerId.AI)]
        [TestCase(PlayerId.AI,PlayerId.Player)] [TestCase(PlayerId.AI,PlayerId.AI)]
        public void WireSeatProjectionShowsTheOppositeFieldAndStateOnlyReconnectDoesNotReplay(PlayerId localSeat,PlayerId caster)
        {
            using(var f=new Fixture(false))
            {
                var connection=f.Host.AddComponent<OnlineBattleConnectionTester>();Set(connection,"_localPlayerId",localSeat);
                var wire=new BattleEventDto{EventType=BattleEventType.AreaSpellEffectTriggered,EffectId=EffectId,SourceCardId="RedDragon",SourceOwnerId=caster,
                    TargetOwnerId=Other(caster),TargetCoords=Coords().Select(TileCoordDto.FromDomain).ToList()};
                var damage=new BattleEventDto{EventType=BattleEventType.DamageApplied,SourceCardId="RedDragon",SourceOwnerId=caster,TargetOwnerId=Other(caster),
                    TargetCoord=TileCoordDto.FromDomain(new TileCoord(0,0)),RuntimeId="victim",TargetRuntimeId="victim",Amount=33,HpBefore=15,HpAfter=-18,DamageType=DamageType.Magic,ValueCause=BattleValueChangeCause.RedDragon};
                var removed=new BattleEventDto{EventType=BattleEventType.OccupantRemoved,TargetOwnerId=Other(caster),TargetCoord=damage.TargetCoord,RuntimeId="victim",TargetRuntimeId="victim"};
                var events=(List<BattleEventDto>)typeof(OnlineBattleConnectionTester).GetMethod("ProjectBattleEventsToLocalPerspective",Hidden).Invoke(connection,new object[]{new[]{wire,damage,removed}});
                var request=f.Online(events[0],events);AssertTargets(request,Other(caster)==localSeat?PlayerId.Player:PlayerId.AI);
                Assert.That(((IList)Property(request,"ValuePopupEvents")).Count,Is.EqualTo(1));Assert.That(((IList)Property(request,"Impacts")).Count,Is.EqualTo(1));
                Assert.That(Property(((IList)Property(request,"Impacts"))[0],"Died"),Is.True);
                Assert.That(f.Estimate(request),Is.GreaterThanOrEqualTo(16f/12f));
                Assert.That(f.Estimate(f.Online(null,Array.Empty<BattleEventDto>())),Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator OnlineEndFlamesCancelWithoutDelayedReplacementHurtOrSnapshotReplay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var wire=new BattleEventDto{EventType=BattleEventType.AreaSpellEffectTriggered,EffectId=EffectId,SourceCardId="RedDragon",SourceOwnerId=PlayerId.Player,
                    TargetOwnerId=PlayerId.AI,TargetCoords=Coords().Select(TileCoordDto.FromDomain).ToList()};
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(new[]{wire}),Is.GreaterThan(0));
                Assert.That(f.Images.Length,Is.EqualTo(10));Assert.That(f.Images.All(i=>i.gameObject.activeInHierarchy),Is.True);
                f.Effect.enabled=false;Assert.That(f.Images.All(i=>!i.gameObject.activeInHierarchy),Is.True);
                f.Effect.enabled=true;f.Bootstrapper.PlayOnlineBattleEventAnimations(new[]{wire});
                Assert.That(f.Images.All(i=>i.gameObject.activeInHierarchy),Is.True);
                f.Bootstrapper.CancelCombatPresentation();Assert.That(f.Images.All(i=>!i.gameObject.activeInHierarchy),Is.True);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(f.Images.All(i=>!i.gameObject.activeInHierarchy),Is.True);
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()),Is.Zero);
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void PreviousServerRedDragonDamagePacketsGetOneCuePerSourceWithoutDuplicatingExplicitCues()
        {
            using(var f=new Fixture(false))
            {
                var packets=new[]{"dragon-a","dragon-a","dragon-b"}.Select((source,index)=>new BattleEventDto
                {EventType=BattleEventType.DamageApplied,SourceCardId="RedDragon",SourceRuntimeId=source,SourceOwnerId=PlayerId.Player,
                    TargetOwnerId=PlayerId.AI,TargetCoord=TileCoordDto.FromDomain(new TileCoord(index,0)),RuntimeId="victim-"+index,TargetRuntimeId="victim-"+index,
                    Amount=33,HpBefore=100,HpAfter=67,DamageType=DamageType.Magic,ValueCause=BattleValueChangeCause.RedDragon}).ToArray();
                var groups=(List<IReadOnlyList<BattleEventDto>>)typeof(BattleBootstrapper).GetMethod("BuildOnlineAnimationGroups",Static).Invoke(null,new object[]{packets});
                Assert.That(groups.Count,Is.EqualTo(2));
                foreach(var group in groups)
                {
                    var cue=group.Single(e=>e.EventType==BattleEventType.AreaSpellEffectTriggered);
                    AssertTargets(f.Online(cue,group),PlayerId.AI);
                }
                var explicitCue=new BattleEventDto{EventType=BattleEventType.AreaSpellEffectTriggered,EffectId=EffectId,SourceCardId="RedDragon",
                    SourceOwnerId=PlayerId.Player,TargetOwnerId=PlayerId.AI,TargetCoords=Coords().Select(TileCoordDto.FromDomain).ToList()};
                groups=(List<IReadOnlyList<BattleEventDto>>)typeof(BattleBootstrapper).GetMethod("BuildOnlineAnimationGroups",Static).Invoke(null,
                    new object[]{new[]{explicitCue}.Concat(packets.Take(2)).ToArray()});
                Assert.That(groups.Count,Is.EqualTo(1));Assert.That(groups[0].Count(e=>e.EventType==BattleEventType.AreaSpellEffectTriggered),Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator ReplacementDuringFireDoesNotReceiveOldOccupantsHurtDeathSoundOrDamagePopup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var tile=f.Board.AITileViews.Single(t=>t.Coord==new TileCoord(0,0));
                var view=tile.gameObject.AddComponent<TileTextView>();Set(view,"_lastVisualRuntimeId","old-body");
                var cue=new BattleEventDto{EventType=BattleEventType.AreaSpellEffectTriggered,EffectId=EffectId,SourceCardId="RedDragon",
                    SourceOwnerId=PlayerId.Player,TargetOwnerId=PlayerId.AI,TargetCoords=Coords().Select(TileCoordDto.FromDomain).ToList()};
                var hit=new BattleEventDto{EventType=BattleEventType.DamageApplied,SourceCardId="RedDragon",SourceOwnerId=PlayerId.Player,
                    TargetOwnerId=PlayerId.AI,TargetCoord=TileCoordDto.FromDomain(tile.Coord),RuntimeId="old-body",TargetRuntimeId="old-body",
                    Amount=15,HpBefore=15,HpAfter=0,DamageType=DamageType.Magic,ValueCause=BattleValueChangeCause.RedDragon};
                var removed=new BattleEventDto{EventType=BattleEventType.OccupantRemoved,TargetOwnerId=PlayerId.AI,
                    TargetCoord=hit.TargetCoord,RuntimeId="old-body",TargetRuntimeId="old-body"};
                var request=f.Online(cue,new[]{cue,hit,removed});
                var identities=(IDictionary)Property(request,"AreaImpactRuntimeIds");
                var impact=((IList)Property(request,"Impacts"))[0];Assert.That(identities[impact],Is.EqualTo("old-body"));
                var sequence=(IEnumerator)typeof(BattleBootstrapper).GetMethod("PlayImpactAnimationSequence",Hidden).Invoke(f.Bootstrapper,new[]{request});
                var completed=new Completion();f.Bootstrapper.StartCoroutine(Finish(sequence,completed));
                Assert.That(f.Images.All(i=>i.gameObject.activeInHierarchy),Is.True);
                Set(view,"_lastVisualRuntimeId","replacement");
                var deadline=Time.unscaledTime+3;
                while(!completed.Done&&Time.unscaledTime<deadline)yield return null;
                Assert.That(completed.Done,Is.True);Assert.That(view.GetComponents<AudioSource>(),Is.Empty);
                Assert.That(f.Root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="ValuePopupText"),Is.False);
                Assert.That(f.Images.All(i=>!i.gameObject.activeInHierarchy),Is.True);
            }
            yield return new ExitPlayMode();
        }

        private sealed class Completion{public bool Done;}
        private static IEnumerator Finish(IEnumerator sequence,Completion done){yield return sequence;done.Done=true;}

        private static PlayerId Other(PlayerId owner)=>owner==PlayerId.Player?PlayerId.AI:PlayerId.Player;
        private static IEnumerable<TileCoord> Coords(){for(var row=0;row<2;row++)for(var column=0;column<5;column++)yield return new TileCoord(column,row);}
        private static UnitState Add(BattleState state,PlayerId owner,int column,int row,string card,int hp)
        {var coord=new TileCoord(column,row);var unit=new UnitState(card+"-"+owner+"-"+column,card,owner,coord,AttackType.Melee,3,hp,true,false,0);state.GetBoard(owner).Place(coord,unit);return unit;}
        private static object Capture(BattleState state)=>typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot",Static).Invoke(null,new object[]{state});
        private static object Build(object before,object after,IReadOnlyList<BattleValuePopupEvent> events)=>typeof(BattleBootstrapper).GetMethod("BuildImpactAnimationRequest",Static).Invoke(null,new object[]{before,after,events,null,null});
        private static object Property(object instance,string name)=>instance.GetType().GetProperty(name).GetValue(instance);
        private static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,Hidden).SetValue(instance,value);
        private static void AssertTargets(object request,PlayerId owner)
        {
            Assert.That(Property(request,"SpellEffectId"),Is.EqualTo(EffectId));
            var targets=((IList)Property(request,"SpellEffectTargets")).Cast<object>().ToArray();
            Assert.That(targets.Length,Is.EqualTo(10));Assert.That(targets.All(t=>(PlayerId)Property(t,"OwnerId")==owner),Is.True);
            Assert.That(targets.Select(t=>(TileCoord)Property(t,"Coord")),Is.EquivalentTo(Coords()));
        }

        private sealed class Fixture:IDisposable
        {
            internal readonly GameObject Root=new GameObject("RedDragonEndFlamesFixture");
            internal readonly GameObject Host;
            internal readonly MonoBehaviour Effect;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly BoardPresenter Board;
            internal readonly BattleState State;
            internal readonly LocalBattleGateway Gateway;
            private readonly RectTransform _canvas;
            private readonly string _oldToken;
            internal Image[] Images=>(Image[])EffectType.GetField("_images",Hidden).GetValue(Effect);
            internal Fixture(bool active=true)
            {
                _canvas=(RectTransform)new GameObject("BackgroundCanvas",typeof(RectTransform),typeof(Canvas)).transform;
                _canvas.SetParent(Root.transform,false);_canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;_canvas.localScale=Vector3.one*.01f;
                var board=new GameObject("DormantBoard");board.SetActive(false);board.transform.SetParent(Root.transform);Board=board.AddComponent<BoardPresenter>();
                Set(Board,"_useResponsiveLayout",false);Set(Board,"_playerTileViews",MakeTiles(PlayerId.Player));Set(Board,"_aiTileViews",MakeTiles(PlayerId.AI));
                Effect=(MonoBehaviour)new GameObject("EndFlames").AddComponent(EffectType);Effect.transform.SetParent(Root.transform);
                Host=new GameObject("DormantCoordinator");Host.SetActive(false);Host.transform.SetParent(Root.transform);
                Bootstrapper=Host.AddComponent<BattleBootstrapper>();Set(Bootstrapper,"_startOnAwake",false);Set(Bootstrapper,"_autoRunAiTurns",false);
                Set(Bootstrapper,"_returnToDraftSceneWhenRunSessionActive",false);Set(Bootstrapper,"_redDragonEndTurnFlamesVfx",Effect);
                var screenHost=new GameObject("DormantScreen");screenHost.SetActive(false);screenHost.transform.SetParent(Root.transform);
                var screen=screenHost.AddComponent<BattleScreenPresenter>();Set(screen,"_boardPresenter",Board);Set(Bootstrapper,"_battleScreenPresenter",screen);
                var provider=JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
                var flow=new BattleFlowController(new BattleSetupService(),new MulliganService(),new TurnStartService(),
                    new BattleCommandProcessor(new PlayCardService(provider),new SpellService(provider),new MoveService(),new AttackService(new TargetingService()),new EndTurnService()));
                var gateway=new LocalBattleGateway(flow);Gateway=gateway;var deck=Enumerable.Repeat("Goblin",33).ToArray();gateway.StartBattle(new BattleSetupRequest(deck,deck,PlayerId.Player));State=gateway.CurrentBattleState;State.SetPhase(PhaseType.Main);
                Set(Bootstrapper,"_battleGateway",gateway);Set(Bootstrapper,"_cardDefinitionProvider",provider);_oldToken=AccountSessionState.SessionToken;
                if(active){typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null,"synthetic-end-flames");Host.SetActive(true);}
                Layout(new Vector2(2340,1080));
            }
            private TileView[] MakeTiles(PlayerId owner)
            {
                return Coords().Select(coord=>{var tile=new GameObject(owner.ToString()+coord,typeof(RectTransform),typeof(Image),typeof(TileView)).GetComponent<TileView>();
                    tile.transform.SetParent(_canvas,false);tile.Configure(owner,coord.Column,coord.Row);return tile;}).ToArray();
            }
            internal void Layout(Vector2 size)
            {
                _canvas.sizeDelta=size;var layout=BoardResponsiveLayoutCalculator.Calculate((int)size.x,(int)size.y,new Rect(0,0,size.x,size.y),.9f,24,48,new Vector2(8,8),1,170);
                foreach(var tile in Board.PlayerTileViews.Concat(Board.AITileViews)){var rect=(RectTransform)tile.transform;rect.sizeDelta=layout.TileSize;rect.anchoredPosition=layout.GetTileCenter(tile.OwnerId,tile.Coord);}Canvas.ForceUpdateCanvases();
            }
            internal float Play(PlayerId owner)=>(float)EffectType.GetMethod("Play",Hidden).Invoke(Effect,new object[]{Board,owner});
            internal void Cancel()=>EffectType.GetMethod("Cancel",Hidden).Invoke(Effect,null);
            internal void Sample(float age){Set(Effect,"_startedAt",Time.unscaledTime-age);EffectType.GetMethod("LateUpdate",Hidden).Invoke(Effect,null);Canvas.ForceUpdateCanvases();}
            internal object Online(BattleEventDto source,IReadOnlyList<BattleEventDto> events)=>typeof(BattleBootstrapper).GetMethod("BuildOnlineImpactAnimationRequest",Hidden).Invoke(Bootstrapper,new object[]{source?.CardId??source?.SourceCardId,source,events});
            internal float Estimate(object request)=>(float)typeof(BattleBootstrapper).GetMethod("EstimateImpactAnimationDuration",Hidden).Invoke(Bootstrapper,new[]{request});
            public void Dispose(){typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null,_oldToken);Object.DestroyImmediate(Root);}
        }
    }
}
