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
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class PlasmaShieldPresentationTests
    {
        private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator LeavePlayMode(){Time.timeScale=1;if(UnityEngine.Application.isPlaying)yield return new ExitPlayMode();}

        [Test]
        public void InputLegalityWhiteHintsAndHoverMatchNonMasterUnitAndBuildingTargets()
        {
            using(var f=new Fixture(false))
            {
                f.AddCard();var input=f.MakeInput();
                foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
                {
                    var unit=f.MakeTarget(owner,1,0,false);
                    var building=new BuildingState("legal-building-"+owner,"ManaPond",owner,new TileCoord(1,1),false,0,60);
                    f.State.GetBoard(owner).Place(building.Position,building);f.Tile(owner,1,1).Present(building);
                    Assert.That(f.CanUse(input,f.Tile(owner,1,0)),Is.True);Assert.That(f.CanUse(input,f.Tile(owner,1,1)),Is.True);
                    Assert.That(f.CanUse(input,f.Tile(owner,2,1)),Is.False,"A Master is never a PlasmaShield recipient.");
                    Assert.That(f.CanUse(input,f.Tile(owner,4,0)),Is.False);
                    f.ClearHighlights();Invoke(input,"HighlightCardTargets",f.State);
                    Assert.That(f.Tile(owner,1,0).HighlightState,Is.EqualTo(BattleHighlightState.SpellTarget));
                    Assert.That(f.Tile(owner,1,1).HighlightState,Is.EqualTo(BattleHighlightState.SpellTarget));
                    Set(input,"_dragHoveredTileView",f.Tile(owner,1,1));Invoke(input,"HighlightDragHoverTarget");
                    Assert.That(f.AllTiles.Count(t=>t.HighlightState==BattleHighlightState.DragHoverTarget),Is.EqualTo(1));
                    Assert.That((bool)Invoke(input,"CanCardCastWithoutTarget",f.State,PlasmaShieldRules.CardId),Is.False);
                    unit.EnterSealbound(2);Assert.That(f.CanUse(input,f.Tile(owner,1,0)),Is.False);
                    f.State.GetBoard(owner).Remove(unit.Position);
                    var hidden=new UnitState("hidden-"+owner,"A-301",owner,unit.Position,AttackType.Ranged,1,100,true,false,0,hasHiding:true);
                    f.State.GetBoard(owner).Place(hidden.Position,hidden);f.Tile(owner,1,0).Present(hidden);
                    Assert.That(f.CanUse(input,f.Tile(owner,1,0)),Is.EqualTo(owner==PlayerId.Player));
                }
            }
        }

        [Test]
        public void TooltipUsesTheActualStackedDefenseSourceAndErasureRemovesTheRecord()
        {
            using(var f=new Fixture(false))
            {
                var target=f.MakeTarget(PlayerId.AI,1,0,false);
                target.ApplyCombatStatBuff(PlasmaShieldRules.CardId,physicalDefenseBonus:10);
                target.ApplyCombatStatBuff(PlasmaShieldRules.CardId,physicalDefenseBonus:23);
                var entries=OccupantStatusTooltipCatalog.BuildAttached(f.Provider.GetRequired("A-301"),target,f.State);
                Assert.That(entries.Select(e=>e.Title),Does.Contain("플라즈마 쉴드: 물리 방어력 +33"));
                Assert.That(entries.Any(e=>e.Title.Contains("공격력")||e.Title.Contains("피해")),Is.False);
                Assert.That(entries.Single(e=>e.Title.StartsWith("플라즈마 쉴드")).Description,Is.Empty);
                new ErasureService().Apply(f.State,target);
                Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null,target,f.State).Any(e=>e.Title.StartsWith("플라즈마 쉴드")),Is.False);
                target.ApplyCombatStatBuff(PlasmaShieldRules.CardId,physicalDefenseBonus:10);
                Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null,target,f.State).Select(e=>e.Title),Does.Contain("플라즈마 쉴드: 물리 방어력 +10"));
            }
        }

        [Test]
        public void OnlineSeatProjectionPreservesExactTargetIdentityWithoutInventingDamageOrRestorationCue()
        {
            using(var f=new Fixture(false))
            foreach(var localSeat in new[]{PlayerId.Player,PlayerId.AI})
            foreach(var remoteTarget in new[]{PlayerId.Player,PlayerId.AI})
            {
                var host=new GameObject("DormantProjection");host.SetActive(false);host.transform.SetParent(f.Root.transform);
                var connection=host.AddComponent<OnlineBattleConnectionTester>();Set(connection,"_localPlayerId",localSeat);
                var cast=Cast(PlayerId.Player,remoteTarget,1,0,"original-target");
                var projected=(IReadOnlyList<BattleEventDto>)Invoke(connection,"ProjectBattleEventsToLocalPerspective",(object)new[]{cast});
                var request=f.OnlineRequest(projected[0],projected);var targets=(IList)Property(request,"SpellEffectTargets");
                Assert.That(targets.Count,Is.EqualTo(1));Assert.That(Property(targets[0],"OwnerId"),Is.EqualTo(remoteTarget==localSeat?PlayerId.Player:PlayerId.AI));
                Assert.That(Property(request,"SpellTargetRuntimeId"),Is.EqualTo("original-target"));
                Assert.That(Property(request,"SpellCasterOwnerId"),Is.EqualTo(localSeat==PlayerId.Player?PlayerId.Player:PlayerId.AI));
                Assert.That(((IList)Property(request,"Impacts")).Count,Is.Zero);Assert.That(((IList)Property(request,"ValuePopupEvents")).Count,Is.Zero);
                Assert.That(f.Estimate(request),Is.GreaterThanOrEqualTo(PlasmaShieldVfx.DurationSeconds));
                var restored=f.OnlineRequest(null,Array.Empty<BattleEventDto>());
                Assert.That(Property(restored,"SpellEffectId"),Is.Null);Assert.That(f.Estimate(restored),Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator RealNativeUnitAndBuildingShapesUseOneScaleWithTheirGroundRingAnchoredAcrossRows()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
            foreach(var cardId in new[]{"Gwangma","BlueDragon","A-301","ManaPond"})
            {
                var tile=f.Tile(owner,1,0);
                if(tile.GetComponent<TileTextView>()==null)f.MakeTarget(owner,1,0);
                var occupant=cardId=="ManaPond"?(OccupantState)new BuildingState("shape-"+owner+cardId,cardId,owner,tile.Coord,false,0,100):
                    new UnitState("shape-"+owner+cardId,cardId,owner,tile.Coord,AttackType.Ranged,1,100,true,false,0);
                f.State.GetBoard(owner).Remove(tile.Coord);f.State.GetBoard(owner).Place(tile.Coord,occupant);tile.Present(occupant);
                var view=tile.GetComponent<TileTextView>();view.SetTile(tile.Title,tile.Content,true);view.PlayIdleAnimation();
                Invoke(view,"LateUpdate");yield return null;Invoke(view,"LateUpdate");
                var body=(Image)Get(view,"_occupantVisualImage");var animator=(Animator)Get(view,"_occupantVisualAnimator");
                if(animator!=null)animator.speed=0;
                Assert.That(f.Play(owner,1,0),Is.EqualTo(PlasmaShieldVfx.DurationSeconds));
                var baseline=f.Pixels();f.SampleFlight(3f/12f+.001f);var size=f.Image.rectTransform.sizeDelta;
                var bottom=IndependentBodyRect(body,f.Image.rectTransform.parent as RectTransform).min;
                var pivotWorld=f.Image.rectTransform.TransformPoint(Vector3.zero);
                var local=(Vector2)(f.Image.rectTransform.parent.InverseTransformPoint(pivotWorld));
                var expected=IndependentBodyRect(body,f.Image.rectTransform.parent as RectTransform);
                Assert.That(local.x,Is.EqualTo(expected.center.x).Within(1.5f));Assert.That(local.y,Is.EqualTo(bottom.y).Within(1.5f));
                using(var formed=f.Pixels("PlasmaShield-"+owner+"-"+cardId))
                    Assert.That(f.DifferentPixels(formed,baseline),Is.GreaterThan(100));
                f.SampleFlight(4f/12f+.001f);
                Assert.That(f.Image.rectTransform.sizeDelta,Is.EqualTo(size),"Frame 3 to 4 preserves the full-cell scale.");
                Assert.That(Vector3.Distance(pivotWorld,f.Image.rectTransform.TransformPoint(Vector3.zero)),Is.LessThan(.02f),"The row change never lifts the ground ring.");
                var anchors=(IReadOnlyList<Vector2>)typeof(PlasmaShieldVfx).GetProperty("GroundAnchors",Static).GetValue(null);
                Assert.That(anchors[4].y-anchors[3].y,Is.GreaterThan(.10f),"The source's 70px row offset is actually measured.");
                var overlay=f.Image.GetComponentInParent<Canvas>();Assert.That(overlay.overrideSorting,Is.True);Assert.That(overlay.sortingOrder,Is.EqualTo(4908));
                Assert.That(f.Image.raycastTarget,Is.False);Assert.That(f.Image.GetComponentInParent<CanvasGroup>().blocksRaycasts,Is.False);
                Assert.That(f.Effect.GetComponents<AudioSource>(),Is.Empty);
                f.SampleFlight(PlasmaShieldVfx.FormationDurationSeconds+.10f);
                Assert.That(f.Image.color.a,Is.InRange(.05f,.3f));
                f.SampleFlight(PlasmaShieldVfx.DurationSeconds+.001f);f.AssertHidden();baseline.Dispose();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LocalConfirmedBuffIsImmediateStackableAndItsOneShotNeverHurtsOrPersists()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                f.MakeTarget(PlayerId.Player,1,0);f.MakeTarget(PlayerId.AI,1,0);yield return null;
                foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
                {
                    var target=f.Tile(owner,1,0).CurrentOccupant;
                    foreach(var total in new[]{10,20})
                    {
                        f.AddCard();Assert.That(f.Bootstrapper.TryUsePlayerCardOnTile(PlasmaShieldRules.CardId,owner,new TileCoord(1,0),out _),Is.True);
                        var log = f.Bootstrapper.CombatLogText;
                        var targetOwner = owner == PlayerId.Player ? "플레이어의" : "상대방의";
                        var spellName = f.Provider.GetRequired(PlasmaShieldRules.CardId).DisplayName;
                        Assert.That(log, Does.Contain("플레이어가 " + targetOwner + " A-301(1,0)에게 " + spellName + " 마법카드를 사용했다."));
                        Assert.That(target.PhysicalDefense,Is.EqualTo(total));Assert.That(target.CurrentHp,Is.EqualTo(100));
                        Assert.That(f.State.ValuePopupEvents,Is.Empty);Assert.That(f.PopupCount,Is.Zero);
                        Assert.That(f.IsPlaying&&f.Bootstrapper.IsBusy,Is.True);
                        Assert.That(IsHurt(f.Tile(owner,1,0).GetComponent<TileTextView>()),Is.False);
                        var start=(float)Get(f.Effect,"_startedAt");
                        Assert.That(f.Bootstrapper.TryUsePlayerCardOnTile(PlasmaShieldRules.CardId,owner,new TileCoord(1,0),out _),Is.False);
                        Assert.That(target.PhysicalDefense,Is.EqualTo(total));Assert.That((float)Get(f.Effect,"_startedAt"),Is.EqualTo(start));
                        var timeout=Time.unscaledTime+2;while(f.Bootstrapper.IsBusy&&Time.unscaledTime<timeout)yield return null;
                        Assert.That(f.Bootstrapper.IsBusy,Is.False);f.AssertHidden();
                        Assert.That(f.State.PersistentEffects,Is.Empty);Assert.That(f.Effect.GetComponents<AudioSource>(),Is.Empty);
                        Assert.That(OccupantStatusTooltipCatalog.BuildAttached(null,target,f.State).Select(e=>e.Title),Does.Contain("플라즈마 쉴드: 물리 방어력 +"+total));
                    }
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator UnscaledLifecyclePreservesSameIdAndCancelsReplacementDeathDisableAndDestroy()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var target=f.MakeTarget(PlayerId.AI,1,0);var tile=f.Tile(PlayerId.AI,1,0);var view=tile.GetComponent<TileTextView>();yield return null;
                Time.timeScale=0;f.Play(PlayerId.AI,1,0);var start=(float)Get(f.Effect,"_startedAt");
                yield return new WaitForSecondsRealtime(.18f);Assert.That(f.Image.sprite.name,Is.Not.EqualTo("Formation_0"));
                var same=new UnitState(target.RuntimeId,"A-301",PlayerId.AI,tile.Coord,AttackType.Ranged,1,100,true,false,0);
                tile.Present(same);view.SetTile(tile.Title,tile.Content,true);Invoke(view,"LateUpdate");Assert.That((float)Get(f.Effect,"_startedAt"),Is.EqualTo(start));f.SampleFlight(.22f);
                Assert.That(f.IsPlaying,Is.True);
                // Sampling deliberately moves the test clock; identity continuity is asserted before it.
                Assert.That(Time.unscaledTime-(float)Get(f.Effect,"_startedAt"),Is.EqualTo(.22f).Within(.002f));
                Assert.That(f.Image.sprite.name,Is.EqualTo("Formation_2"));
                var replacement=new UnitState("replacement","A-301",PlayerId.AI,tile.Coord,AttackType.Ranged,1,100,true,false,0);
                tile.Present(replacement);view.SetTile(tile.Title,tile.Content,true);Invoke(view,"LateUpdate");f.SampleFlight(.24f);f.AssertHidden();
                Assert.That((float)Invoke(f.Effect,"Play",view,target.RuntimeId),Is.Zero);
                f.Play(PlayerId.AI,1,0);view.PlayDeathAnimation();f.SampleFlight(.1f);f.AssertHidden();
                view.ResetCombatPresentation();view.PlayIdleAnimation();Invoke(view,"LateUpdate");
                f.Play(PlayerId.AI,1,0);f.Effect.enabled=false;f.AssertHidden();f.Effect.enabled=true;
                f.Play(PlayerId.AI,1,0);tile.gameObject.SetActive(false);f.SampleFlight(.1f);f.AssertHidden();tile.gameObject.SetActive(true);
                view.SetTile(tile.Title,tile.Content,true);view.PlayIdleAnimation();Invoke(view,"LateUpdate");
                f.Play(PlayerId.AI,1,0);Object.Destroy(f.Effect);yield return null;
                Assert.That(f.Root.GetComponentsInChildren<Image>(true).Any(i=>i.name=="PlasmaShieldFormation"),Is.False);
                Time.timeScale=1;
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ConfirmedOnlineCuesHoldTheQueueButNeverMutateDefenseOrReplayOnSnapshot()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var own=f.MakeTarget(PlayerId.Player,1,0);var enemy=f.MakeTarget(PlayerId.AI,1,0);yield return null;
                var first=Cast(PlayerId.Player,PlayerId.Player,1,0,own.RuntimeId);
                var second=Cast(PlayerId.AI,PlayerId.AI,1,0,enemy.RuntimeId);
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(new[]{first,second}),Is.GreaterThanOrEqualTo(2*PlasmaShieldVfx.DurationSeconds));
                Assert.That(f.Bootstrapper.IsPlasmaShieldOnlinePresentationPending&&f.Bootstrapper.IsBusy,Is.True);
                var timeout=Time.unscaledTime+3;while((string)Get(f.Effect,"_runtimeId")!=enemy.RuntimeId&&Time.unscaledTime<timeout)yield return null;
                Assert.That((string)Get(f.Effect,"_runtimeId"),Is.EqualTo(enemy.RuntimeId));
                Assert.That(own.PhysicalDefense,Is.Zero);Assert.That(enemy.PhysicalDefense,Is.Zero,"Only authoritative snapshots/services grant defense.");
                var start=(float)Get(f.Effect,"_startedAt");
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()),Is.Zero);
                Assert.That((float)Get(f.Effect,"_startedAt"),Is.EqualTo(start));
                f.Bootstrapper.CancelCombatPresentation();yield return null;f.AssertHidden();
                Assert.That(f.Bootstrapper.IsPlasmaShieldOnlinePresentationPending,Is.False);Assert.That(f.PopupCount,Is.Zero);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AiConfirmedShieldUsesTheChosenRecipientWithoutDamagePresentation()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                f.State.RestoreRuntimeState(1,PlayerId.AI,PhaseType.Main);
                foreach(var card in f.State.AI.Hand.Cards.ToArray())f.State.AI.Hand.Remove(card.CardId,card.RuntimeId);
                f.State.Player.Resources.Spend(f.State.Player.Resources.Clone());f.State.AI.Resources.Spend(f.State.AI.Resources.Clone());
                f.State.AI.Hand.Add(PlasmaShieldRules.CardId);f.State.AI.Resources.Add(new ResourceSet(0,0,1,0));
                var recipient=f.MakeTarget(PlayerId.AI,0,0,attack:30);
                recipient.RemainingAttacksThisTurn=0;f.State.AI.Master.RemainingAttacksThisTurn=0;
                foreach(var column in new[]{1,3})
                {
                    var threat=new UnitState("incoming-physical-"+column,"Goblin",PlayerId.Player,new TileCoord(column,0),AttackType.Ranged,10,100,true,false,0,physicalDefense:3,magicDefense:4);
                    f.State.PlayerBoard.Place(threat.Position,threat);
                }
                yield return null;
                var ai=new AiDecisionService(f.Provider,new TargetingService(),ZeroCardUpgradeLevelProvider.Instance,
                    new AiSearchOptions{MaxDepth=1,BeamWidth=5,MaxSimulations=1200,MaxMilliseconds=1000});
                var command=ai.GetNextCommand(f.State) as CastScriptedSpellCommand;
                Assert.That(command,Is.Not.Null);Assert.That(command.CardId,Is.EqualTo(PlasmaShieldRules.CardId));
                Assert.That(command.TargetOwnerId,Is.EqualTo(PlayerId.AI));Assert.That(command.TargetCoord,Is.EqualTo(recipient.Position));
                Set(f.Bootstrapper,"_aiDecisionService",ai);
                var sequence=(IEnumerator)Invoke(f.Bootstrapper,"RunAiTurnSequence");Assert.That(sequence.MoveNext(),Is.True);
                var target=f.State.GetBoard(command.TargetOwnerId).GetOccupant(command.TargetCoord);
                Assert.That(target.PhysicalDefense,Is.EqualTo(10));Assert.That(target.CurrentHp,Is.EqualTo(100));
                f.Bootstrapper.StartCoroutine((IEnumerator)sequence.Current);
                var timeout=Time.unscaledTime+1;while(!f.IsPlaying&&Time.unscaledTime<timeout)yield return null;
                Assert.That(f.IsPlaying,Is.True);Assert.That((string)Get(f.Effect,"_runtimeId"),Is.EqualTo(target.RuntimeId));
                Assert.That(f.PopupCount,Is.Zero);Assert.That(f.State.ValuePopupEvents,Is.Empty);
                f.Bootstrapper.CancelCombatPresentation();f.Bootstrapper.StopAllCoroutines();f.AssertHidden();
            }
            yield return new ExitPlayMode();
        }

        private static object Invoke(object instance,string name,params object[] args)=>instance.GetType().GetMethod(name,Hidden).Invoke(instance,args);
        private static object Get(object instance,string name)=>instance.GetType().GetField(name,Hidden).GetValue(instance);
        private static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,Hidden).SetValue(instance,value);
        private static object Property(object instance,string name)=>instance.GetType().GetProperty(name).GetValue(instance);
        private static BattleEventDto Cast(PlayerId caster,PlayerId owner,int col,int row,string id)=>new BattleEventDto
        {EventType=BattleEventType.SpellCast,CardId=PlasmaShieldRules.CardId,SourceCardId=PlasmaShieldRules.CardId,
            SourceOwnerId=caster,TargetOwnerId=owner,TargetCoord=TileCoordDto.FromDomain(new TileCoord(col,row)),TargetRuntimeId=id};
        private static bool IsHurt(TileTextView view)
        {var animator=(Animator)Get(view,"_occupantVisualAnimator");return animator!=null&&animator.runtimeAnimatorController!=null&&animator.GetCurrentAnimatorStateInfo(0).IsName("BeAttacked");}
        private static Rect IndependentBodyRect(Image body,RectTransform overlay)
        {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(body.sprite))),Is.True);
                var rect=body.sprite.rect;var sx=(float)source.width/body.sprite.texture.width;var sy=(float)source.height/body.sprite.texture.height;
                var x0=Mathf.FloorToInt(rect.xMin*sx);var y0=Mathf.FloorToInt(rect.yMin*sy);
                var x1=Mathf.CeilToInt(rect.xMax*sx);var y1=Mathf.CeilToInt(rect.yMax*sy);var pixels=source.GetPixels32();
                var minX=x1;var minY=y1;var maxX=x0-1;var maxY=y0-1;
                for(var y=y0;y<y1;y++)for(var x=x0;x<x1;x++)if(pixels[y*source.width+x].a>=16)
                {minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
                Assert.That(maxX,Is.GreaterThanOrEqualTo(minX));
                var norm=Rect.MinMaxRect((float)(minX-x0)/(x1-x0),(float)(minY-y0)/(y1-y0),(float)(maxX+1-x0)/(x1-x0),(float)(maxY+1-y0)/(y1-y0));
                var draw=body.GetPixelAdjustedRect();var spriteSize=body.sprite.rect.size;
                if(body.preserveAspect){var size=spriteSize*Mathf.Min(draw.width/spriteSize.x,draw.height/spriteSize.y);draw=new Rect(draw.center-size*.5f,size);}
                var a=draw.min+Vector2.Scale(norm.min,draw.size);var b=draw.min+Vector2.Scale(norm.max,draw.size);
                var corners=new[]{new Vector2(a.x,a.y),new Vector2(a.x,b.y),new Vector2(b.x,b.y),new Vector2(b.x,a.y)};
                var minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var maximum=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                foreach(var corner in corners){var p=(Vector2)overlay.InverseTransformPoint(body.rectTransform.TransformPoint(corner));minimum=Vector2.Min(minimum,p);maximum=Vector2.Max(maximum,p);}
                return Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y);
            }
            finally{Object.DestroyImmediate(source);}
        }


        private sealed class CapturedPixels : IDisposable
        {
            internal readonly Texture2D Texture;
            internal int width=>Texture.width;
            internal CapturedPixels(Texture2D texture){Texture=texture;}
            public static implicit operator Texture2D(CapturedPixels value)=>value.Texture;

            public void Dispose()=>Object.DestroyImmediate(Texture);
        }
        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root=new GameObject("PlasmaShieldPresentationFixture");
            internal readonly PlasmaShieldVfx Effect;
            internal readonly BattleBootstrapper Bootstrapper;
            internal readonly LocalBattleGateway Gateway;
            internal readonly ICardDefinitionProvider Provider;
            internal readonly BoardPresenter Board;
            internal readonly RectTransform CanvasRect;
            private readonly Camera _camera;
            private readonly RenderTexture _render;
            private readonly string _oldToken;
            internal BattleState State=>Gateway.CurrentBattleState;
            internal IEnumerable<TileView> AllTiles=>Board.PlayerTileViews.Concat(Board.AITileViews);
            internal Image Image=>Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i=>i.name=="PlasmaShieldFormation");
            internal RectTransform Clip=>Image.rectTransform.parent as RectTransform;
            internal bool IsPlaying=>(bool)Get(Effect,"_playing");
            internal float Remaining=>(float)typeof(PlasmaShieldVfx).GetProperty("RemainingDurationSeconds",Hidden).GetValue(Effect);
            internal int PopupCount=>Root.GetComponentsInChildren<TMP_Text>(true).Count(t=>t.name=="ValuePopupText"&&t.gameObject.activeInHierarchy);
            internal Fixture(bool active=true)
            {
                var cameraHost=new GameObject("PreviewCamera",typeof(Camera));cameraHost.transform.SetParent(Root.transform);
                _camera=cameraHost.GetComponent<Camera>();_camera.transform.position=new Vector3(0,0,-10);_camera.orthographic=true;_camera.orthographicSize=5.4f;
                _camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.035f,.045f,.06f);
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {_render=new RenderTexture(2340,1080,24);_render.Create();_camera.targetTexture=_render;}
                var canvas=new GameObject("BattleCanvas",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();canvas.transform.SetParent(Root.transform,false);
                canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_camera;CanvasRect=(RectTransform)canvas.transform;CanvasRect.localScale=Vector3.one*.01f;
                var boardHost=new GameObject("Board");boardHost.SetActive(false);boardHost.transform.SetParent(Root.transform);
                Board=boardHost.AddComponent<BoardPresenter>();Set(Board,"_useResponsiveLayout",false);
                boardHost.SetActive(true);Set(Board,"_playerTileViews",MakeTiles(PlayerId.Player));Set(Board,"_aiTileViews",MakeTiles(PlayerId.AI));
                Effect=new GameObject("PlasmaShieldEffectHost").AddComponent<PlasmaShieldVfx>();Effect.transform.SetParent(Root.transform);
                var host=new GameObject("Coordinator");host.SetActive(false);host.transform.SetParent(Root.transform);
                Bootstrapper=host.AddComponent<BattleBootstrapper>();Set(Bootstrapper,"_startOnAwake",false);Set(Bootstrapper,"_autoRunAiTurns",false);
                Set(Bootstrapper,"_returnToDraftSceneWhenRunSessionActive",false);
                Set(Bootstrapper,"_cardCatalogAsset",AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset"));
                var screenHost=new GameObject("DormantScreen");screenHost.SetActive(false);screenHost.transform.SetParent(Root.transform);
                var screen=screenHost.AddComponent<BattleScreenPresenter>();Set(screen,"_boardPresenter",Board);Set(Bootstrapper,"_battleScreenPresenter",screen);Set(Bootstrapper,"_plasmaShieldVfx",Effect);
                Provider=JsonCardDefinitionDatabase.FromJson(File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();
                var flow=new BattleFlowController(new BattleSetupService(),new MulliganService(),new TurnStartService(),
                    new BattleCommandProcessor(new PlayCardService(Provider),new SpellService(Provider),new MoveService(),new AttackService(new TargetingService()),new EndTurnService()));
                Gateway=new LocalBattleGateway(flow);var deck=Enumerable.Repeat("Goblin",33).ToArray();Gateway.StartBattle(new BattleSetupRequest(deck,deck,PlayerId.Player));State.SetPhase(PhaseType.Main);
                Set(Bootstrapper,"_battleGateway",Gateway);Set(Bootstrapper,"_cardDefinitionProvider",Provider);_oldToken=AccountSessionState.SessionToken;
                if(active){typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null,"synthetic-fire-spear-presentation-session");host.SetActive(true);}
                Layout(new Vector2(2340,1080));
            }
            private TileView[] MakeTiles(PlayerId owner)
            {
                var tiles=new TileView[10];
                for(var i=0;i<tiles.Length;i++)
                {
                    var tile=new GameObject(owner+"Tile"+i,typeof(RectTransform),typeof(Image),typeof(TileView)).GetComponent<TileView>();
                    tile.transform.SetParent(CanvasRect,false);tile.Configure(owner,i%5,i/5);tile.GetComponent<Image>().color=new Color(.11f,.17f,.22f);tiles[i]=tile;
                }
                return tiles;
            }
            internal TileView Tile(PlayerId owner,int column,int row)=>AllTiles.Single(t=>t.OwnerId==owner&&t.Coord==new TileCoord(column,row));
            internal IEnumerable<TileView> Column(PlayerId owner,int column)=>AllTiles.Where(t=>t.OwnerId==owner&&t.Coord.Column==column).OrderBy(t=>t.Coord.Row);
            internal void Layout(Vector2 size)
            {
                CanvasRect.sizeDelta=size;var layout=BoardResponsiveLayoutCalculator.Calculate((int)size.x,(int)size.y,new Rect(0,0,size.x,size.y),.9f,24f,48f,new Vector2(8,8),1f,170f);
                foreach(var tile in AllTiles){var rect=(RectTransform)tile.transform;rect.sizeDelta=layout.TileSize;rect.anchoredPosition=layout.GetTileCenter(tile.OwnerId,tile.Coord);}
                Canvas.ForceUpdateCanvases();
            }
            internal UnitState MakeTarget(PlayerId owner,int column,int row,bool feedback=true,int attack=1)
            {
                var tile=Tile(owner,column,row);var unit=new UnitState("spear-"+owner+"-"+column+"-"+row,"A-301",owner,tile.Coord,AttackType.Ranged,attack,100,true,false,0);
                State.GetBoard(owner).Place(tile.Coord,unit);
                if(!feedback){tile.Present(unit);return unit;}
                tile.gameObject.SetActive(false);
                var background=new GameObject("TileBackground",typeof(RectTransform),typeof(Image)).GetComponent<Image>();background.transform.SetParent(tile.transform,false);
                background.raycastTarget=false;background.color=new Color(.11f,.17f,.22f);background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;
                background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
                var label=new GameObject("TileLabel",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(tile.transform,false);label.rectTransform.sizeDelta=((RectTransform)tile.transform).sizeDelta;
                var view=tile.gameObject.AddComponent<TileTextView>();Set(view,"_tileView",tile);Set(view,"_text",label);Set(view,"_battleBootstrapper",Bootstrapper);
                tile.Present(unit);tile.gameObject.SetActive(true);view.SetTile(tile.Title,tile.Content,true);view.PlayIdleAnimation();Invoke(view,"LateUpdate");return unit;
            }
            internal void AddCard(){State.Player.Hand.Add(PlasmaShieldRules.CardId);State.Player.Resources.Add(new ResourceSet(0,0,1,0));}
            internal BattlePlayerInputController MakeInput()
            {
                var host=new GameObject("DormantInput");host.SetActive(false);host.transform.SetParent(Root.transform);
                var input=host.AddComponent<BattlePlayerInputController>();Set(input,"_boardPresenter",Board);Set(input,"_battleBootstrapper",Bootstrapper);
                Set(input,"_selectedCardId",PlasmaShieldRules.CardId);Set(input,"_draggedCardId",PlasmaShieldRules.CardId);return input;
            }
            internal bool CanUse(BattlePlayerInputController input,TileView tile)=>(bool)Invoke(input,"CanCardUseOnTile",State,PlasmaShieldRules.CardId,tile);
            internal void ClearHighlights(){foreach(var tile in AllTiles)tile.SetHighlight(BattleHighlightState.None);}
            internal float Play(PlayerId owner,int column,int row){var tile=Tile(owner,column,row);return (float)Invoke(Effect,"Play",tile.GetComponent<TileTextView>(),tile.CurrentOccupantRuntimeId);}
            internal void SampleFlight(float age){Set(Effect,"_startedAt",Time.unscaledTime-age);Invoke(Effect,"LateUpdate");Canvas.ForceUpdateCanvases();}
            internal void Cancel()=>Invoke(Effect,"Cancel");
            internal object OnlineRequest(BattleEventDto source,IReadOnlyList<BattleEventDto> events)=>Invoke(Bootstrapper,"BuildOnlineImpactAnimationRequest",source?.CardId??source?.SourceCardId,source,events);
            internal float Estimate(object request)=>(float)Invoke(Bootstrapper,"EstimateImpactAnimationDuration",request);
            internal void AssertHidden(){Assert.That(Image==null||!Image.gameObject.activeInHierarchy,Is.True);Assert.That(IsPlaying,Is.False);}
            internal CapturedPixels Pixels(string name=null)
            {
                Assert.That(_render,Is.Not.Null,"Actual rendering requires the isolated Editor graphics device.");Canvas.ForceUpdateCanvases();_camera.Render();
                var old=RenderTexture.active;var texture=new Texture2D(_render.width,_render.height,TextureFormat.RGB24,false);
                try
                {
                    RenderTexture.active=_render;texture.ReadPixels(new Rect(0,0,_render.width,_render.height),0,0);texture.Apply();
                    if(name!=null){var path=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../Captures/PlasmaShield_20261001/"+name+".png"));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());}
                    return new CapturedPixels(texture);
                }
                finally{RenderTexture.active=old;}
            }
            internal int DifferentPixels(Texture2D image,Texture2D previous) {var a=image.GetPixels32();var b=previous.GetPixels32();return a.Where((pixel,index)=>!pixel.Equals(b[index])).Count();}
            public void Dispose()
            {
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null,_oldToken);Object.DestroyImmediate(Root);
                if(_render!=null){_render.Release();Object.DestroyImmediate(_render);}
            }
        }
    }
}
