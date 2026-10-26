using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Draft;
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
    public sealed class TemporaryReplicatePresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target,value);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethods(Hidden)
            .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length &&
                candidate.GetParameters().Select((parameter,index) => parameter.ParameterType.IsInstanceOfType(args[index])).All(matches => matches))
            .Invoke(target,args);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var preview in Resources.FindObjectsOfTypeAll<DeckCardPreviewOverlay>())
                if (!EditorUtility.IsPersistent(preview)) Object.DestroyImmediate(preview.gameObject);
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [TestCase(280f,392f)]
        [TestCase(600f,300f)]
        public void HourglassStaysInsideTopRightOfRenderedCardAndNeverInterceptsInput(float width, float height)
        {
            var node = new GameObject("HourglassTest", typeof(RectTransform), typeof(Image));
            var texture = new Texture2D(200,280);
            var sprite = Sprite.Create(texture,new Rect(0,0,200,280),new Vector2(.5f,.5f));
            try
            {
                var image = node.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true;
                image.rectTransform.sizeDelta = new Vector2(width,height);
                TemporaryReplicateHourglass marker = null;
                TemporaryReplicateHourglass.Present(ref marker,image,true);
                var bounds = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(image);
                var center = marker.rectTransform.anchoredPosition;
                Assert.That(center.x, Is.GreaterThan(bounds.center.x));
                Assert.That(center.y, Is.GreaterThan(bounds.center.y));
                Assert.That(bounds.Contains(center+marker.rectTransform.sizeDelta*.5f), Is.True);
                Assert.That(marker.raycastTarget, Is.False);
                Assert.That(marker.GetComponentsInChildren<Text>(), Is.Empty, "Icon has no turn badge or text.");
                var id = marker.GetInstanceID();
                TemporaryReplicateHourglass.Present(ref marker,image,false);
                Assert.That(marker.gameObject.activeSelf, Is.False);
                TemporaryReplicateHourglass.Present(ref marker,image,true);
                Assert.That(marker.GetInstanceID(), Is.EqualTo(id));
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void TemporaryPreviewExplainsExpiryAndOrdinaryCardClearsItsMarkerAndDescription()
        {
            var owner = new GameObject("TemporaryPreview");
            try
            {
                var hand = owner.AddComponent<HandCardView>(); hand.Present("Werewolf","temporary",true);
                DeckCardPreviewOverlay.ShowFor(hand,"Werewolf",null,null,handCard:hand);
                var preview = Resources.FindObjectsOfTypeAll<DeckCardPreviewOverlay>().Single();
                Assert.That(preview.GetComponentsInChildren<Text>().Any(text => text.text == "임시 복제"), Is.True);
                Assert.That(preview.GetComponentsInChildren<Text>().Any(text => text.text == "사용하지 않으면 이번 턴 종료 시 손패에서 사라집니다."), Is.True);
                Assert.That(preview.GetComponentsInChildren<TemporaryReplicateHourglass>().Length, Is.EqualTo(1));
                hand.Present("Werewolf","ordinary",false);
                DeckCardPreviewOverlay.ShowFor(hand,"Werewolf",null,null,handCard:hand);
                Assert.That(preview.GetComponentsInChildren<Text>().Any(text => text.text.Contains("이번 턴 종료")), Is.False);
                Assert.That(preview.GetComponentsInChildren<TemporaryReplicateHourglass>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void DissolveRemovesEdgesBeforeCenterAndUsesOnlyBoundedGreyDust()
        {
            var node = new GameObject("DissolveMesh",typeof(RectTransform));
            var graphic = node.AddComponent<TemporaryReplicateDissolveGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(240f,336f);
            var vertices = new List<UIVertex>();
            using (var mesh = new VertexHelper())
            {
                try
                {
                    graphic.Configure(null,Color.white,false); graphic.SetProgress(0f);
                    Call(graphic,"OnPopulateMesh",mesh); var initial = mesh.currentVertCount;
                    Assert.That(initial, Is.EqualTo(12*18*4));
                    graphic.SetProgress(.35f); Call(graphic,"OnPopulateMesh",mesh);
                    Assert.That(mesh.currentVertCount, Is.GreaterThan(0).And.LessThan(initial));
                    mesh.GetUIVertexStream(vertices);
                    Assert.That(vertices.Min(vertex => vertex.position.x), Is.GreaterThan(graphic.rectTransform.rect.xMin));
                    Assert.That(vertices.Max(vertex => vertex.position.x), Is.LessThan(graphic.rectTransform.rect.xMax));
                    graphic.Configure(null,Color.white,true); Call(graphic,"OnPopulateMesh",mesh);
                    vertices.Clear(); mesh.GetUIVertexStream(vertices);
                    Assert.That(mesh.currentVertCount, Is.GreaterThan(0).And.LessThanOrEqualTo(72*4));
                    Assert.That(vertices.All(vertex => vertex.color.r == vertex.color.g && vertex.color.g == vertex.color.b), Is.True);
                    graphic.SetProgress(1f); Call(graphic,"OnPopulateMesh",mesh);
                    Assert.That(mesh.currentVertCount, Is.Zero);
                }
                finally { Object.DestroyImmediate(node); }
            }
        }

        [UnityTest]
        public IEnumerator AcceptedExpiryUsesRuntimeIdentityKeepsSnapshotPositionAndRunsWhilePaused()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new HandFixture())
            {
                var state = f.State(); f.Hand.Present(state); yield return null;
                var temporary = f.Hand.HandCardViews[1];
                var artwork = temporary.GetComponent<HandCardTextView>().TutorialArtwork;
                Assert.That(temporary.GetComponentsInChildren<TemporaryReplicateHourglass>(), Is.Empty, "Runtime icon belongs to the detached artwork.");
                Assert.That(artwork.GetComponentsInChildren<TemporaryReplicateHourglass>().Length, Is.EqualTo(1));
                Assert.That(f.Hand.HandCardViews[0].GetComponent<HandCardTextView>().TutorialArtwork.GetComponentsInChildren<TemporaryReplicateHourglass>(), Is.Empty);
                f.Hand.QueueTemporaryReplicateExpiry(); f.Hand.QueueTemporaryReplicateExpiry();
                f.Hand.Present(state); // Old-state refresh during the preceding impact animation.
                Assert.That(f.Effects(), Is.Empty);
                state.Remove("Werewolf","temporary"); f.Hand.Present(state);
                var effect = f.Effects().Single(); var position = effect.transform.position;
                Assert.That(effect.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(state.CardIds.Count, Is.EqualTo(2));
                Assert.That(f.Hand.HandCardViews.Take(2).All(view => view.HasCard && !view.IsTemporaryReplicate), Is.True);
                f.Hand.Present(state); yield return null;
                Assert.That(f.Effects().Length, Is.EqualTo(1), "Duplicate refresh must not replay disappearance.");
                Assert.That(Vector3.Distance(position,effect.transform.position), Is.LessThan(.01f), "Recycled card slot moved the visual snapshot.");
                Assert.That(f.Hand.HandCardViews.Take(2).All(view => view.GetComponent<HandCardTextView>().TutorialArtwork.GetComponentsInChildren<TemporaryReplicateHourglass>().Length == 0), Is.True);
                var timeScale = Time.timeScale;
                try { Time.timeScale = 0f; yield return new WaitForSecondsRealtime(TemporaryReplicateDissolveView.Duration+.1f); }
                finally { Time.timeScale = timeScale; }
                Assert.That(f.Effects(), Is.Empty);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CardUseSnapshotAndReconnectResetDoNotInventExpiryAndDisableCleansUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new HandFixture())
            {
                var state = f.State(); f.Hand.Present(state); yield return null;
                state.Remove("Werewolf","temporary"); f.Hand.Present(state);
                Assert.That(f.Effects(), Is.Empty, "Playing a temporary card is not expiration.");
                state = f.State(); f.Hand.Present(state); yield return null;
                f.Hand.QueueTemporaryReplicateExpiry(); f.Hand.CancelTemporaryReplicateExpiry();
                state.Remove("Werewolf","temporary"); f.Hand.Present(state);
                Assert.That(f.Effects(), Is.Empty, "Reconnection snapshot must not replay an old pending cue.");
                state = f.State(); f.Hand.Present(state); yield return null;
                f.Hand.QueueTemporaryReplicateExpiry(); state.RemoveTemporaryReplicates(); f.Hand.Present(state);
                Assert.That(f.Effects().Length, Is.EqualTo(1));
                f.Hand.enabled = false; yield return null;
                Assert.That(f.Effects(), Is.Empty);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ProjectedTurnEndHandlesBothSeatsTimeoutsAndIgnoresOpponentOrDrawEvents()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new HandFixture())
            {
                var inactive = new GameObject("InactivePresentationDriver"); inactive.SetActive(false); inactive.transform.SetParent(f.Root.transform);
                var screen = inactive.AddComponent<BattleScreenPresenter>(); Set(screen,"_handPresenter",f.Hand);
                var bootstrapper = inactive.AddComponent<BattleBootstrapper>(); Set(bootstrapper,"_battleScreenPresenter",screen);
                var connection = inactive.AddComponent<OnlineBattleConnectionTester>();
                foreach (var seat in new[] { PlayerId.Player,PlayerId.AI })
                {
                    Set(connection,"_localPlayerId",seat);
                    var opponent = seat == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
                    var state = f.State(); f.Hand.Present(state); yield return null;
                    var remote = new List<BattleEventDto> { new BattleEventDto { EventType=BattleEventType.TurnEnded,SourceOwnerId=opponent } };
                    var projected = (IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",remote);
                    bootstrapper.PlayOnlineBattleEventAnimations(projected);
                    state.RemoveTemporaryReplicates(); f.Hand.Present(state);
                    Assert.That(f.Effects(), Is.Empty);
                    state = f.State(); f.Hand.Present(state); yield return null;
                    remote = new List<BattleEventDto> { new BattleEventDto { EventType=BattleEventType.TurnTimerExpired,SourceOwnerId=seat },
                        new BattleEventDto { EventType=BattleEventType.TurnEnded,SourceOwnerId=seat } };
                    projected = (IReadOnlyList<BattleEventDto>)Call(connection,"ProjectBattleEventsToLocalPerspective",remote);
                    bootstrapper.PlayOnlineBattleEventAnimations(projected);
                    state.RemoveTemporaryReplicates(); f.Hand.Present(state);
                    Assert.That(f.Effects().Length, Is.EqualTo(1));
                    bootstrapper.CancelCombatPresentation(); yield return null;
                    Assert.That(f.Effects(), Is.Empty);
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LocalAcceptedEndTurnExpiresOnlyTemporaryCopyThroughTheRealPresenter()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var f = new HandFixture())
            {
                var provider = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>("Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset").CreateProvider();
                var gateway = TutorialResourceBattle.Create(provider);
                gateway.ApplyMulligan(PlayerId.Player,new[] { "firebolt" },new TutorialResourceBattle.MulliganDeck()); gateway.ResolveTurnStart();
                var state = gateway.CurrentBattleState;
                foreach (var id in state.Player.Hand.CardIds.ToArray()) state.Player.Hand.Remove(id);
                state.Player.Hand.Add("Werewolf","ordinary",false); state.Player.Hand.Add("Werewolf","temporary",true);
                var inactive = new GameObject("LocalPresentationDriver"); inactive.SetActive(false); inactive.transform.SetParent(f.Root.transform);
                var screen = inactive.AddComponent<BattleScreenPresenter>(); Set(screen,"_handPresenter",f.Hand);
                var bootstrapper = inactive.AddComponent<BattleBootstrapper>(); Set(bootstrapper,"_battleScreenPresenter",screen);
                Set(bootstrapper,"_battleGateway",gateway); Set(bootstrapper,"_autoRunAiTurns",false); Set(bootstrapper,"_animateAttackSequences",false);
                screen.Present(state); yield return null;
                Assert.That(bootstrapper.CanPlayerEndTurn, Is.True);
                bootstrapper.EndTurn();
                Assert.That(state.Player.Hand.Contains("Werewolf","ordinary"), Is.True);
                Assert.That(state.Player.Hand.Contains("Werewolf","temporary"), Is.False);
                Assert.That(f.Effects().Length, Is.EqualTo(1));
            }
            yield return new ExitPlayMode();
        }

        private sealed class HandFixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly HandPresenter Hand;
            internal HandFixture()
            {
                Root = new GameObject("TemporaryHandFixture",typeof(RectTransform),typeof(Canvas)); Root.SetActive(false);
                var canvas = Root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var slots = new GameObject("LogicalSlots",typeof(RectTransform)); slots.transform.SetParent(Root.transform,false);
                var layout = new GameObject("RuntimeHandRoot",typeof(RectTransform)); layout.transform.SetParent(Root.transform,false);
                var rect = layout.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0f,0f); rect.anchorMax = new Vector2(1f,0f);
                rect.pivot = new Vector2(.5f,0f); rect.sizeDelta = new Vector2(0f,440f);
                Hand = Root.AddComponent<HandPresenter>(); Set(Hand,"_handRoot",slots.transform);
                Set(Hand,"_runtimeCanvas",canvas); Set(Hand,"_runtimeCanvasRect",Root.GetComponent<RectTransform>()); Set(Hand,"_runtimeHandRoot",rect);
                Root.SetActive(true); Hand.SetExpanded(true);
            }
            internal HandState State()
            {
                var state = new HandState(); state.Add("Werewolf","ordinary",false);
                state.Add("Werewolf","temporary",true); state.Add("Goblin","following",false); return state;
            }
            internal TemporaryReplicateDissolveView[] Effects() => Root.GetComponentsInChildren<TemporaryReplicateDissolveView>(true);
            public void Dispose() { Hand.CancelTemporaryReplicateExpiry(); Object.DestroyImmediate(Root); }
        }
    }
}
