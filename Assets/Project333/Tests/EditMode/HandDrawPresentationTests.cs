using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.Online;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Application.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class HandDrawPresentationTests
    {
        [UnityTearDown] public IEnumerator Exit() { Time.timeScale=1; if(Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator BatchDrawLeavesDeckBackFlipsAndArrivesInInteractiveHand()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                var state=f.State(); f.Hand.Present(state); yield return null;
                var back=f.Deck.DrawSourceRect.GetComponent<Image>(); back.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                back.rectTransform.anchoredPosition+=new Vector2(-170,95);
                Assert.That(CardDrawFlight.Pose.TryCapture(f.Deck.DrawSourceRect,f.Root.GetComponent<RectTransform>(),out var origin),Is.True);
                f.Hand.QueueCardDraws(2); state.Add("PowerBank","draw-a",false); state.Add("Goblin","draw-b",false); f.Hand.Present(state);
                Assert.That(f.Flights().Length,Is.EqualTo(2));
                foreach(var flight in f.Flights())
                {
                    Assert.That(Vector2.Distance(flight.transform.localPosition,origin.Center),Is.LessThan(3));
                    Assert.That(flight.transform.Find("DeckBack").GetComponent<Image>().sprite,Is.SameAs(back.sprite));
                    Assert.That(flight.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
                    Assert.That(flight.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
                }
                Assert.That(f.Artwork("draw-a").GetComponentInParent<CanvasGroup>().alpha,Is.Zero);
                Assert.That(f.Artwork("base").GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
                f.Hand.Present(state); Assert.That(f.Flights().Length,Is.EqualTo(2),"Repeated state cannot replay draws.");
                yield return new WaitForSecondsRealtime(.23f);
                Assert.That(f.Flights().Any(x=>x.transform.Find("DrawnCard").gameObject.activeSelf),Is.True);
                Assert.That(f.Flights().Any(x=>x.GetComponentsInChildren<Text>(true).Any(t=>!string.IsNullOrEmpty(t.text))),Is.True,"Numbers travel with card artwork.");
                f.Hand.SetExpanded(false); yield return null;
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(f.Flights(),Is.Empty);
                foreach(var id in new[]{"draw-a","draw-b"})
                { var group=f.Artwork(id).GetComponentInParent<CanvasGroup>(); Assert.That(group.alpha,Is.EqualTo(1)); Assert.That(group.interactable,Is.True); }
            }
        }

        [UnityTest] public IEnumerator DrawTracksRuntimeIdAcrossHandRebindingAndCancelsRemovedCard()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                var state=f.State(); f.Hand.Present(state); yield return null;
                f.Hand.QueueCardDraws(1); f.Hand.Present(state); Assert.That(f.Flights(),Is.Empty,"Old refresh must retain the pending draw cue.");
                state.Add("ManaStone","temporary",true); f.Hand.Present(state); Assert.That(f.Flights(),Is.Empty,"Replicate generation is not a deck draw.");
                state.Add("PowerBank","draw",false); f.Hand.Present(state); yield return null; Assert.That(f.Flights().Length,Is.EqualTo(1));
                var ghost=f.Flights().Single();
                state.Remove("ManaStone","base"); f.Hand.Present(state); yield return null;
                Assert.That(f.Flights().Single(),Is.SameAs(ghost));
                Assert.That(f.Artwork("draw").GetComponentInParent<CanvasGroup>().alpha,Is.Zero);
                Assert.That(f.Artwork("temporary").GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
                state.Remove("PowerBank","draw"); state.Add("ManaStone","reuse",false); f.Hand.Present(state); yield return null;
                Assert.That(f.Flights(),Is.Empty); Assert.That(f.Artwork("reuse").GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
            }
        }

        [UnityTest] public IEnumerator PausedTimeCompletionResetAndDisableRestoreHandWithoutReplay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                var state=f.State(); f.Hand.Present(state); yield return null;
                Time.timeScale=0; f.Hand.QueueCardDraws(1); state.Add("PowerBank","draw",false); f.Hand.Present(state);
                yield return new WaitForSecondsRealtime(.5f); Assert.That(f.Flights(),Is.Empty);
                f.Hand.QueueCardDraws(1); state.Add("ManaStone","cancel",false); f.Hand.Present(state); yield return null; Assert.That(f.Flights().Length,Is.EqualTo(1));
                f.Boot.CancelCombatPresentation(); yield return null;
                Assert.That(f.Flights(),Is.Empty); Assert.That(f.Artwork("cancel").GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
                f.Hand.QueueCardDraws(1); state.Add("ManaStone","disable",false); f.Hand.Present(state);
                f.Root.SetActive(false); f.Root.SetActive(true); f.Hand.Present(state); yield return null;
                Assert.That(f.Flights(),Is.Empty); Assert.That(f.Artwork("disable").GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
                f.Hand.CancelCardDraws(); var restored=new HandState(); restored.Add("ManaStone","restored",false);
                f.Hand.QueueCardDraws(5); f.Hand.Present(restored); Assert.That(f.Flights(),Is.Empty,"First authoritative snapshot is a baseline.");
            }
        }

        [UnityTest] public IEnumerator OnlineOwnDrawFromEitherSeatExcludesOpponentAndSnapshots()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                var connection=f.Driver.AddComponent<OnlineBattleConnectionTester>();
                foreach(var seat in new[]{PlayerId.Player,PlayerId.AI})
                {
                    f.Hand.CancelCardDraws(); var state=f.State(); f.Hand.Present(state); yield return null;
                    Set(connection,"_localPlayerId",seat);
                    var opponent=seat==PlayerId.Player?PlayerId.AI:PlayerId.Player;
                    var events=new List<BattleEventDto>{new BattleEventDto{EventType=BattleEventType.CardDrawn,SourceOwnerId=opponent,Amount=1}};
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    state.Add("ManaStone","other-change",false); f.Hand.Present(state); Assert.That(f.Flights(),Is.Empty);
                    events[0].SourceOwnerId=seat;
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    state.Add("PowerBank","own-draw",false); f.Hand.Present(state); yield return null; Assert.That(f.Flights().Length,Is.EqualTo(1));
                    f.Hand.Present(state); yield return null; Assert.That(f.Flights().Length,Is.EqualTo(1));
                }
            }
        }

        [UnityTest] public IEnumerator LocalGatewayDrawEventsAnimateOnlyNewOwnDraws()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                var provider=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
                var gateway=TutorialResourceBattle.Create(provider);
                Set(f.Boot,"_battleGateway",gateway); Set(f.Boot,"_cardDefinitionProvider",provider);
                Set(f.Screen,"_playerDeckPresenter",f.Deck);
                Call(f.Boot,"RefreshPresenter"); yield return null;
                gateway.ApplyMulligan(PlayerId.Player,new[]{"firebolt"},new TutorialResourceBattle.MulliganDeck()); gateway.ResolveTurnStart();
                Call(f.Boot,"RefreshPresenter"); Assert.That(f.Flights().Length,Is.EqualTo(1),"Actual local first-turn draw uses the same path.");
                var before=f.Flights().Single(); Call(f.Boot,"RefreshPresenter"); Assert.That(f.Flights().Single(),Is.SameAs(before));
                yield return new WaitForSecondsRealtime(.5f);
                BattleDrawService.DrawCards(gateway.CurrentBattleState.Player,gateway.CurrentBattleState,2);
                BattleDrawService.DrawCards(gateway.CurrentBattleState.AI,gateway.CurrentBattleState,1);
                Call(f.Boot,"RefreshPresenter"); Assert.That(f.Flights().Length,Is.EqualTo(2),"Additional own draws animate; opponent draws do not.");
                f.Boot.CancelCombatPresentation(); Call(f.Boot,"RefreshPresenter"); Assert.That(f.Flights(),Is.Empty);
            }
        }

        [UnityTest] public IEnumerator MulliganRedrawIsHandledByOverlayNotNormalHandFlight()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                var provider=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
                var gateway=TutorialResourceBattle.Create(provider); var deck=TutorialResourceBattle.CreateDeck();
                gateway.StartBattle(new BattleSetupRequest(deck,deck,PlayerId.Player,aiMulliganEnabled:true));
                Set(f.Boot,"_battleGateway",gateway); Set(f.Boot,"_cardDefinitionProvider",provider); Set(f.Screen,"_playerDeckPresenter",f.Deck);
                Call(f.Boot,"RefreshPresenter"); yield return null;
                gateway.ApplyMulligan(PlayerId.Player,new[]{"firebolt"},new TutorialResourceBattle.MulliganDeck());
                Call(f.Boot,"RefreshPresenter"); Assert.That(f.Flights(),Is.Empty,"Waiting for opponent after redraw must not duplicate the mulligan flight.");
                gateway.PassMulligan(PlayerId.AI); gateway.ResolveTurnStart();
                Call(f.Boot,"RefreshPresenter"); Assert.That(f.Flights().Length,Is.EqualTo(1));
                yield return new WaitForSecondsRealtime(.5f); Assert.That(f.Flights(),Is.Empty);
            }
        }

        [UnityTest] public IEnumerator FirstTurnDrawWaitsForMulliganPresentationAndUsesLastDeckPose()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                var overlay=f.Driver.AddComponent<BattleMulliganOverlayPresenter>(); Set(overlay,"_sessionActive",true);
                f.Hand.ConfigureDeckDrawSource(f.Deck,overlay);
                var state=f.State(); f.Hand.Present(state); yield return null;
                f.Deck.PresentCount(1);
                CardDrawFlight.Pose.TryCapture(f.Deck.DrawSourceRect,f.Root.GetComponent<RectTransform>(),out var origin);
                f.Hand.QueueCardDraws(1); state.Add("PowerBank","last-deck-card",false); f.Hand.Present(state); f.Deck.PresentCount(0);
                Assert.That(f.Flights(),Is.Empty); yield return new WaitForSecondsRealtime(.1f); Assert.That(f.Flights(),Is.Empty);
                Set(overlay,"_sessionActive",false); yield return null;
                Assert.That(f.Flights().Length,Is.EqualTo(1));
                Assert.That(Vector2.Distance(f.Flights()[0].transform.localPosition,origin.Center),Is.LessThan(15),"Hidden last deck layer cannot move the captured origin.");
                yield return new WaitForSecondsRealtime(.5f); Assert.That(f.Flights(),Is.Empty);
            }
        }

        [UnityTest] public IEnumerator IndependentDeckDepartureStaysOnDeckAfterHandCanvasScaleChanges()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                // The production scene has independent overlay canvases, with a bottom-left
                // Canvas pivot. The older fixtures put both deck and hand on one Canvas.
                f.Deck.transform.SetParent(null,false);
                try
                {
                    var handCanvas=f.Root.GetComponent<Canvas>();
                    f.Root.GetComponent<RectTransform>().pivot=Vector2.zero;
                    var state=f.State(); f.Hand.Present(state); yield return null;
                    Canvas.ForceUpdateCanvases();
                    f.Hand.QueueCardDraws(1);state.Add("PowerBank","draw",false);f.Hand.Present(state);
                    var flight=f.Flights().Single(); var originRect=f.Deck.DrawSourceRect;
                    foreach(var scale in new[]{1f,.5f,1.5f})
                    {
                        handCanvas.scaleFactor=scale;Canvas.ForceUpdateCanvases();
                        // Hold the exact departure phase, isolating coordinate-space changes
                        // from elapsed animation progress or network/event timing.
                        Set(flight,"_start",Time.unscaledTime);
                        flight.Tick(f.Artwork("draw"));
                        var expected=RectTransformUtility.WorldToScreenPoint(null,f.Deck.DrawSourceRect.TransformPoint(f.Deck.DrawSourceRect.rect.center));
                        var actual=RectTransformUtility.WorldToScreenPoint(null,flight.transform.position);
                        Assert.That(Vector2.Distance(actual,expected),Is.LessThan(2),"Departure must remain on the independent deck at hand Canvas scale "+scale);
                    }
                    // A source layout change during the same flight must use the visible
                    // deck, and hiding the final layer must not switch to the root's pivot.
                    originRect.anchoredPosition+=new Vector2(-75,40);
                    f.Deck.PresentCount(0); Canvas.ForceUpdateCanvases();
                    Set(flight,"_start",Time.unscaledTime); flight.Tick(f.Artwork("draw"));
                    var lastDeckScreen=RectTransformUtility.WorldToScreenPoint(null,originRect.TransformPoint(originRect.rect.center));
                    var lastFlightScreen=RectTransformUtility.WorldToScreenPoint(null,flight.transform.position);
                    Assert.That(Vector2.Distance(lastFlightScreen,lastDeckScreen),Is.LessThan(2),"Retain exact deck layer through source movement and last-card hide.");
                    yield return new WaitForSecondsRealtime(.5f); Assert.That(f.Flights(),Is.Empty);
                    Assert.That(f.Artwork("draw").GetComponentInParent<CanvasGroup>().interactable,Is.True);
                }
                finally {Object.DestroyImmediate(f.Deck.gameObject);}
            }
        }

        [UnityTest] public IEnumerator OnlineMidBattleDrawsOnTurns17Through21NeverStartMulliganDeal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                f.Deck.transform.SetParent(null,false);
                try
                {
                    var provider=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
                    var gateway=TutorialResourceBattle.Create(provider);gateway.PassMulligan(PlayerId.Player);gateway.ResolveTurnStart();
                    var battle=gateway.CurrentBattleState;
                    var overlay=f.Driver.AddComponent<BattleMulliganOverlayPresenter>();
                    Set(f.Screen,"_playerDeckPresenter",f.Deck);Set(f.Screen,"_mulliganOverlayPresenter",overlay);
                    f.Screen.Present(battle);yield return null;
                    foreach(var turn in new[]{17,19,21})
                    {
                        battle.RestoreRuntimeState(turn,PlayerId.Player,PhaseType.Main);
                        var oldest=battle.Player.Hand.Cards[0];battle.Player.Hand.Remove(oldest.CardId,oldest.RuntimeId);
                        f.Screen.Present(battle);
                        f.Boot.PlayOnlineBattleEventAnimations(new[]{new BattleEventDto{EventType=BattleEventType.CardDrawn,SourceOwnerId=PlayerId.Player,Amount=1}});
                        BattleDrawService.DrawCards(battle.Player,battle,1);f.Screen.Present(battle);
                        var flight=f.Flights().Single();
                        f.Root.GetComponent<Canvas>().scaleFactor=turn==19?.5f:1f;Canvas.ForceUpdateCanvases();
                        Set(flight,"_start",Time.unscaledTime);Call(f.Hand,"TickCardDraws");
                        var expected=RectTransformUtility.WorldToScreenPoint(null,f.Deck.DrawSourceRect.TransformPoint(f.Deck.DrawSourceRect.rect.center));
                        var actual=RectTransformUtility.WorldToScreenPoint(null,flight.transform.position);
                        Assert.That(Vector2.Distance(actual,expected),Is.LessThan(2),"Own turn "+turn+" must depart from the visible deck.");
                        Assert.That(overlay.IsSessionActive,Is.False);Assert.That(overlay.IsDealingOpeningHand,Is.False);Assert.That(overlay.IsDealingReplacementHand,Is.False);
                        yield return new WaitForSecondsRealtime(.5f);Assert.That(f.Flights(),Is.Empty);
                    }
                }
                finally{Object.DestroyImmediate(f.Deck.gameObject);}
            }
        }

        [UnityTest] public IEnumerator ConsecutiveDrawsReuseOriginCacheAndReconnectClearsIt()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();Assert.That(Application.isPlaying,Is.True);
            using(var f=new Fixture())
            {
                var state=f.State();f.Hand.Present(state);yield return null;Canvas.ForceUpdateCanvases();f.Hand.Present(state);
                var cache=typeof(HandPresenter).GetField("_drawOriginCache",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.Hand);
                Func<int> count=()=>(int)cache.GetType().GetProperty("RecalculationCount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cache);
                var warmed=count();Assert.That(warmed,Is.GreaterThan(0));
                foreach(var id in new[]{"cached-draw-a","cached-draw-b"})
                {
                    f.Hand.QueueCardDraws(1);state.Add("PowerBank",id,false);f.Hand.Present(state);
                    for(var i=0;i<20;i++)Call(f.Hand,"TickCardDraws");
                    Assert.That(count(),Is.EqualTo(warmed),"Stable origin must be shared by successive flights.");
                    yield return new WaitForSecondsRealtime(.5f);Assert.That(f.Flights(),Is.Empty);
                    Assert.That(count(),Is.EqualTo(warmed));
                }
                f.Boot.CancelCombatPresentation();Assert.That(count(),Is.Zero,"Reconnect/reset clears the old battle cache.");
                f.Hand.Present(state);Assert.That(count(),Is.EqualTo(1));Assert.That(f.Flights(),Is.Empty,"Restored hand remains a baseline.");
                f.Hand.QueueCardDraws(1);state.Add("ManaStone","after-reconnect",false);f.Hand.Present(state);
                Assert.That(count(),Is.EqualTo(1));yield return new WaitForSecondsRealtime(.5f);Assert.That(f.Flights(),Is.Empty);
            }
        }

        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        private sealed class Fixture:IDisposable
        {
            internal readonly GameObject Root,Driver;
            internal readonly HandPresenter Hand;
            internal readonly PlayerDeckPresenter Deck;
            internal readonly BattleScreenPresenter Screen;
            internal readonly BattleBootstrapper Boot;
            internal Fixture()
            {
                Root=new GameObject("DrawFixture",typeof(RectTransform),typeof(Canvas)); Root.SetActive(false);
                var canvas=Root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                var slots=new GameObject("Slots",typeof(RectTransform)); slots.transform.SetParent(Root.transform,false);
                var layout=new GameObject("RuntimeHandRoot",typeof(RectTransform)); layout.transform.SetParent(Root.transform,false);
                Hand=Root.AddComponent<HandPresenter>(); Set(Hand,"_handRoot",slots.transform);
                Set(Hand,"_runtimeCanvas",canvas); Set(Hand,"_runtimeCanvasRect",Root.GetComponent<RectTransform>()); Set(Hand,"_runtimeHandRoot",layout.GetComponent<RectTransform>());
                var deckNode=new GameObject("Deck",typeof(RectTransform),typeof(Canvas)); deckNode.transform.SetParent(Root.transform,false); Deck=deckNode.AddComponent<PlayerDeckPresenter>();
                Driver=new GameObject("Driver"); Driver.SetActive(false); Driver.transform.SetParent(Root.transform,false);
                Screen=Driver.AddComponent<BattleScreenPresenter>(); Set(Screen,"_handPresenter",Hand);
                Boot=Driver.AddComponent<BattleBootstrapper>(); Set(Boot,"_battleScreenPresenter",Screen);
                Set(Boot,"_autoRunAiTurns",false); Set(Boot,"_animateAttackSequences",false); Set(Boot,"_sendPlayerActionsToOnlineGateway",false);
                Root.SetActive(true); Hand.SetExpanded(true); Deck.PresentCount(20); Hand.ConfigureDeckDrawSource(Deck);
            }
            internal HandState State(){var state=new HandState();state.Add("ManaStone","base",false);return state;}
            internal Image Artwork(string id)=>Hand.HandCardViews.Single(v=>v.HasCard&&v.RuntimeId==id).GetComponent<HandCardTextView>().TutorialArtwork;
            internal CardDrawFlight[] Flights()=>Root.GetComponentsInChildren<CardDrawFlight>();
            public void Dispose(){ Hand.CancelCardDraws(); Object.DestroyImmediate(Root); }
        }
    }
}
