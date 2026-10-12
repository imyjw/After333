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
    public sealed class FireSpearPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            Time.timeScale = 1f;
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [TestCase(PlayerId.Player, PlayerId.Player)] [TestCase(PlayerId.Player, PlayerId.AI)]
        [TestCase(PlayerId.AI, PlayerId.Player)] [TestCase(PlayerId.AI, PlayerId.AI)]
        public void IndependentTileCanvasesProjectTheSpearThroughBothSelectedCells(PlayerId caster, PlayerId owner)
        {
            using (var f = new Fixture(false))
            {
                f.SeparateTileCanvases();
                f.DetachOccupantVisualCanvases();
                foreach (var column in new[] { 0, 2, 4 })
                {
                    var roots = f.Column(owner, column).Select(tile => tile.GetComponentInParent<Canvas>().rootCanvas).ToArray();
                    Assert.That(roots[0], Is.Not.SameAs(roots[1]), "Production battle tiles have independent root canvases.");
                    Assert.That(f.Play(caster, owner, column), Is.EqualTo(FireSpearVfx.DurationSeconds),
                        "Distinct root canvases must not reject a confirmed cast.");
                    var anchorCanvas = f.Tile(owner, column, 0).GetComponent<TileTextView>()
                        .ActiveEffectButtonAnchor.GetComponentInParent<Canvas>().rootCanvas;
                    Assert.That(Get(f.Effect, "_canvas"), Is.SameAs(anchorCanvas),
                        "A small detached occupant-body Canvas is not the tile UI viewport.");
                    var initial = f.HeadWorld;
                    f.SampleFlight(FireSpearVfx.ImpactDelaySeconds * .5f);
                    var last = f.Tile(owner, column, owner == caster ? 0 : 1);
                    Assert.That(Vector3.Distance(f.HeadWorld, last.transform.position),
                        Is.LessThan(Vector3.Distance(initial, last.transform.position)));
                    f.SampleFlight(FireSpearVfx.ImpactDelaySeconds + .001f);
                    f.AssertGeometry(caster, owner, column);
                    var firstImpact = f.HeadWorld;
                    var translation = new Vector3(.25f, -.10f, 0f);
                    foreach (var tile in f.AllTiles) tile.transform.position += translation;
                    f.SampleFlight(FireSpearVfx.ImpactDelaySeconds + .002f);
                    Assert.That(Vector3.Distance(f.HeadWorld, firstImpact + translation), Is.LessThan(.002f),
                        "The flight follows live visual anchors rather than logical parent positions.");
                    f.AssertGeometry(caster, owner, column);
                    f.Cancel();
                }
            }
        }
        [TestCase(PlayerId.Player, PlayerId.Player)] [TestCase(PlayerId.Player, PlayerId.AI)]
        [TestCase(PlayerId.AI, PlayerId.Player)] [TestCase(PlayerId.AI, PlayerId.AI)]
        public void ActualTwoCellColumnHeadAndClipStayAlignedAcrossLayouts(PlayerId caster, PlayerId owner)
        {
            using (var f = new Fixture(false))
            {
                foreach (var size in new[] { new Vector2(2340,1080), new Vector2(1280,720), new Vector2(1080,2340) })
                foreach (var column in new[] {0,2,4})
                {
                    f.Layout(size);
                    Assert.That(f.Play(caster,owner,column),Is.EqualTo(FireSpearVfx.DurationSeconds));
                    var initial=f.HeadWorld; f.SampleFlight(FireSpearVfx.ImpactDelaySeconds * .5f); var halfway=f.HeadWorld;
                    var last=f.Tile(owner,column,owner==caster?0:1);
                    Assert.That(Vector3.Distance(halfway,last.transform.position),Is.LessThan(Vector3.Distance(initial,last.transform.position)));
                    f.SampleFlight(FireSpearVfx.ImpactDelaySeconds + .001f); f.AssertGeometry(caster,owner,column);
                    Assert.That(f.Image.sprite.name,Is.EqualTo("FlameSpear_3"));
                    Assert.That(f.Image.raycastTarget,Is.False);
                    Assert.That(f.Image.GetComponentInParent<CanvasGroup>().blocksRaycasts,Is.False);
                    Assert.That(f.Effect.GetComponents<AudioSource>(),Is.Empty);
                }
                f.Layout(new Vector2(2340,1080)); f.CanvasRect.localRotation=Quaternion.Euler(0,0,11);
                Assert.That(f.Play(caster,owner,2),Is.GreaterThan(0));f.SampleFlight(FireSpearVfx.ImpactDelaySeconds + .001f);f.AssertGeometry(caster,owner,2);
            }
        }

        [TestCase(PlayerId.Player, PlayerId.Player)] [TestCase(PlayerId.Player, PlayerId.AI)]
        [TestCase(PlayerId.AI, PlayerId.Player)] [TestCase(PlayerId.AI, PlayerId.AI)]
        public void ActualOnlineSeatProjectionKeepsCasterSelectedBoardAndTwoCells(PlayerId localSeat, PlayerId remoteTarget)
        {
            using(var f=new Fixture(false))
            {
                var host=new GameObject("DormantProjection");host.SetActive(false);host.transform.SetParent(f.Root.transform);
                var connection=host.AddComponent<OnlineBattleConnectionTester>();Set(connection,"_localPlayerId",localSeat);
                foreach(var row in new[]{0,1})
                {
                    var cast=Cast(PlayerId.Player,remoteTarget,3,row);
                    var wire=new BattleEventDto {EventType=BattleEventType.DamageApplied,SourceCardId=FireSpearRules.CardId,
                        SourceOwnerId=PlayerId.Player,TargetOwnerId=remoteTarget,TargetCoord=TileCoordDto.FromDomain(new TileCoord(3,0)),
                        RuntimeId="wire-victim",TargetRuntimeId="wire-victim",Amount=15,HpBefore=100,HpAfter=85,
                        DamageType=DamageType.Magic};
                    var projected=(IReadOnlyList<BattleEventDto>)Invoke(connection,"ProjectBattleEventsToLocalPerspective",(object)new[]{cast,wire});
                    var owner=remoteTarget==localSeat?PlayerId.Player:PlayerId.AI;
                    var caster=localSeat==PlayerId.Player?PlayerId.Player:PlayerId.AI;
                    var request=f.OnlineRequest(projected[0],projected);AssertTargets(request,owner,3);
                    Assert.That(Property(request,"SpellCasterOwnerId"),Is.EqualTo(caster));
                    Assert.That(Property(request,"GuardAreaRuntimeIds"),Is.True);
                    var impacts=(IList)Property(request,"Impacts");Assert.That(impacts.Count,Is.EqualTo(1));
                    Assert.That(((IDictionary)Property(request,"AreaImpactRuntimeIds"))[impacts[0]],Is.EqualTo("wire-victim"));
                    Assert.That(((IList)Property(request,"ValuePopupEvents")).Count,Is.EqualTo(1));
                    Assert.That(f.Estimate(request),Is.GreaterThanOrEqualTo(FireSpearVfx.DurationSeconds));
                    var immune=f.OnlineRequest(projected[0],new[]{projected[0]});
                    AssertTargets(immune,owner,3);Assert.That(((IList)Property(immune,"Impacts")).Count,Is.Zero,"A confirmed cast without damage never invents Hurt.");
                }
                var restored=f.OnlineRequest(null,Array.Empty<BattleEventDto>());
                Assert.That(Property(restored,"SpellEffectId"),Is.Null);Assert.That(f.Estimate(restored),Is.Zero);
                f.AssertHidden();
            }
        }

        [Test]
        public void LegalSelectionAndDragHoverUseOnlyTheSelectedOwnersColumn()
        {
            using(var f=new Fixture(false))
            {
                f.AddCard();
                var input=f.MakeInput();
                foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
                {
                    var victim=f.MakeTarget(owner,1,0,false);var anchor=f.Tile(owner,1,0);
                    Assert.That(f.CanUse(input,anchor),Is.True);
                    Assert.That(f.CanUse(input,f.Tile(owner,1,1)),Is.False,"An empty collateral cell is not a legal cast anchor.");
                    Assert.That((bool)Invoke(input,"CanCardCastWithoutTarget",f.State,FireSpearRules.CardId),Is.False);
                    f.ClearHighlights();Invoke(input,"HighlightCardTargets",f.State);
                    Assert.That(anchor.HighlightState,Is.EqualTo(BattleHighlightState.SpellTarget));
                    Set(input,"_dragHoveredTileView",anchor);Invoke(input,"HighlightDragHoverTarget");
                    var hovered=f.AllTiles.Where(t=>t.HighlightState==BattleHighlightState.DragHoverTarget).ToArray();
                    Assert.That(hovered.Length,Is.EqualTo(2));
                    Assert.That(hovered.All(t=>t.OwnerId==owner&&t.Coord.Column==1),Is.True);
                    victim.EnterSealbound(2);Assert.That(f.CanUse(input,anchor),Is.False);
                    f.State.GetBoard(owner).Remove(anchor.Coord);
                    var hiding=new UnitState("hiding-"+owner,"A-301",owner,anchor.Coord,AttackType.Ranged,1,100,true,false,0,hasHiding:true);
                    f.State.GetBoard(owner).Place(anchor.Coord,hiding);anchor.Present(hiding);
                    Assert.That(f.CanUse(input,anchor),Is.EqualTo(owner==PlayerId.Player),"Only friendly Hiding is a selectable anchor.");
                    f.State.GetBoard(owner).Remove(anchor.Coord);anchor.Present(null);
                }
            }
        }

        [Test]
        public void LateImpactKeepsACompleteTailAndCanvasRebindRestoresSorting()
        {
            using(var f=new Fixture(false))
            {
                f.Play(PlayerId.Player,PlayerId.AI,2);
                Set(f.Effect,"_endsAt",Time.unscaledTime - .65f + FireSpearVfx.DurationSeconds);
                f.SampleFlight(.65f);f.AssertGeometry(PlayerId.Player,PlayerId.AI,2);
                Assert.That(f.Remaining,Is.GreaterThan(FireSpearVfx.TailDurationSeconds - .01f),"A render hitch cannot consume the entire post-impact tail.");
                var image=f.Image;var head=f.HeadWorld;
                Assert.That((bool)Invoke(f.Effect,"TryBeginImpact"),Is.True);
                Assert.That(Vector3.Distance(head,f.HeadWorld),Is.LessThan(.0001f));
                var newCanvas=new GameObject("ReboundCanvas",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
                newCanvas.transform.SetParent(f.Root.transform,false);newCanvas.renderMode=RenderMode.WorldSpace;
                ((RectTransform)newCanvas.transform).sizeDelta=f.CanvasRect.sizeDelta;
                newCanvas.transform.localScale=f.CanvasRect.localScale;
                foreach(var tile in f.AllTiles)tile.transform.SetParent(newCanvas.transform,false);
                Assert.That(f.Play(PlayerId.AI,PlayerId.Player,3),Is.GreaterThan(0));
                Assert.That(f.Image,Is.SameAs(image),"The visual Image is reused.");
                var overlay=f.Image.GetComponentInParent<Canvas>();
                Assert.That(overlay.overrideSorting,Is.True);Assert.That(overlay.sortingOrder,Is.EqualTo(4908));
                Assert.That(overlay.rootCanvas,Is.SameAs(newCanvas));
                f.Cancel();f.AssertHidden();
            }
        }

        [UnityTest]
        public IEnumerator ConfirmedGatewayDamageIsHeldUntilTheSharedImpactBoundary()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                f.MakeTarget(PlayerId.AI,1,0);f.MakeTarget(PlayerId.AI,1,1);
                var opposite=f.MakeTarget(PlayerId.Player,1,0);var neighbor=f.MakeTarget(PlayerId.AI,2,0);
                yield return null;f.AddCard();
                var before=Capture(f.State);
                f.Gateway.ExecuteCommand(PlayerId.Player,new CastScriptedSpellCommand(FireSpearRules.CardId,PlayerId.AI,new TileCoord(1,1)));
                Assert.That(f.Column(PlayerId.AI,1).All(t=>t.CurrentOccupant.CurrentHp==85),Is.True);
                Assert.That(opposite.CurrentHp,Is.EqualTo(100));Assert.That(neighbor.CurrentHp,Is.EqualTo(100));
                Assert.That(f.State.Player.Resources.Mana,Is.EqualTo(0));Assert.That(f.State.Player.Hand.Contains(FireSpearRules.CardId),Is.False);
                var request=Build(before,Capture(f.State),f.State.ValuePopupEvents.ToArray(),PlayerId.Player);
                Configure(request,PlayerId.AI,new TileCoord(1,1));AssertTargets(request,PlayerId.AI,1);
                var sequence=(IEnumerator)Invoke(f.Bootstrapper,"PlayImpactAnimationSequence",request);
                Assert.That(sequence.MoveNext(),Is.True);var spell=(IEnumerator)sequence.Current;
                Assert.That(spell.MoveNext(),Is.True);Assert.That(spell.Current,Is.TypeOf<WaitForSecondsRealtime>());
                f.SampleFlight(FireSpearVfx.ImpactDelaySeconds * .5f);Assert.That(f.PopupCount,Is.Zero);
                foreach(var tile in f.Column(PlayerId.AI,1)){tile.GetComponent<TileTextView>().Refresh();AssertDisplayedHp(tile,100);Assert.That(IsHurt(tile.GetComponent<TileTextView>()),Is.False);}
                f.SampleFlight(FireSpearVfx.ImpactDelaySeconds + .001f);Assert.That(spell.MoveNext(),Is.False);Assert.That(sequence.MoveNext(),Is.True);
                foreach(var tile in f.Column(PlayerId.AI,1)){AssertDisplayedHp(tile,85);Assert.That(IsHurt(tile.GetComponent<TileTextView>()),Is.True);}
                Assert.That((int)typeof(TileTextView).GetField("_lastCommonHitFrame",Static).GetValue(null),Is.EqualTo(Time.frameCount));
                yield return null;Assert.That(f.PopupCount,Is.EqualTo(2),"Both confirmed victims receive simultaneous damage popups.");
                Assert.That(f.Effect.GetComponents<AudioSource>(),Is.Empty,"The spear adds no duplicate impact source.");
                f.Bootstrapper.CancelCombatPresentation();f.AssertHidden();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActualLocalOwnCastStartsOnceAndRejectsBusyOrMissingHandReplay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                f.MakeTarget(PlayerId.Player,1,0);f.MakeTarget(PlayerId.Player,1,1);
                var enemy=f.MakeTarget(PlayerId.AI,1,0);yield return null;f.AddCard();
                Assert.That(f.Bootstrapper.TryUsePlayerCardOnTile(FireSpearRules.CardId,PlayerId.Player,new TileCoord(1,0),out _),Is.True);
                Assert.That(f.IsPlaying,Is.True);Assert.That(f.Column(PlayerId.Player,1).All(t=>t.CurrentOccupant.CurrentHp==85),Is.True);
                Assert.That(enemy.CurrentHp,Is.EqualTo(100));
                var start=(float)Get(f.Effect,"_startedAt");
                Assert.That(f.Bootstrapper.TryUsePlayerCardOnTile(FireSpearRules.CardId,PlayerId.AI,new TileCoord(1,0),out _),Is.False);
                Assert.That((float)Get(f.Effect,"_startedAt"),Is.EqualTo(start));Assert.That(enemy.CurrentHp,Is.EqualTo(100));
                var timeout=Time.unscaledTime+3f;while(f.Bootstrapper.IsBusy&&Time.unscaledTime<timeout)yield return null;
                Assert.That(f.Bootstrapper.IsBusy,Is.False);f.AssertHidden();
                LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("Play card interaction failed.*"));
                Assert.That(f.Bootstrapper.TryUsePlayerCardOnTile(FireSpearRules.CardId,PlayerId.Player,new TileCoord(1,0),out _),Is.False);
                Assert.That(f.Column(PlayerId.Player,1).All(t=>t.CurrentOccupant.CurrentHp==85),Is.True);f.AssertHidden();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeIdReplacementAndCancellationDoNotApplyOldFeedback()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                f.MakeTarget(PlayerId.AI,1,0);f.MakeTarget(PlayerId.AI,1,1);yield return null;f.AddCard();
                var before=Capture(f.State);f.Gateway.ExecuteCommand(PlayerId.Player,new CastScriptedSpellCommand(FireSpearRules.CardId,PlayerId.AI,new TileCoord(1,0)));
                var request=Build(before,Capture(f.State),f.State.ValuePopupEvents.ToArray(),PlayerId.Player);Configure(request,PlayerId.AI,new TileCoord(1,0));
                var sequence=(IEnumerator)Invoke(f.Bootstrapper,"PlayImpactAnimationSequence",request);
                Assert.That(sequence.MoveNext(),Is.True);var spell=(IEnumerator)sequence.Current;Assert.That(spell.MoveNext(),Is.True);
                var tile=f.Tile(PlayerId.AI,1,0);var replacement=new UnitState("new-speared-tile","A-301",PlayerId.AI,tile.Coord,AttackType.Ranged,1,100,true,false,0);
                tile.Present(replacement);var view=tile.GetComponent<TileTextView>();view.SetTile(tile.Title,tile.Content,true);view.PlayIdleAnimation();
                Invoke(view,"LateUpdate");Assert.That(view.VisualRuntimeId,Is.EqualTo(replacement.RuntimeId));
                f.SampleFlight(FireSpearVfx.ImpactDelaySeconds + .001f);Assert.That(spell.MoveNext(),Is.False);Assert.That(sequence.MoveNext(),Is.True);
                Assert.That(IsHurt(view),Is.False);AssertDisplayedHp(tile,100);Assert.That(replacement.CurrentHp,Is.EqualTo(100));
                yield return null;Assert.That(f.PopupCount,Is.EqualTo(1));
                f.Bootstrapper.CancelCombatPresentation();f.AssertHidden();
                var next=(IEnumerator)Invoke(f.Bootstrapper,"PlayImpactAnimationSequence",request);
                Assert.That(next.MoveNext(),Is.True);var nextSpell=(IEnumerator)next.Current;Assert.That(nextSpell.MoveNext(),Is.True);
                f.Bootstrapper.CancelCombatPresentation();f.Play(PlayerId.AI,PlayerId.Player,2);f.SampleFlight(.1f);
                Assert.That(nextSpell.MoveNext(),Is.False);Assert.That(next.MoveNext(),Is.False);
                Assert.That(f.IsPlaying,Is.True,"An old canceled command does not cancel the reused visual.");
                f.Cancel();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator OnlineGroupsRemainBusyThroughTheirTailsAndCleanupWhenDisabled()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var events=new[]{Cast(PlayerId.Player,PlayerId.Player,1,0),Cast(PlayerId.AI,PlayerId.AI,3,1)};
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(events),Is.GreaterThanOrEqualTo(2*FireSpearVfx.DurationSeconds));
                Assert.That(f.Bootstrapper.IsFireSpearOnlinePresentationPending&&f.Bootstrapper.IsBusy,Is.True);
                var timeout=Time.unscaledTime+4f;
                while((int)Get(f.Effect,"_column")!=3&&Time.unscaledTime<timeout)yield return null;
                Assert.That((int)Get(f.Effect,"_column"),Is.EqualTo(3));Assert.That(f.Bootstrapper.IsBusy,Is.True);
                f.Bootstrapper.CancelCombatPresentation();f.AssertHidden();
                yield return new WaitForSecondsRealtime(.85f);
                Assert.That(f.Bootstrapper.IsFireSpearOnlinePresentationPending,Is.False);f.AssertHidden();
                f.Play(PlayerId.Player,PlayerId.AI,1);f.Effect.enabled=false;f.AssertHidden();f.Effect.enabled=true;
                f.Play(PlayerId.Player,PlayerId.AI,1);f.Tile(PlayerId.AI,1,1).gameObject.SetActive(false);f.SampleFlight(.1f);f.AssertHidden();
                f.Tile(PlayerId.AI,1,1).gameObject.SetActive(true);
                Assert.That(f.Bootstrapper.PlayOnlineBattleEventAnimations(Array.Empty<BattleEventDto>()),Is.Zero);f.AssertHidden();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AiConfirmedCommandUsesItsSelectedColumnAndMagicDamage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                f.State.RestoreRuntimeState(1,PlayerId.AI,PhaseType.Main);
                foreach(var card in f.State.AI.Hand.Cards.ToArray())f.State.AI.Hand.Remove(card.CardId,card.RuntimeId);
                f.State.AI.Hand.Add(FireSpearRules.CardId);f.State.AI.Resources.Add(new ResourceSet(3,0,0,0));
                f.MakeTarget(PlayerId.Player,1,0);f.MakeTarget(PlayerId.Player,1,1);yield return null;
                var ai=new AiDecisionService(f.Provider,new TargetingService(),ZeroCardUpgradeLevelProvider.Instance,
                    new AiSearchOptions{MaxDepth=1,BeamWidth=5,MaxSimulations=1200,MaxMilliseconds=500});
                var command=ai.GetNextCommand(f.State) as CastScriptedSpellCommand;
                Assert.That(command,Is.Not.Null);Assert.That(command.CardId,Is.EqualTo(FireSpearRules.CardId));
                var original=f.State.GetBoard(command.TargetOwnerId).GetOccupant(command.TargetCoord).CurrentHp;
                Set(f.Bootstrapper,"_aiDecisionService",ai);
                var sequence=(IEnumerator)Invoke(f.Bootstrapper,"RunAiTurnSequence");
                Assert.That(sequence.MoveNext(),Is.True);
                Assert.That(f.State.GetBoard(command.TargetOwnerId).GetOccupant(command.TargetCoord).CurrentHp,Is.EqualTo(original-15));
                f.Bootstrapper.StartCoroutine((IEnumerator)sequence.Current);
                var timeout=Time.unscaledTime+1f;while(!f.IsPlaying&&Time.unscaledTime<timeout)yield return null;
                Assert.That(f.IsPlaying,Is.True);Assert.That((PlayerId)Get(f.Effect,"_caster"),Is.EqualTo(PlayerId.AI));
                Assert.That((PlayerId)Get(f.Effect,"_targetOwner"),Is.EqualTo(command.TargetOwnerId));
                Assert.That((int)Get(f.Effect,"_column"),Is.EqualTo(command.TargetCoord.Column));
                f.Bootstrapper.CancelCombatPresentation();f.Bootstrapper.StopAllCoroutines();f.AssertHidden();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActualPixelsStayInsideTwoSelectedCellsBothOwnersWithLegalWhitePulse()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})foreach(var row in new[]{0,1})
                    f.MakeTarget(owner,1,row);
                f.AddCard();yield return null;
                var input=f.MakeInput();
                f.ClearHighlights();Invoke(input,"HighlightCardTargets",f.State);
                foreach(var tile in f.AllTiles.Where(t=>t.GetComponent<TileTextView>()!=null))
                {
                    tile.GetComponent<TileTextView>().Refresh();
                    var pulse=tile.GetComponentsInChildren<TileTargetPulse>(true).Single();
                    pulse.Refresh(tile.transform.Find("TileBackground").GetComponent<Image>(),true,.6f);
                    var image=pulse.GetComponent<Image>();Assert.That(image.enabled,Is.True);Assert.That(image.color,Is.EqualTo(new Color(1,1,1,.65f)));
                    Assert.That(image.raycastTarget,Is.False);
                }
                using(var capture=f.Pixels("FireSpear-LegalWhitePulse")) { Assert.That(capture.width,Is.EqualTo(2340)); }
                foreach(var caster in new[]{PlayerId.Player,PlayerId.AI})
                foreach(var owner in new[]{PlayerId.Player,PlayerId.AI})
                {
                    f.ClearHighlights();Invoke(input,"HighlightCardTargets",f.State);
                    Set(input,"_dragHoveredTileView",f.Tile(owner,1,0));Invoke(input,"HighlightDragHoverTarget");
                    foreach(var tile in f.AllTiles.Where(t=>t.GetComponent<TileTextView>()!=null))tile.GetComponent<TileTextView>().Refresh();
                    using(var hover=f.Pixels("FireSpear-Hover-"+owner)) { Assert.That(hover.width,Is.EqualTo(2340)); }
                    var baseline=f.Pixels();
                    try
                    {
                        f.Play(caster,owner,1);f.SampleFlight(FireSpearVfx.ImpactDelaySeconds * (2f / 3f));
                        using(var flight=f.Pixels("FireSpear-Flight-"+caster+"-"+owner))f.AssertChangedInsideColumn(flight,baseline,owner,1);
                        f.SampleFlight(FireSpearVfx.ImpactDelaySeconds + .001f);f.AssertGeometry(caster,owner,1);
                        using(var impact=f.Pixels("FireSpear-Impact-"+caster+"-"+owner))f.AssertChangedInsideColumn(impact,baseline,owner,1);
                        f.Cancel();
                    }
                    finally{baseline.Dispose();}
                }
                var oldScale=Time.timeScale;Time.timeScale=0f;
                try
                {
                    f.Play(PlayerId.Player,PlayerId.AI,1);var head=f.HeadWorld;yield return new WaitForSecondsRealtime(.13f);
                    Assert.That(Vector3.Distance(head,f.HeadWorld),Is.GreaterThan(.05f));
                    Assert.That(f.Image.sprite.name,Is.Not.EqualTo("FlameSpear_0"));
                    yield return new WaitForSecondsRealtime(.75f);f.AssertHidden();
                }
                finally{Time.timeScale=oldScale;}
            }
            yield return new ExitPlayMode();
        }

        private static object Invoke(object instance,string name,params object[] args)=>instance.GetType().GetMethod(name,Hidden).Invoke(instance,args);
        private static object Get(object instance,string name)=>instance.GetType().GetField(name,Hidden).GetValue(instance);
        private static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,Hidden).SetValue(instance,value);
        private static object Property(object instance,string name)=>instance.GetType().GetProperty(name).GetValue(instance);
        private static object Capture(BattleState state)=>typeof(BattleBootstrapper).GetMethod("CaptureBattleSnapshot",Static).Invoke(null,new object[]{state});
        private static object Build(object before,object after,IReadOnlyList<BattleValuePopupEvent> events,PlayerId caster)=>typeof(BattleBootstrapper).GetMethod("BuildImpactAnimationRequest",Static).Invoke(null,new object[]{before,after,events,FireSpearRules.CardId,caster});
        private static void Configure(object request,PlayerId owner,TileCoord coord)=>typeof(BattleBootstrapper).GetMethod("ConfigureFireSpearCast",Static).Invoke(null,new object[]{request,owner,coord});
        private static BattleEventDto Cast(PlayerId caster,PlayerId owner,int column,int row)=>new BattleEventDto
        {EventType=BattleEventType.SpellCast,CardId=FireSpearRules.CardId,SourceCardId=FireSpearRules.CardId,SourceOwnerId=caster,TargetOwnerId=owner,TargetCoord=TileCoordDto.FromDomain(new TileCoord(column,row))};
        private static void AssertTargets(object request,PlayerId owner,int column)
        {
            Assert.That(Property(request,"SpellEffectId"),Is.EqualTo(FireSpearRules.EffectId));
            var targets=((IList)Property(request,"SpellEffectTargets")).Cast<object>().ToArray();
            Assert.That(targets.Length,Is.EqualTo(2));Assert.That(targets.All(t=>(PlayerId)Property(t,"OwnerId")==owner),Is.True);
            Assert.That(targets.Select(t=>(TileCoord)Property(t,"Coord")),Is.EqualTo(new[]{new TileCoord(column,0),new TileCoord(column,1)}));
        }
        private static bool IsHurt(TileTextView view)
        {var animator=(Animator)Get(view,"_occupantVisualAnimator");return animator!=null&&animator.runtimeAnimatorController!=null&&animator.GetCurrentAnimatorStateInfo(0).IsName("BeAttacked");}
        private static void AssertDisplayedHp(TileView tile,int hp)
        {var text=(TMP_Text)Get(tile.GetComponent<TileTextView>(),"_occupantStatHpText");Assert.That(text,Is.Not.Null);Assert.That(text.text,Is.EqualTo(hp.ToString()));}

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
            internal readonly GameObject Root=new GameObject("FireSpearPresentationFixture");
            internal readonly FireSpearVfx Effect;
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
            internal Image Image=>Root.GetComponentsInChildren<Image>(true).SingleOrDefault(i=>i.name=="FireSpearProjectile");
            internal RectTransform Clip=>Image.rectTransform.parent as RectTransform;
            internal bool IsPlaying=>(bool)Get(Effect,"_playing");
            internal float Remaining=>(float)typeof(FireSpearVfx).GetProperty("RemainingDurationSeconds",Hidden).GetValue(Effect);
            internal int PopupCount=>Root.GetComponentsInChildren<TMP_Text>(true).Count(t=>t.name=="ValuePopupText"&&t.gameObject.activeInHierarchy);
            internal Vector3 HeadWorld
            {
                get {var p=(Vector2)typeof(FireSpearVfx).GetProperty("HeadNormalized",Static).GetValue(null);var r=Image.rectTransform.rect;
                    return Image.rectTransform.TransformPoint(new Vector3(r.xMin+r.width*p.x,r.yMin+r.height*p.y));}
            }
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
                Effect=new GameObject("FireSpearEffectHost").AddComponent<FireSpearVfx>();Effect.transform.SetParent(Root.transform);
                var host=new GameObject("Coordinator");host.SetActive(false);host.transform.SetParent(Root.transform);
                Bootstrapper=host.AddComponent<BattleBootstrapper>();Set(Bootstrapper,"_startOnAwake",false);Set(Bootstrapper,"_autoRunAiTurns",false);
                Set(Bootstrapper,"_returnToDraftSceneWhenRunSessionActive",false);
                Set(Bootstrapper,"_cardCatalogAsset",AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset"));
                var screenHost=new GameObject("DormantScreen");screenHost.SetActive(false);screenHost.transform.SetParent(Root.transform);
                var screen=screenHost.AddComponent<BattleScreenPresenter>();Set(screen,"_boardPresenter",Board);Set(Bootstrapper,"_battleScreenPresenter",screen);Set(Bootstrapper,"_fireSpearVfx",Effect);
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
            internal void SeparateTileCanvases()
            {
                var index = 0;
                foreach (var tile in AllTiles)
                {
                    var canvas = new GameObject("IndependentTileCanvas" + index, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                    canvas.transform.SetParent(Root.transform, false);
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = _camera;
                    var rect = (RectTransform)canvas.transform;
                    rect.sizeDelta = CanvasRect.sizeDelta;
                    rect.localScale = Vector3.one * (.008f + .0003f * index);
                    rect.localPosition = new Vector3(.03f * index, -.01f * index, 0f);
                    tile.transform.SetParent(rect, true);
                    index++;
                }
                Canvas.ForceUpdateCanvases();
            }
            internal void DetachOccupantVisualCanvases()
            {
                foreach (var tile in AllTiles)
                {
                    tile.gameObject.SetActive(false);
                    var background = new GameObject("TileBackground", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    background.SetParent(tile.transform, false);
                    background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
                    background.offsetMin = background.offsetMax = Vector2.zero;
                    var label = new GameObject("TileLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    label.transform.SetParent(tile.transform, false);
                    label.rectTransform.sizeDelta = ((RectTransform)tile.transform).sizeDelta;
                    var view = tile.gameObject.AddComponent<TileTextView>();
                    Set(view, "_tileView", tile); Set(view, "_text", label); Set(view, "_battleBootstrapper", Bootstrapper);
                    tile.gameObject.SetActive(true);
                    var bodyCanvas = new GameObject("DetachedOccupantCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                    bodyCanvas.transform.SetParent(Root.transform, false);
                    bodyCanvas.renderMode = RenderMode.WorldSpace; bodyCanvas.worldCamera = _camera;
                    ((RectTransform)bodyCanvas.transform).sizeDelta = new Vector2(129.6f, 129.6f);
                    bodyCanvas.transform.localScale = Vector3.one * .01f;
                    var body = new GameObject("DetachedOccupantBody", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    body.transform.SetParent(bodyCanvas.transform, false); body.enabled = false;
                    Set(view, "_occupantVisualImage", body);
                    Assert.That((Canvas)Invoke(view, "GetVisualRootCanvas"), Is.SameAs(bodyCanvas));
                    Assert.That(view.ActiveEffectButtonAnchor.GetComponentInParent<Canvas>().rootCanvas, Is.Not.SameAs(bodyCanvas));
                }
                Canvas.ForceUpdateCanvases();
            }
            internal TileView Tile(PlayerId owner,int column,int row)=>AllTiles.Single(t=>t.OwnerId==owner&&t.Coord==new TileCoord(column,row));
            internal IEnumerable<TileView> Column(PlayerId owner,int column)=>AllTiles.Where(t=>t.OwnerId==owner&&t.Coord.Column==column).OrderBy(t=>t.Coord.Row);
            internal void Layout(Vector2 size)
            {
                CanvasRect.sizeDelta=size;var layout=BoardResponsiveLayoutCalculator.Calculate((int)size.x,(int)size.y,new Rect(0,0,size.x,size.y),.9f,24f,48f,new Vector2(8,8),1f,170f);
                foreach(var tile in AllTiles){var rect=(RectTransform)tile.transform;rect.sizeDelta=layout.TileSize;rect.anchoredPosition=layout.GetTileCenter(tile.OwnerId,tile.Coord);}
                Canvas.ForceUpdateCanvases();
            }
            internal UnitState MakeTarget(PlayerId owner,int column,int row,bool feedback=true)
            {
                var tile=Tile(owner,column,row);var unit=new UnitState("spear-"+owner+"-"+column+"-"+row,"A-301",owner,tile.Coord,AttackType.Ranged,1,100,true,false,0);
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
            internal void AddCard(){State.Player.Hand.Add(FireSpearRules.CardId);State.Player.Resources.Add(new ResourceSet(3,0,0,0));}
            internal BattlePlayerInputController MakeInput()
            {
                var host=new GameObject("DormantInput");host.SetActive(false);host.transform.SetParent(Root.transform);
                var input=host.AddComponent<BattlePlayerInputController>();Set(input,"_boardPresenter",Board);Set(input,"_battleBootstrapper",Bootstrapper);
                Set(input,"_selectedCardId",FireSpearRules.CardId);Set(input,"_draggedCardId",FireSpearRules.CardId);return input;
            }
            internal bool CanUse(BattlePlayerInputController input,TileView tile)=>(bool)Invoke(input,"CanCardUseOnTile",State,FireSpearRules.CardId,tile);
            internal void ClearHighlights(){foreach(var tile in AllTiles)tile.SetHighlight(BattleHighlightState.None);}
            internal float Play(PlayerId caster,PlayerId owner,int column)=>(float)Invoke(Effect,"Play",Board,caster,owner,column);
            internal void SampleFlight(float age){Set(Effect,"_startedAt",Time.unscaledTime-age);Invoke(Effect,"LateUpdate");Canvas.ForceUpdateCanvases();}
            internal void Cancel()=>Invoke(Effect,"Cancel");
            internal object OnlineRequest(BattleEventDto source,IReadOnlyList<BattleEventDto> events)=>Invoke(Bootstrapper,"BuildOnlineImpactAnimationRequest",source?.CardId??source?.SourceCardId,source,events);
            internal float Estimate(object request)=>(float)Invoke(Bootstrapper,"EstimateImpactAnimationDuration",request);
            internal void AssertHidden(){Assert.That(Image==null||!Image.gameObject.activeInHierarchy,Is.True);Assert.That(IsPlaying,Is.False);}
            internal void AssertGeometry(PlayerId caster,PlayerId owner,int column)
            {
                var last=Tile(owner,column,owner==caster?0:1);var anchor=last.GetComponent<TileTextView>()?.ActiveEffectButtonAnchor??last.transform as RectTransform;
                Assert.That(Vector3.Distance(HeadWorld,anchor.TransformPoint(anchor.rect.center)),Is.LessThan(.002f),"The white-hot spear head reaches the far cell center.");
                foreach(var tile in Column(owner,column))
                {
                    var p=Clip.InverseTransformPoint(tile.transform.position);Assert.That(Mathf.Abs(p.y),Is.LessThan(.01f));
                    Assert.That(p.x,Is.InRange(Clip.rect.xMin,Clip.rect.xMax));
                }
                foreach(var tile in AllTiles.Where(t=>t.OwnerId!=owner||t.Coord.Column!=column))
                {var p=Clip.InverseTransformPoint(tile.transform.position);Assert.That(Clip.rect.Contains(p),Is.False,"No other column/board center enters the clip.");}
                var maskRect = Clip.GetComponent<RectMask2D>().canvasRect;
                Assert.That(maskRect.width, Is.GreaterThan(0f), "Leftward flight keeps a valid positive Canvas mask rectangle.");
                Assert.That(maskRect.height, Is.GreaterThan(0f), "Turning the image never inverts the mask corners.");
                var overlay=Image.GetComponentInParent<Canvas>();Assert.That(overlay.overrideSorting,Is.True);Assert.That(overlay.sortingOrder,Is.EqualTo(4908));
            }
            internal CapturedPixels Pixels(string name=null)
            {
                Assert.That(_render,Is.Not.Null,"Actual rendering requires the isolated Editor graphics device.");Canvas.ForceUpdateCanvases();_camera.Render();
                var old=RenderTexture.active;var texture=new Texture2D(_render.width,_render.height,TextureFormat.RGB24,false);
                try
                {
                    RenderTexture.active=_render;texture.ReadPixels(new Rect(0,0,_render.width,_render.height),0,0);texture.Apply();
                    if(name!=null){var path=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../Captures/FireSpearPresentation_20261001/"+name+".png"));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());}
                    return new CapturedPixels(texture);
                }
                finally{RenderTexture.active=old;}
            }
            internal void AssertChangedInsideColumn(Texture2D image,Texture2D baseline,PlayerId owner,int column)
            {
                var current=image.GetPixels32();var previous=baseline.GetPixels32();var changed=0;var outside=0;
                for(var y=0;y<image.height;y++)for(var x=0;x<image.width;x++)
                {
                    var index=y*image.width+x;if(current[index].Equals(previous[index]))continue;
                    var ray=_camera.ViewportPointToRay(new Vector3((x+.5f)/image.width,(y+.5f)/image.height));
                    var plane=new Plane(Clip.forward,Clip.position);if(!plane.Raycast(ray,out var distance))continue;
                    var point=Clip.InverseTransformPoint(ray.GetPoint(distance));
                    if(point.x<Clip.rect.xMin-2f||point.x>Clip.rect.xMax+2f||point.y<Clip.rect.yMin-2f||point.y>Clip.rect.yMax+2f)outside++;
                    else changed++;
                }
                Assert.That(changed,Is.GreaterThan(80),"Actual fiery pixels appear in "+owner+" column "+column+".");
                Assert.That(outside,Is.Zero,"Every changed pixel stays inside the selected two-cell mask, with only two pixels of edge tolerance.");
                Assert.That(Image.GetComponent<CanvasRenderer>().GetMesh().vertexCount,Is.GreaterThan(0));
            }
            public void Dispose()
            {
                typeof(AccountSessionState).GetProperty("SessionToken").SetValue(null,_oldToken);Object.DestroyImmediate(Root);
                if(_render!=null){_render.Release();Object.DestroyImmediate(_render);}
            }
        }
    }
}

