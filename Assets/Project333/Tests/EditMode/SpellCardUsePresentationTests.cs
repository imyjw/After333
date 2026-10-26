using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class SpellCardUsePresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Set(object o, string field, object value) => o.GetType().GetField(field, Hidden).SetValue(o, value);
        private static object Call(object o, string method, params object[] args) => o.GetType().GetMethods(Hidden)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length &&
                m.GetParameters().Select((p,i) => p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(o,args);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void CardBreaksIntoBoundedFragmentsWithBlueTwinklesAndFinishesEmpty()
        {
            var node = new GameObject("BlueShardMesh", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var graphic=node.AddComponent<SpellCardUseGraphic>();
                graphic.rectTransform.sizeDelta=new Vector2(270,378);
                using (var mesh=new VertexHelper())
                {
                    graphic.Configure(null,Color.white,false); graphic.SetProgress(0);
                    Call(graphic,"OnPopulateMesh",mesh);
                    Assert.That(mesh.currentVertCount,Is.EqualTo(16*22*4));
                    graphic.SetProgress(.55f); Call(graphic,"OnPopulateMesh",mesh);
                    var verts=new List<UIVertex>(); mesh.GetUIVertexStream(verts);
                    Assert.That(verts.All(v=>v.color.b>v.color.r && v.color.b>v.color.g),Is.True);
                    Assert.That(verts.Any(v=>!graphic.rectTransform.rect.Contains(v.position)),Is.True);
                    graphic.Configure(null,Color.white,true); Call(graphic,"OnPopulateMesh",mesh);
                    Assert.That(mesh.currentVertCount,Is.GreaterThan(0).And.LessThanOrEqualTo((16*22+88*3)*4));
                    graphic.SetProgress(1); Call(graphic,"OnPopulateMesh",mesh);
                    Assert.That(mesh.currentVertCount,Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(node); }
        }

        [UnityTest]
        public IEnumerator AcceptedSpellStartsBeforeDelayedHandRefresh()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var hand=new HandState(); hand.Add("Earthquake","quake",false);
                f.Hand.Present(hand); yield return null;
                var artwork=f.Hand.HandCardViews[0].GetComponent<HandCardTextView>().TutorialArtwork;
                artwork.rectTransform.position+=new Vector3(150,170,0);
                f.Hand.PrepareSpellCardUse("Earthquake","quake");
                var position=f.All().Single().transform.position;
                f.Hand.ConfirmSpellCardUse("Earthquake","quake");
                Assert.That(f.Active().Length,Is.EqualTo(1),"Confirmation must start breakup before the area effect refreshes the hand.");
                Assert.That(f.Hand.HandCardViews[0].HasCard,Is.True,"Delayed presentation still holds the old hand.");
                Assert.That(artwork.GetComponentInParent<CanvasGroup>().alpha,Is.Zero,"Consumed artwork must not return to the hand.");
                f.Hand.Present(hand);
                Assert.That(f.Active().Single().transform.position,Is.EqualTo(position));
                yield return null;
                Assert.That(artwork.GetComponentInParent<CanvasGroup>().alpha,Is.Zero,"Old-state refresh must not resurrect the card.");
                yield return new WaitForSecondsRealtime(SpellCardUseVfx.Duration+.05f);
                Assert.That(f.All(),Is.Empty,"Short breakup finishes while the area effect can still be playing.");
                Assert.That(artwork.GetComponentInParent<CanvasGroup>().alpha,Is.Zero);
                hand.Remove("Earthquake","quake"); f.Hand.Present(hand);
                Assert.That(f.All(),Is.Empty,"The delayed hand refresh cannot replay breakup.");
                hand.Add("Earthquake","new-copy",false); f.Hand.Present(hand); yield return null;
                Assert.That(f.Hand.HandCardViews[0].GetComponent<HandCardTextView>().TutorialArtwork.GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1f),"A reused slot must not inherit suppression.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PendingSpellStaysAtDropUntilConfirmationAndRejectRestoresOnlyItsCard()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var hand=new HandState(); hand.Add("ManaStone","waiting",false); hand.Add("ManaStone","other",false);
                f.Hand.Present(hand); yield return null;
                var artwork=f.Hand.HandCardViews[0].GetComponent<HandCardTextView>().TutorialArtwork;
                var other=f.Hand.HandCardViews[1].GetComponent<HandCardTextView>().TutorialArtwork;
                artwork.rectTransform.position+=new Vector3(100,180,0);
                f.Hand.PrepareSpellCardUse("ManaStone","waiting");
                var held=f.All().Single(); var drop=held.transform.position;
                Assert.That(held.gameObject.activeInHierarchy,Is.True);
                Assert.That(held.IsPlaying,Is.False);
                Assert.That(artwork.GetComponentInParent<CanvasGroup>().alpha,Is.Zero);
                Assert.That(artwork.GetComponentInParent<CanvasGroup>().blocksRaycasts,Is.False);
                Assert.That(other.GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
                f.Hand.Present(hand);
                yield return new WaitForSecondsRealtime(SpellCardUseVfx.Duration+.1f);
                Assert.That(f.All().Length,Is.EqualTo(1),"An awaiting response cannot time out with the cosmetic duration.");
                Assert.That(held.IsPlaying,Is.False);
                Assert.That(held.transform.position,Is.EqualTo(drop));
                Assert.That(artwork.GetComponentInParent<CanvasGroup>().alpha,Is.Zero);
                f.Boot.CancelPendingSpellCardUse(); yield return null;
                Assert.That(f.All(),Is.Empty);
                Assert.That(artwork.GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
                Assert.That(other.GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RealLocalSelfCastsShareOneEffectWithoutMovingFollowingCards()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f=new Fixture())
            {
                var provider=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
                var gateway=TutorialResourceBattle.Create(provider);
                gateway.ApplyMulligan(PlayerId.Player,new[]{"firebolt"},new TutorialResourceBattle.MulliganDeck()); gateway.ResolveTurnStart();
                Set(f.Boot,"_battleGateway",gateway); Set(f.Boot,"_cardDefinitionProvider",provider);
                foreach (var id in new[]{"Daehwandan","ManaStone","PowerBank"})
                {
                    var state=gateway.CurrentBattleState;
                    foreach(var old in state.Player.Hand.CardIds.ToArray()) state.Player.Hand.Remove(old);
                    state.Player.Resources.Add(new ResourceSet(100,100,100,100));
                    state.Player.Hand.Add(id,"used",true); state.Player.Hand.Add(id,"kept",false);
                    f.Screen.Present(state); yield return null;
                    var source=f.Hand.HandCardViews[0].GetComponent<HandCardTextView>().TutorialArtwork;
                    source.rectTransform.position+=new Vector3(120,150,0);
                    Assert.That(f.Boot.TryCastPlayerPersistentSpell(id,"used",out var message),Is.True,message);
                    Assert.That(state.Player.Hand.Contains(id,"used"),Is.False);
                    Assert.That(state.Player.Hand.Contains(id,"kept"),Is.True);
                    Assert.That(f.Active().Length,Is.EqualTo(1));
                    var effect=f.Active().Single(); var position=effect.transform.position;
                    Assert.That(effect.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
                    Assert.That(effect.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),Is.True);
                    Assert.That(f.Root.GetComponentsInChildren<TemporaryReplicateDissolveView>(),Is.Empty);
                    f.Screen.Present(state); yield return null;
                    Assert.That(effect.transform.position,Is.EqualTo(position));
                    Assert.That(f.Active().Length,Is.EqualTo(1));
                    f.Hand.CancelSpellCardUses(); yield return null;
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FailedCommandAndUnconfirmedSnapshotNeverDisintegrateTheCard()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var provider=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
                var gateway=TutorialResourceBattle.Create(provider);
                gateway.ApplyMulligan(PlayerId.Player,new[]{"firebolt"},new TutorialResourceBattle.MulliganDeck()); gateway.ResolveTurnStart();
                var state=gateway.CurrentBattleState;
                state.Player.Resources.Spend(state.Player.Resources.Clone());
                state.Player.Hand.Add("PowerBank","blocked",false);
                Set(f.Boot,"_battleGateway",gateway); Set(f.Boot,"_cardDefinitionProvider",provider);
                f.Screen.Present(state); yield return null;
                // This fixture deliberately keeps the scene driver inactive. Only its toast coroutine logs this.
                LogAssert.Expect(LogType.Error, "Coroutine couldn't be started because the the game object 'Driver' is inactive!");
                Assert.That(f.Boot.TryCastPlayerPersistentSpell("PowerBank","blocked",out _),Is.False);
                Assert.That(state.Player.Hand.Contains("PowerBank","blocked"),Is.True);
                yield return null;
                Assert.That(f.All(),Is.Empty);
                f.Hand.PrepareSpellCardUse("PowerBank","blocked");
                Assert.That(f.All().Length,Is.EqualTo(1));
                Assert.That(f.Active(),Is.Empty,"Waiting copy must not disintegrate before confirmation.");
                state.Player.Hand.Remove("PowerBank","blocked"); f.Hand.Present(state.Player.Hand); yield return null;
                Assert.That(f.All(),Is.Empty,"A bare snapshot cannot confirm card use.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator OnlineConfirmationUsesOwnProjectedSeatAndExactRemovedRuntimeId()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var connection=f.Driver.AddComponent<OnlineBattleConnectionTester>();
                foreach(var seat in new[]{PlayerId.Player,PlayerId.AI})
                {
                    Set(connection,"_localPlayerId",seat);
                    var hand=new HandState(); hand.Add("ManaStone","first",false); hand.Add("ManaStone","second",false);
                    f.Hand.Present(hand); yield return null;
                    f.Hand.PrepareSpellCardUse("ManaStone","second");
                    f.Hand.PrepareSpellCardUse("ManaStone","second");
                    Assert.That(f.All().Length,Is.EqualTo(1));
                    var position=f.All().Single().transform.position;
                    var opponent=seat==PlayerId.Player?PlayerId.AI:PlayerId.Player;
                    var events=new List<BattleEventDto>{new BattleEventDto{EventType=BattleEventType.SpellCast,SourceOwnerId=opponent,CardId="ManaStone"}};
                    // The inactive fixture does not run the separate opponent-reveal coroutine.
                    LogAssert.Expect(LogType.Error, "Coroutine couldn't be started because the the game object 'Driver' is inactive!");
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    f.Boot.ClearOpponentPlayedCardReveals();
                    f.Hand.Present(hand); Assert.That(f.Active(),Is.Empty);
                    events[0].SourceOwnerId=seat;
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    Assert.That(f.Active().Length,Is.EqualTo(1),"Confirmed use must not wait for the delayed hand state.");
                    f.Hand.Present(hand);
                    hand.Remove("ManaStone","first"); f.Hand.Present(hand);
                    Assert.That(f.Active().Length,Is.EqualTo(1),"An old/different-card refresh must not restart breakup.");
                    var remaining=f.Hand.HandCardViews.Single(v=>v.HasCard && v.RuntimeId=="second");
                    Assert.That(remaining.GetComponent<HandCardTextView>().TutorialArtwork.GetComponentInParent<CanvasGroup>().alpha,Is.Zero);
                    hand.Remove("ManaStone","second"); f.Hand.Present(hand);
                    Assert.That(f.Active().Length,Is.EqualTo(1));
                    Assert.That(f.Active()[0].transform.position,Is.EqualTo(position));
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    f.Hand.Present(hand); Assert.That(f.Active().Length,Is.EqualTo(1));
                    f.Boot.CancelCombatPresentation(); yield return null;
                    Assert.That(f.All(),Is.Empty);
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RejectionResetDisableAndPausedTimeCleanUpIndependentSnapshots()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var hand=new HandState(); hand.Add("ManaStone","one",false);
                f.Hand.Present(hand); yield return null;
                f.Hand.PrepareSpellCardUse("ManaStone","one"); f.Boot.CancelPendingSpellCardUse(); yield return null;
                Assert.That(f.All(),Is.Empty);
                Assert.That(f.Hand.HandCardViews[0].GetComponent<HandCardTextView>().TutorialArtwork.GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1f));
                f.Hand.PrepareSpellCardUse("ManaStone","one"); f.Screen.ResetTutorialPractice(); yield return null;
                Assert.That(f.All(),Is.Empty);
                f.Hand.PrepareSpellCardUse("ManaStone","one"); f.Hand.enabled=false; yield return null;
                Assert.That(f.All(),Is.Empty); f.Hand.enabled=true;
                f.Hand.PrepareSpellCardUse("ManaStone","one"); f.Hand.ConfirmSpellCardUse("ManaStone");
                Time.timeScale=0; hand.Remove("ManaStone","one"); f.Hand.Present(hand);
                Assert.That(f.Active().Length,Is.EqualTo(1));
                yield return new WaitForSecondsRealtime(SpellCardUseVfx.Duration+.15f);
                Assert.That(f.All(),Is.Empty);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EveryTrapInstallsByUpwardAirDragAndUsesSharedBreakup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var provider=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
                var gateway=TutorialResourceBattle.Create(provider);
                gateway.ApplyMulligan(PlayerId.Player,new[]{"firebolt"},new TutorialResourceBattle.MulliganDeck()); gateway.ResolveTurnStart();
                var state=gateway.CurrentBattleState; state.Player.Resources.Spend(state.Player.Resources.Clone());
                Set(f.Boot,"_battleGateway",gateway); Set(f.Boot,"_cardDefinitionProvider",provider);
                var input=f.Driver.AddComponent<BattlePlayerInputController>(); Set(input,"_battleBootstrapper",f.Boot); Set(input,"_handPresenter",f.Hand);
                var tileNode=new GameObject("MasterTile"); tileNode.transform.SetParent(f.Driver.transform);
                var tile=tileNode.AddComponent<TileView>(); tile.Configure(PlayerId.Player,state.Player.Master.Position.Column,state.Player.Master.Position.Row);
                foreach(var id in new[]{"TrapReturn","TrapMirrorField","ExplosiveReactiveArmour","IronDome","SummonTrap"})
                {
                    foreach(var old in state.Player.Hand.CardIds.ToArray()) state.Player.Hand.Remove(old);
                    state.Player.Hand.Add(id,"install-"+id,false); f.Screen.Present(state); f.Hand.SetExpanded(true); yield return null;
                    var card=f.Hand.HandCardViews[0];
                    Assert.That((bool)Call(input,"CanCardCastWithoutTarget",state,id),Is.True,id);
                    Assert.That((bool)Call(input,"CanCardUseOnTile",state,id,tile),Is.False,"No master target/highlight.");
                    var before=state.ArmedTraps.Count;
                    Call(input,"HandleHandCardDragStarted",card,new Vector2(100,100));
                    Assert.That((bool)Call(input,"CanSelectedCardTargetOccupant",state,state.Player.Master),Is.False);
                    var art=card.GetComponent<HandCardTextView>().TutorialArtwork;
                    art.rectTransform.position+=new Vector3(0,160,0);
                    var drop=art.rectTransform.TransformPoint(HandCardStatOverlayLayout.CalculateRenderedSpriteRect(art).center);
                    Call(input,"HandleHandCardDragEnded",card,new Vector2(100,350));
                    Assert.That(state.ArmedTraps.Count,Is.EqualTo(before+1),id);
                    Assert.That(state.ArmedTraps.Last().RuntimeId,Is.EqualTo("install-"+id));
                    Assert.That(state.Player.Hand.Contains(id,"install-"+id),Is.False);
                    Assert.That(state.Player.Resources.Gold,Is.Zero,"Activation cost is not charged on installation.");
                    Assert.That(f.Active().Length,Is.EqualTo(1),id);
                    Assert.That(Vector3.Distance(f.Active()[0].transform.position,drop),Is.LessThan(.05f));
                    f.Hand.CancelSpellCardUses(); yield return null;
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TrapShortDragCancelsAndDuplicateInstallationKeepsItsCard()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var provider=AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
                var gateway=TutorialResourceBattle.Create(provider);
                gateway.ApplyMulligan(PlayerId.Player,new[]{"firebolt"},new TutorialResourceBattle.MulliganDeck()); gateway.ResolveTurnStart();
                var state=gateway.CurrentBattleState;
                foreach(var old in state.Player.Hand.CardIds.ToArray()) state.Player.Hand.Remove(old);
                state.Player.Hand.Add("TrapReturn","cancel",false);
                Set(f.Boot,"_battleGateway",gateway); Set(f.Boot,"_cardDefinitionProvider",provider);
                var input=f.Driver.AddComponent<BattlePlayerInputController>(); Set(input,"_battleBootstrapper",f.Boot); Set(input,"_handPresenter",f.Hand);
                f.Screen.Present(state); f.Hand.SetExpanded(true); yield return null;
                var card=f.Hand.HandCardViews[0];
                Call(input,"HandleHandCardDragStarted",card,new Vector2(100,100));
                Call(input,"HandleHandCardDragEnded",card,new Vector2(100,130));
                Assert.That(state.ArmedTraps,Is.Empty); Assert.That(f.All(),Is.Empty);
                Assert.That(state.Player.Hand.Contains("TrapReturn","cancel"),Is.True);
                Assert.That(f.Boot.TryCastPlayerPersistentSpell("TrapReturn","cancel",out var message),Is.True,message);
                f.Hand.CancelSpellCardUses(); yield return null;
                state.Player.Hand.Add("TrapReturn","duplicate",false); f.Screen.Present(state); yield return null;
                Assert.That((bool)Call(input,"CanCardCastWithoutTarget",state,"TrapReturn"),Is.False);
                LogAssert.Expect(LogType.Error,"Coroutine couldn't be started because the the game object 'Driver' is inactive!");
                Assert.That(f.Boot.TryCastPlayerPersistentSpell("TrapReturn","duplicate",out _),Is.False);
                yield return null;
                Assert.That(state.ArmedTraps.Count,Is.EqualTo(1));
                Assert.That(state.Player.Hand.Contains("TrapReturn","duplicate"),Is.True);
                Assert.That(f.All(),Is.Empty);
                Assert.That(f.Hand.HandCardViews[0].GetComponent<HandCardTextView>().TutorialArtwork.GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1f));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GenericOwnTrapAcknowledgementConfirmsOnlyPendingTrapsForBothSeats()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using(var f=new Fixture())
            {
                var connection=f.Driver.AddComponent<OnlineBattleConnectionTester>();
                foreach(var seat in new[]{PlayerId.Player,PlayerId.AI})
                {
                    Set(connection,"_localPlayerId",seat);
                    var hand=new HandState(); hand.Add("ManaStone","mana",false); hand.Add("TrapReturn","return",false); hand.Add("TrapMirrorField","mirror",false);
                    f.Hand.Present(hand); yield return null;
                    f.Hand.PrepareSpellCardUse("ManaStone","mana");
                    f.Hand.PrepareTrapCardUse("TrapReturn","return");
                    f.Hand.PrepareTrapCardUse("TrapMirrorField","mirror");
                    var events=new List<BattleEventDto>{new BattleEventDto{EventType=BattleEventType.TrapInstalled,SourceOwnerId=seat,TrapCount=1}};
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    Assert.That(f.Active().Length,Is.EqualTo(1));
                    Assert.That(events[0].CardId,Is.Empty); Assert.That(events[0].RuntimeId,Is.Empty,"Never reveal trap identity in public ACK.");
                    events[0].SourceOwnerId=seat==PlayerId.Player?PlayerId.AI:PlayerId.Player;
                    LogAssert.Expect(LogType.Error,"Coroutine couldn't be started because the the game object 'Driver' is inactive!");
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    f.Boot.ClearOpponentPlayedCardReveals();
                    Assert.That(f.Active().Length,Is.EqualTo(1),"Opponent installation cannot confirm own pending card.");
                    events[0].SourceOwnerId=seat; events[0].TrapCount=2;
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    Assert.That(f.Active().Length,Is.EqualTo(2));
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    Assert.That(f.Active().Length,Is.EqualTo(2),"Generic ACK cannot consume the ordinary spell cue.");
                    events[0]=new BattleEventDto{EventType=BattleEventType.SpellCast,SourceOwnerId=seat,CardId="ManaStone"};
                    f.Boot.PlayOnlineBattleEventAnimations((IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",events));
                    Assert.That(f.Active().Length,Is.EqualTo(3));
                    f.Boot.CancelCombatPresentation(); yield return null;
                    Assert.That(f.All(),Is.Empty);
                }
            }
            yield return new ExitPlayMode();
        }

        private sealed class Fixture:IDisposable
        {
            internal readonly GameObject Root,Driver;
            internal readonly HandPresenter Hand;
            internal readonly BattleScreenPresenter Screen;
            internal readonly BattleBootstrapper Boot;
            internal Fixture()
            {
                Root=new GameObject("SpellHandFixture",typeof(RectTransform),typeof(Canvas)); Root.SetActive(false);
                var canvas=Root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                var slots=new GameObject("LogicalSlots",typeof(RectTransform)); slots.transform.SetParent(Root.transform,false);
                var layout=new GameObject("RuntimeHandRoot",typeof(RectTransform)); layout.transform.SetParent(Root.transform,false);
                var rect=layout.GetComponent<RectTransform>(); rect.anchorMin=new Vector2(0,0); rect.anchorMax=new Vector2(1,0);
                rect.pivot=new Vector2(.5f,0); rect.sizeDelta=new Vector2(0,440);
                Hand=Root.AddComponent<HandPresenter>(); Set(Hand,"_handRoot",slots.transform);
                Set(Hand,"_runtimeCanvas",canvas); Set(Hand,"_runtimeCanvasRect",Root.GetComponent<RectTransform>()); Set(Hand,"_runtimeHandRoot",rect);
                Driver=new GameObject("Driver"); Driver.SetActive(false); Driver.transform.SetParent(Root.transform);
                Screen=Driver.AddComponent<BattleScreenPresenter>(); Set(Screen,"_handPresenter",Hand);
                Boot=Driver.AddComponent<BattleBootstrapper>(); Set(Boot,"_battleScreenPresenter",Screen);
                Set(Boot,"_autoRunAiTurns",false); Set(Boot,"_animateAttackSequences",false); Set(Boot,"_sendPlayerActionsToOnlineGateway",false);
                Root.SetActive(true); Hand.SetExpanded(true);
            }
            internal SpellCardUseVfx[] All()=>Root.GetComponentsInChildren<SpellCardUseVfx>(true);
            internal SpellCardUseVfx[] Active()=>Root.GetComponentsInChildren<SpellCardUseVfx>().Where(v=>v.IsPlaying).ToArray();
            public void Dispose(){Hand.CancelSpellCardUses();Object.DestroyImmediate(Root);}
        }
    }
}
