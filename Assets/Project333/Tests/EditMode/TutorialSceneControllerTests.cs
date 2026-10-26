using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Startup;
using Project333.Runtime.Presentation.Tutorial;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Domain.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{

    public sealed class TutorialSceneControllerTests
    {
        private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        private readonly Dictionary<string, Scene> _borrowedScenes = new Dictionary<string, Scene>();
        private GameObject _host;
        private TutorialSceneController _controller;

        private TutorialSceneController Create(bool attachStartScene = true)
        {
            _host = new GameObject("TutorialSceneControllerTest");
            _controller = _host.AddComponent<TutorialSceneController>();
            _controller.ConfigurePracticeCatalog(AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath));
            _controller.ConfigureReturnAction(() => { });
            _controller.Initialize();
            if (attachStartScene) AttachCurrentScene();
            return _controller;
        }

        private Scene AttachCurrentScene()
        {
            var name = _controller.Session.PracticeBattleStarted ? "Battle_VSlice" :
                _controller.Session.IsDraftReady ? "Draft_VSlice" :
                _controller.Session.Chapter == 2 ? "DeckBuilding_VSlice" : "GameStart_VSlice";
            if (!_borrowedScenes.TryGetValue(name, out var scene))
            {
                // Native scene assets are borrowed in memory and are never saved by this fixture.
                scene = EditorSceneManager.OpenScene("Assets/" + name + ".unity", OpenSceneMode.Additive);
                _borrowedScenes.Add(name, scene);
            }
            _controller.AttachPracticeScene(scene);
            Canvas.ForceUpdateCanvases();
            Assert.That(_controller.IsPracticeSceneReady, Is.True);
            Assert.That(_controller.PracticeSceneName, Is.EqualTo(name));
            return scene;
        }

        private static IEnumerable<T> InScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        private T PracticeComponent<T>() where T : Component =>
            InScene<T>(_borrowedScenes[_controller.PracticeSceneName]).First();
        private static T Field<T>(object target, string field) =>
            (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private Button Button(string name) => _host.GetComponentsInChildren<Button>(true).First(x => x.name == name);
        private Button RealStartButton() => PracticeComponent<GameStartSceneController>()
            .TutorialStartButtonRect.GetComponent<Button>();
        private GameObject Offer(int index = 0) => PracticeComponent<DraftOverlayPresenter>()
            .TutorialOfferRects[index].gameObject;
        private Text RealDeckText() => Field<Text>(PracticeComponent<DraftOverlayPresenter>(), "_deckListText");
        private RectTransform[] VisiblePracticeAuras()
        {
            Canvas.ForceUpdateCanvases();
            typeof(TutorialSceneController).GetMethod("RefreshPracticeHighlightLayouts", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_controller, null);
            typeof(TutorialSceneController).GetMethod("RefreshPracticeDialogueLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_controller, null);
            return _host.GetComponentsInChildren<RectTransform>(true)
                .Where(x => x.name.StartsWith("PracticeAura_", StringComparison.Ordinal) && x.gameObject.activeInHierarchy).ToArray();
        }
        private void AssertPracticeFeedback(int targetCount)
        {
            Assert.That(Button("NextButton").gameObject.activeSelf, Is.EqualTo(targetCount == 0),
                "Player practice has no Next button; completing it restores normal dialogue navigation.");
            var auras = VisiblePracticeAuras();
            Assert.That(auras.Length, Is.EqualTo(targetCount));
            foreach (var aura in auras)
            {
                Assert.That(aura.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(aura.GetComponentsInChildren<Image>().All(x => !x.raycastTarget && x.color.g > x.color.r), Is.True,
                    "Green border guidance must allow the actual control to receive the gesture.");
            }
        }
        [TestCase(-15f, .31f)]
        [TestCase(15f, 1f)]
        public void CardGuidanceFollowsAllFourCornersDuringRotationAndScaling(float angle, float scale)
        {
            Create(); SelectChapterTwo(); AdvanceToAction(_controller, TutorialAction.PreviewOffer);
            var art = PracticeComponent<DraftOverlayPresenter>().GetTutorialOfferArtwork(0);
            art.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            art.rectTransform.localScale = Vector3.one * scale;
            var aura = VisiblePracticeAuras().Single(x => x.name == "PracticeAura_0");
            var spriteRect = Project333.Runtime.Presentation.Hand.HandCardStatOverlayLayout.CalculateRenderedSpriteRect(art);
            var expected = new[] { spriteRect.min, new Vector2(spriteRect.xMin, spriteRect.yMax),
                spriteRect.max, new Vector2(spriteRect.xMax, spriteRect.yMin) };
            var actual = new Vector3[4]; aura.GetWorldCorners(actual);
            for (var i = 0; i < actual.Length; i++)
                Assert.That(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, actual[i]),
                    RectTransformUtility.WorldToScreenPoint(null, art.rectTransform.TransformPoint(expected[i]))),
                    Is.LessThan(2f), "Aura lost rotated/scaled card corner " + i);
            Assert.That(aura.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        [Test]
        public void HeldDeckBuildingPreviewAndTooltipsRenderAbovePracticeBorders()
        {
            var controller = Create(); SelectChapterTwo(); AdvanceToAction(controller, TutorialAction.PreviewOffer);
            var target = Offer(); var step = controller.Session.StepIndex;
            var data = Press(target); AdvanceHold(target, .5f);
            var preview = Resources.FindObjectsOfTypeAll<DeckCardPreviewOverlay>().Single(x => x.gameObject.activeInHierarchy);
            var previewCanvas = preview.GetComponent<Canvas>();
            var auras = VisiblePracticeAuras(); Assert.That(auras.Length, Is.EqualTo(3));
            foreach (var aura in auras)
            {
                var canvas = aura.GetComponentInParent<Canvas>();
                Assert.That(canvas.sortingLayerID, Is.EqualTo(previewCanvas.sortingLayerID));
                Assert.That(canvas.sortingOrder, Is.LessThan(previewCanvas.sortingOrder), "Guidance must not cover held card tooltips.");
            }
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(HoldComponent(target)), Is.False);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step + 1));
            Assert.That(controller.Session.ManualPickCount, Is.Zero);
        }

        private static object[] RealAccountState() => new object[]
        {
            AccountSessionState.Tickets, AccountSessionState.ResourceGold,
            AccountSessionState.ActiveRunId, AccountSessionState.ActiveRunStatus,
            AccountSessionState.ActiveRunWins, AccountSessionState.ActiveRunLosses,
            AccountSessionState.ActiveDeckId, AccountSessionState.ActiveDeckCardCount,
            AccountSessionState.ActiveDraftPicks.ToArray(), AccountSessionState.ActiveDraftOffer.ToArray(),
        };

        private static PointerEventData Press(GameObject target, Vector2? position = null)
        {
            var rect = target.GetComponent<RectTransform>();
            var point = position ?? RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(null)
            {
                pointerId = 1, button = PointerEventData.InputButton.Left,
                eligibleForClick = true, position = point, pressPosition = point,
            };
            // EditMode does not run this runtime audio component's Awake.
            // Keep its unrelated sound playback out of native input simulations.
            var sound = target.GetComponent<ButtonTouchSound>();
            var soundEnabled = sound != null && sound.enabled;
            if (sound != null) sound.enabled = false;
            try { ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler); }
            finally { if (sound != null) sound.enabled = soundEnabled; }
            return data;
        }

        private static Component HoldComponent(GameObject target) =>
            (Component)target.GetComponent<CardHoldPreview>() ?? target.GetComponent<DeckListCardHoldPreview>();
        private static void AdvanceHold(GameObject target, float seconds)
        {
            var hold = HoldComponent(target);
            Assert.That(hold, Is.Not.Null, "Practice must use the real card/list hold component.");
            var type = hold.GetType();
            var started = (float)type.GetField("_pressedAt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hold);
            type.GetMethod("AdvanceHold", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hold, new object[] { started + seconds });
        }

        private static PointerEventData HoldRelease(GameObject target, Vector2? position = null)
        {
            var data = Press(target, position);
            AdvanceHold(target, .5f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(HoldComponent(target)), Is.True);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            return data;
        }

        private static void ShortTap(GameObject target, Vector2? position = null)
        {
            var data = Press(target, position);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            if (data.eligibleForClick) ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        }

        private void SelectChapterTwo()
        {
            Button("TutorialSelectionButton").onClick.Invoke();
            Button("Chapter2").onClick.Invoke();
            AttachCurrentScene();
        }

        private void AdvanceToAction(TutorialSceneController controller, TutorialAction action)
        {
            var remaining = 40;
            while (!controller.Session.WaitingForAction && remaining-- > 0) Button("NextButton").onClick.Invoke();
            Assert.That(controller.Session.CurrentStep.RequiredAction, Is.EqualTo(action));
        }

        private Vector2 PointForDeckCard(Text text, string cardId)
        {
            Assert.That(_controller.TryGetCardDefinitionAsset(cardId, out var card), Is.True);
            var scroll = text.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            var relay = text.GetComponent<DeckListCardHoldPreview>();
            Assert.That(relay.TryGetCardLocalBounds(cardId, out var bounds), Is.True);
            var local = bounds.center;
            var point = RectTransformUtility.WorldToScreenPoint(null, text.rectTransform.TransformPoint(local));
            if (scroll != null && scroll.viewport != null)
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, null), Is.True,
                    "The practice gesture must target a visible native deck row.");
            return point;
        }

        [TearDown]
        public void Cleanup()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            foreach (var overlay in Resources.FindObjectsOfTypeAll<DeckCardPreviewOverlay>())
                if (!EditorUtility.IsPersistent(overlay)) Object.DestroyImmediate(overlay.gameObject);
            foreach (var scene in _borrowedScenes.Values)
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            _borrowedScenes.Clear();
            TutorialRuntimeIsolation.CancelEntry();
        }

        [Test]
        public void InitializationIsIdempotentAndWaitsForTheNativeSceneBeforeAdvancing()
        {
            var controller = Create(attachStartScene: false);
            controller.Initialize();
            Assert.That(_host.GetComponentsInChildren<Canvas>().Length, Is.EqualTo(1));
            Assert.That(controller.IsPracticeSceneReady, Is.False);
            Assert.That(Button("NextButton").interactable, Is.False);
            Button("NextButton").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.Zero);
            AttachCurrentScene();
            Button("NextButton").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(1));
            Assert.That(_host.GetComponentsInChildren<Text>().All(t => t.font != null), Is.True);
            Assert.That(_host.GetComponentsInChildren<GameStartSceneController>(), Is.Empty);
            Assert.That(_host.GetComponentsInChildren<DraftOverlayPresenter>(), Is.Empty);
        }

        [Test]
        public void NativeStartButtonRequiresThePracticeActionAndEntersNativeDeckBuildingLocally()
        {
            var realAccount = RealAccountState();
            var controller = Create();
            var start = PracticeComponent<GameStartSceneController>();
            var button = RealStartButton();
            Assert.That(button.gameObject.scene.name, Is.EqualTo("GameStart_VSlice"));
            Assert.That(Field<Image>(start, "_backgroundImage").sprite, Is.Not.Null);
            Assert.That(Field<Image>(start, "_startButtonImage").sprite, Is.Not.Null);
            for (var i = 0; i < TutorialSession.SecondChapterStep - 2; i++) Button("NextButton").onClick.Invoke();
            Assert.That(button.interactable, Is.True, "The real start button retains its ordinary appearance.");
            Assert.That(Field<Text>(start, "_ticketText").GetComponent<AccountWalletView>().TicketValue.text,
                Is.EqualTo("3"));
            Assert.That(Field<Button>(start, "_shopButton").interactable, Is.True);
            Assert.That(Field<Button>(start, "_ownedCardsButton").interactable, Is.True);
            Field<Button>(start, "_shopButton").onClick.Invoke();
            Field<Button>(start, "_ownedCardsButton").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(TutorialSession.SecondChapterStep - 2));
            Assert.That(RealAccountState(), Is.EqualTo(realAccount));
            button.onClick.Invoke();
            Assert.That(controller.Session.TutorialRunStarted, Is.False);
            Button("NextButton").onClick.Invoke();
            Assert.That(Button("NextButton").interactable, Is.False);
            AssertPracticeFeedback(1);
            Button("NextButton").onClick.Invoke();
            Assert.That(controller.Session.Chapter, Is.EqualTo(1));
            ShortTap(button.gameObject);
            Assert.That(controller.Session.Chapter, Is.EqualTo(2));
            Assert.That(controller.Session.IsComplete, Is.False);
            Assert.That(controller.Session.PracticeTickets, Is.Zero);
            Assert.That(controller.IsPracticeSceneReady, Is.False);
            Assert.Throws<ArgumentException>(() => controller.AttachPracticeScene(_borrowedScenes["GameStart_VSlice"]));
            AttachCurrentScene();
            Assert.That(PracticeComponent<DraftOverlayPresenter>().TutorialOfferRects.Count, Is.EqualTo(3));
            Assert.That(Offer().gameObject.scene.name, Is.EqualTo("DeckBuilding_VSlice"));
            Assert.That(_host.GetComponentsInChildren<CardHoldPreview>(), Is.Empty);
            Assert.That(RealAccountState(), Is.EqualTo(realAccount));
        }

        [Test]
        public void OfferPracticeRequiresHoldAndReleaseAndDoesNotSelectFromTheSamePress()
        {
            var controller = Create(); SelectChapterTwo(); AdvanceToAction(controller, TutorialAction.PreviewOffer);
            AssertPracticeFeedback(3);
            var step = controller.Session.StepIndex;
            ShortTap(Offer()); Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            var target = Offer(); var data = Press(target); AdvanceHold(target, .5f);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step), "Opening alone is not completed practice.");
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            Assert.That(data.eligibleForClick, Is.False, "The release must never fall through to a card pick.");
            Assert.That(controller.Session.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.PickCard));
            AssertPracticeFeedback(3);
            Assert.That(controller.Session.ManualPickCount, Is.Zero);
            HoldRelease(Offer()); Assert.That(controller.Session.ManualPickCount, Is.Zero);
            ShortTap(Offer()); Assert.That(controller.Session.ManualPickCount, Is.EqualTo(1));
            AssertPracticeFeedback(0);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CanceledOfferHoldAndDifferentFingerCannotCompletePractice(bool move)
        {
            var controller = Create(); SelectChapterTwo(); AdvanceToAction(controller, TutorialAction.PreviewOffer);
            var step = controller.Session.StepIndex; var target = Offer(); var data = Press(target); AdvanceHold(target, .5f);
            var stranger = new PointerEventData(null) { pointerId = 2, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target, stranger, ExecuteEvents.pointerUpHandler);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(target.GetComponent<CardHoldPreview>()), Is.True);
            if (move)
            { data.position += new Vector2(200, 200); ExecuteEvents.Execute(target, data, ExecuteEvents.pointerMoveHandler); }
            else ExecuteEvents.Execute(target, data, ExecuteEvents.pointerExitHandler);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Assert.That(controller.Session.ManualPickCount, Is.Zero);
        }

        [Test]
        public void NativeDeckHoldThenFivePicksAndContinueReachTheCompletedNativeDraftScene()
        {
            var realAccount = RealAccountState();
            var controller = Create(); SelectChapterTwo(); AdvanceToAction(controller, TutorialAction.PreviewOffer);
            HoldRelease(Offer()); ShortTap(Offer()); AdvanceToAction(controller, TutorialAction.PreviewDeck);
            AssertPracticeFeedback(1);
            var text = RealDeckText();
            var point = PointForDeckCard(text, controller.Session.DraftCardIds[0]);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(VisiblePracticeAuras()[0], point, null), Is.True,
                "Deck guidance surrounds the chosen native card row, including its hold target.");
            var step = controller.Session.StepIndex;
            ShortTap(text.gameObject, point); Button("NextButton").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            var release = HoldRelease(text.gameObject, point);
            Assert.That(release.eligibleForClick, Is.False);
            AssertPracticeFeedback(0);
            AdvanceToAction(controller, TutorialAction.PickCard);
            for (var i = 1; i < 5; i++) { AssertPracticeFeedback(3); ShortTap(Offer(i % 3)); }
            AssertPracticeFeedback(0);
            Assert.That(controller.Session.ManualPickCount, Is.EqualTo(5));
            Assert.That(controller.Session.DraftCardIds.Count, Is.EqualTo(5));
            AdvanceToAction(controller, TutorialAction.CompleteDeck);
            Assert.That(Button("NextButton").GetComponentInChildren<Text>().text, Is.EqualTo("계속"));
            ShortTap(Button("NextButton").gameObject);
            Assert.That(controller.Session.IsDraftReady, Is.True);
            Assert.That(controller.Session.DraftCardIds.Count, Is.EqualTo(33));
            Assert.That(controller.IsPracticeSceneReady, Is.False);
            AttachCurrentScene();
            var completedDraft = PracticeComponent<DraftSceneController>();
            Assert.That(completedDraft.TutorialDeckListText.text, Does.Contain("x "));
            Assert.That(completedDraft.TutorialDeckListText.GetComponent<DeckListCardHoldPreview>(), Is.Not.Null);
            Field<Button>(completedDraft, "_startPveBattleButton").onClick.Invoke();
            Field<Button>(completedDraft, "_startPvpBattleButton").onClick.Invoke();
            Assert.That(controller.Session.IsDraftReady, Is.True);
            Assert.That(controller.PracticeSceneName, Is.EqualTo("Draft_VSlice"));
            Assert.That(RealAccountState(), Is.EqualTo(realAccount), "Future mode controls cannot start a real run.");
            Assert.That(Button("NextButton").gameObject.activeSelf, Is.True);
            Assert.That(Button("NextButton").GetComponentInChildren<Text>().text, Is.EqualTo("다음"));
            var deck = controller.Session.DraftCardIds.ToArray();
            ShortTap(Button("NextButton").gameObject);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(TutorialSession.ThirdChapterStep));
            Assert.That(controller.Session.Chapter, Is.EqualTo(3));
            Assert.That(controller.Session.DraftCardIds, Is.EqualTo(deck));
            Assert.That(controller.PracticeSceneName, Is.EqualTo("Draft_VSlice"));
            Assert.That(_borrowedScenes.Count, Is.EqualTo(3), "Chapter three keeps the completed native draft lobby.");
        }

        [Test]
        public void SelectionCancelsHeldPreviewAndStartsOnlyImplementedChaptersWithFreshState()
        {
            var controller = Create(); SelectChapterTwo(); AdvanceToAction(controller, TutorialAction.PreviewOffer);
            var target = Offer(); var data = Press(target); AdvanceHold(target, .5f);
            Button("TutorialSelectionButton").onClick.Invoke();
            Assert.That(VisiblePracticeAuras(), Is.Empty, "Practice guidance is hidden while selecting a chapter.");
            Button("CloseSelectionButton").onClick.Invoke();
            AssertPracticeFeedback(3);
            Button("TutorialSelectionButton").onClick.Invoke();
            var step = controller.Session.StepIndex;
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            Button("NextButton").onClick.Invoke(); Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(target.GetComponent<CardHoldPreview>()), Is.False);
            Assert.That(Button("Chapter1").interactable && Button("Chapter2").interactable, Is.True);
            Assert.That(Button("Chapter3").interactable, Is.True);
            Assert.That(Button("Chapter4").interactable && Button("Chapter5").interactable, Is.True);
            Assert.That(Button("Chapter6").interactable, Is.True);
            Button("Chapter6").onClick.Invoke(); Assert.That(controller.Session.StepIndex, Is.EqualTo(TutorialSession.SixthChapterStep));
            Assert.That(controller.Session.CurrentStep.Cue, Is.EqualTo(TutorialCue.ShopIntro));
            Button("TutorialSelectionButton").onClick.Invoke();
            Button("Chapter2").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(TutorialSession.SecondChapterStep));
            Assert.That(controller.Session.DraftCardIds, Is.Empty);
            Assert.That(controller.IsPracticeSceneReady, Is.True);
            Button("TutorialSelectionButton").onClick.Invoke(); Button("Chapter1").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(TutorialSession.FirstChapterStep));
            Assert.That(controller.Session.PracticeTickets, Is.EqualTo(3));
            Assert.That(controller.IsPracticeSceneReady, Is.False);
            AttachCurrentScene();
            Assert.That(_borrowedScenes.Count, Is.EqualTo(2));
        }

        private void SelectChapterThree()
        {
            Button("TutorialSelectionButton").onClick.Invoke();
            Button("Chapter3").onClick.Invoke();
            AttachCurrentScene();
        }

        [Test]
        public void ThirdChapterUsesTheNativeDeckHoldAndOnlyHighlightedRowReleaseCompletesPractice()
        {
            var account = RealAccountState();
            var controller = Create(); SelectChapterThree();
            Assert.That(controller.Session.DraftCardIds.Count, Is.EqualTo(33));
            AdvanceToAction(controller, TutorialAction.PreviewDeck);
            AssertPracticeFeedback(1);
            var draft = PracticeComponent<DraftSceneController>();
            var text = draft.TutorialDeckListText;
            var point = PointForDeckCard(text, controller.Session.DraftCardIds[0]);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(VisiblePracticeAuras()[0], point, null), Is.True);
            var step = controller.Session.StepIndex;
            ShortTap(text.gameObject, point);
            Button("NextButton").onClick.Invoke();
            draft.StartPveBattleFromUi(); draft.StartPvpBattleFromUi();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            var otherPoint = PointForDeckCard(text, controller.Session.DraftCardIds[1]);
            HoldRelease(text.gameObject, otherPoint);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            point = PointForDeckCard(text, controller.Session.DraftCardIds[0]);
            var data = Press(text.gameObject, point); AdvanceHold(text.gameObject, .5f);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Button("TutorialSelectionButton").onClick.Invoke();
            ExecuteEvents.Execute(text.gameObject, data, ExecuteEvents.pointerUpHandler);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Button("CloseSelectionButton").onClick.Invoke();
            HoldRelease(text.gameObject, point);
            Assert.That(controller.Session.CurrentStep.Cue, Is.EqualTo(TutorialCue.PvpMode));
            Assert.That(Button("NextButton").gameObject.activeSelf, Is.True);
            Assert.That(RealAccountState(), Is.EqualTo(account));
        }

        [Test]
        public void ModeExplanationsShowRealTargetsQuotaExampleAndReconnectWithoutLaunchingOnline()
        {
            var controller = Create(); SelectChapterThree();
            AdvanceToAction(controller, TutorialAction.PreviewDeck);
            var draft = PracticeComponent<DraftSceneController>();
            var text = draft.TutorialDeckListText;
            HoldRelease(text.gameObject, PointForDeckCard(text, controller.Session.DraftCardIds[0]));
            for (var remaining = 30; !controller.Session.WaitingForAction && remaining-- > 0;)
            {
                var cue = controller.Session.CurrentStep.Cue;
                var step = controller.Session.StepIndex;
                var auras = VisiblePracticeAuras();
                Assert.That(auras.Length, Is.EqualTo(1), cue.ToString());
                Assert.That(Button("NextButton").gameObject.activeSelf, Is.True);
                var label = Field<Text>(draft, "_startPveBattleButtonLabel").text;
                Assert.That(label, Does.Contain(cue == TutorialCue.GoldDungeonRemaining ? "1/3" : "3/3"));
                Assert.That(Field<Text>(draft, "_startPvpBattleButtonLabel").text,
                    Is.EqualTo(cue == TutorialCue.Reconnect ? "재접속 (70초)" : "전투"));
                draft.StartPveBattleFromUi(); draft.StartPvpBattleFromUi();
                Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
                Assert.That(controller.Session.PracticeGoldDungeonRemaining, Is.EqualTo(3));
                var panel = _host.GetComponentsInChildren<RectTransform>().First(x => x.name == "DialoguePanel");
                var guide = _host.GetComponentsInChildren<Image>().First(x => x.name == "GuidePortrait");
                Assert.That(ScreenRect(panel).Overlaps(ScreenRect(auras[0])), Is.False, "Dialogue covers " + cue);
                Assert.That(ScreenRect(guide.rectTransform).Overlaps(ScreenRect(auras[0])), Is.False, "Guide covers " + cue);
                Button("NextButton").onClick.Invoke();
            }
            Assert.That(controller.Session.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.StartGoldDungeon));
            AssertPracticeFeedback(1);
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return new Rect(min, max - min);
        }

        [Test]
        public void GoldDungeonPracticeOpensDeterministicLocalBattleAndReplayLeavesRealRunUntouched()
        {
            var account = RealAccountState();
            var runFields = typeof(DraftRunSessionState).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .Where(f => !f.IsLiteral).ToDictionary(f => f, f => f.GetValue(null));
            var controller = Create(); SelectChapterThree();
            AdvanceToAction(controller, TutorialAction.PreviewDeck);
            var draft = PracticeComponent<DraftSceneController>();
            var text = draft.TutorialDeckListText;
            HoldRelease(text.gameObject, PointForDeckCard(text, controller.Session.DraftCardIds[0]));
            AdvanceToAction(controller, TutorialAction.StartGoldDungeon);
            var gold = draft.TutorialGoldDungeonButtonRect.GetComponent<Button>();
            AssertPracticeFeedback(1);
            var step = controller.Session.StepIndex;
            Button("NextButton").onClick.Invoke();
            draft.StartPvpBattleFromUi();
            Button("TutorialSelectionButton").onClick.Invoke();
            gold.onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Button("CloseSelectionButton").onClick.Invoke();
            ShortTap(gold.gameObject);
            Assert.That(controller.Session.IsComplete, Is.False);
            Assert.That(controller.Session.PracticeGoldDungeonRemaining, Is.EqualTo(2));
            Assert.That(controller.IsPracticeSceneReady, Is.False);
            gold.onClick.Invoke();
            AttachCurrentScene();
            var battle = PracticeComponent<BattleBootstrapper>();
            Assert.That(Field<IBattleGateway>(battle, "_battleGateway"), Is.TypeOf<LocalBattleGateway>());
            Assert.That(battle.OnlineConnectionTester, Is.Null);
            Assert.That(battle.CurrentBattleState.ActivePlayerId, Is.EqualTo(PlayerId.Player));
            Assert.That(battle.CurrentBattleState.Phase, Is.EqualTo(PhaseType.TurnStart));
            Assert.That(battle.CurrentBattleState.Player.Master.CurrentHp, Is.EqualTo(333));
            Assert.That(battle.CurrentBattleState.Player.Hand.Count, Is.EqualTo(5));
            var hand = battle.CurrentBattleState.Player.Hand.CardIds.ToArray();
            var state = battle.CurrentBattleState;
            controller.AttachPracticeScene(_borrowedScenes["Battle_VSlice"]);
            Assert.That(battle.CurrentBattleState, Is.SameAs(state), "Refresh must not restart entry.");
            Assert.That(Button("NextButton").GetComponentInChildren<Text>().text, Is.EqualTo("다음"));
            ShortTap(Button("NextButton").gameObject);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(TutorialSession.FourthChapterStep));
            Assert.That(battle.CurrentBattleState.Phase, Is.EqualTo(PhaseType.Mulligan));
            // Re-enter chapter three through the real selector instead of ending the whole lesson.
            
            SelectChapterThree();
            AdvanceToAction(controller, TutorialAction.PreviewDeck);
            draft = PracticeComponent<DraftSceneController>(); text = draft.TutorialDeckListText;
            HoldRelease(text.gameObject, PointForDeckCard(text, controller.Session.DraftCardIds[0]));
            AdvanceToAction(controller, TutorialAction.StartGoldDungeon);
            draft.StartPveBattleFromUi(); AttachCurrentScene();
            Assert.That(PracticeComponent<BattleBootstrapper>().CurrentBattleState.Player.Hand.CardIds, Is.EqualTo(hand));
            Assert.That(RealAccountState(), Is.EqualTo(account));
            foreach (var entry in runFields) Assert.That(entry.Key.GetValue(null), Is.EqualTo(entry.Value), entry.Key.Name);
        }

        private Text DialogueText() => _host.GetComponentsInChildren<Text>(true).Single(x => x.name == "DialogueText");

        [Test]
        public void PreviousReviewsVisitedDialogueWithoutRewindingOrSkippingTheLesson()
        {
            var controller = Create();
            var first = DialogueText().text;
            Assert.That(Button("PreviousButton").interactable, Is.False);
            Button("PreviousButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(first));
            Button("NextButton").onClick.Invoke(); var second = DialogueText().text;
            Button("NextButton").onClick.Invoke(); var current = DialogueText().text;
            var step = controller.Session.StepIndex;
            Button("PreviousButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(second));
            Button("PreviousButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(first));
            Assert.That(Button("PreviousButton").interactable, Is.False);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Button("NextButton").onClick.Invoke(); Button("NextButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(current));
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Button("NextButton").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step + 1));
        }

        [Test]
        public void ReviewingARequiredPracticeBlocksInputAndRestoresItsHiddenNextAndAuras()
        {
            var controller = Create(); SelectChapterTwo(); AdvanceToAction(controller, TutorialAction.PreviewOffer);
            HoldRelease(Offer()); // The actual next action is selecting a card.
            Assert.That(controller.Session.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.PickCard));
            var current = DialogueText().text; var step = controller.Session.StepIndex;
            Button("PreviousButton").onClick.Invoke();
            Assert.That(Button("NextButton").gameObject.activeSelf, Is.True);
            Assert.That(VisiblePracticeAuras(), Is.Empty);
            var blocker = _host.GetComponentsInChildren<TutorialPracticeInputBlocker>(true).Single();
            Assert.That(blocker.gameObject.activeSelf, Is.True);
            Assert.That(blocker.IsRaycastLocationValid(Vector2.zero, null), Is.True);
            ShortTap(Offer()); HoldReleaseWhileReviewing();
            Assert.That(controller.Session.ManualPickCount, Is.Zero);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Button("NextButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(current));
            AssertPracticeFeedback(3);
            Assert.That(blocker.gameObject.activeSelf, Is.False);
            Button("NextButton").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step), "Review must not complete an unperformed action.");
            ShortTap(Offer());
            Assert.That(controller.Session.ManualPickCount, Is.EqualTo(1));
        }

        private void HoldReleaseWhileReviewing()
        {
            var target = Offer(); var data = Press(target); AdvanceHold(target, .5f);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        }

        [Test]
        public void ReviewingCompletedCardSelectionDoesNotAddAnotherCardOrResetOffers()
        {
            var account = RealAccountState();
            var controller = Create(); SelectChapterTwo(); AdvanceToAction(controller, TutorialAction.PreviewOffer);
            HoldRelease(Offer()); ShortTap(Offer());
            var cards = controller.Session.DraftCardIds.ToArray();
            var offers = controller.Session.CurrentOfferCardIds.ToArray();
            var step = controller.Session.StepIndex; var current = DialogueText().text;
            Button("PreviousButton").onClick.Invoke();
            ShortTap(Offer());
            Button("NextButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(current));
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Assert.That(controller.Session.ManualPickCount, Is.EqualTo(1));
            Assert.That(controller.Session.DraftCardIds, Is.EqualTo(cards));
            Assert.That(controller.Session.CurrentOfferCardIds, Is.EqualTo(offers));
            Assert.That(RealAccountState(), Is.EqualTo(account));
        }

        [Test]
        public void PreviousAcrossChapterBoundaryKeepsCurrentSceneAndSpentPracticeTickets()
        {
            var controller = Create(); AdvanceToAction(controller, TutorialAction.StartRun);
            var instruction = DialogueText().text;
            RealStartButton().onClick.Invoke();
            Assert.That(Button("PreviousButton").interactable, Is.False, "Do not navigate during additive scene loading.");
            AttachCurrentScene();
            var scene = controller.PracticeSceneName;
            var step = controller.Session.StepIndex;
            Button("PreviousButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(instruction));
            Assert.That(controller.PracticeSceneName, Is.EqualTo(scene));
            Assert.That(controller.IsPracticeSceneReady, Is.True);
            Assert.That(controller.Session.PracticeTickets, Is.Zero);
            Button("NextButton").onClick.Invoke();
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Assert.That(controller.Session.PracticeTickets, Is.Zero);
        }

        [Test]
        public void ChapterChoiceClearsReviewHistoryAndModalBlocksPrevious()
        {
            var controller = Create(); Button("NextButton").onClick.Invoke(); Button("NextButton").onClick.Invoke();
            var current = DialogueText().text;
            Button("TutorialSelectionButton").onClick.Invoke();
            Button("PreviousButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(current));
            Button("Chapter2").onClick.Invoke(); AttachCurrentScene();
            Assert.That(Button("PreviousButton").interactable, Is.False);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(TutorialSession.SecondChapterStep));
            Button("NextButton").onClick.Invoke();
            Button("PreviousButton").onClick.Invoke();
            Button("TutorialSelectionButton").onClick.Invoke(); Button("Chapter1").onClick.Invoke(); AttachCurrentScene();
            Assert.That(Button("PreviousButton").interactable, Is.False);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(TutorialSession.FirstChapterStep));
        }

        [Test]
        public void ReviewingChapterOneRestoresTheVisualAidForTheDisplayedDialogue()
        {
            var controller = Create();
            while (controller.Session.CurrentStep.Cue != TutorialCue.Resources) Button("NextButton").onClick.Invoke();
            var resources = _host.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "CivilizationResources");
            var gold = _host.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "GoldSubstitution");
            Button("NextButton").onClick.Invoke();
            Assert.That(gold.gameObject.activeSelf, Is.True);
            Button("PreviousButton").onClick.Invoke();
            Assert.That(resources.gameObject.activeSelf, Is.True);
            Assert.That(gold.gameObject.activeSelf, Is.False);
            Button("NextButton").onClick.Invoke();
            Assert.That(resources.gameObject.activeSelf, Is.False);
            Assert.That(gold.gameObject.activeSelf, Is.True);
        }

        [TestCase(500f, 230f)]
        [TestCase(1600f, 160f)]
        public void PreviousSitsLeftOfNextWithoutCoveringDialogue(float width, float height)
        {
            Create();
            var panel = (RectTransform)DialogueText().transform.parent;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(width, height);
            typeof(TutorialSceneController).GetMethod("RefreshDialogueNavigationLayout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, null);
            var previous = Button("PreviousButton").GetComponent<RectTransform>();
            var next = Button("NextButton").GetComponent<RectTransform>();
            Rect Bounds(RectTransform child)
            {
                var corners = new Vector3[4]; child.GetWorldCorners(corners);
                var min = (Vector2)panel.InverseTransformPoint(corners[0]);
                var max = (Vector2)panel.InverseTransformPoint(corners[2]);
                return new Rect(min, max-min);
            }
            var left = Bounds(previous); var right = Bounds(next); var text = Bounds(DialogueText().rectTransform);
            Assert.That(left.xMax, Is.LessThan(right.xMin));
            Assert.That(text.Overlaps(left) || text.Overlaps(right), Is.False);
            Assert.That(panel.rect.Contains(left.min) && panel.rect.Contains(right.max), Is.True);
            Assert.That(left.height, Is.GreaterThanOrEqualTo(44f));
            Assert.That(text.height, Is.GreaterThan(45f));
        }

        [Test]
        public void BattleReviewPreservesMulliganAndCancellationDoesNotAccumulateHistory()
        {
            var controller = Create();
            Button("TutorialSelectionButton").onClick.Invoke(); Button("Chapter4").onClick.Invoke(); AttachCurrentScene();
            var battle = PracticeComponent<BattleBootstrapper>();
            Button("NextButton").onClick.Invoke();
            var state = battle.CurrentBattleState;
            var hand = state.Player.Hand.CardIds.ToArray();
            var step = controller.Session.StepIndex;
            var instruction = DialogueText().text;
            Button("PreviousButton").onClick.Invoke();
            var intro = DialogueText().text;
            battle.TutorialMulliganSelectionChanged(true);
            Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            Button("NextButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(instruction));
            Assert.That(battle.CurrentBattleState, Is.SameAs(state));
            Assert.That(state.Player.Hand.CardIds, Is.EqualTo(hand));
            Assert.That(state.Phase, Is.EqualTo(PhaseType.Mulligan));
            for (var i = 0; i < 3; i++)
            {
                battle.TutorialMulliganSelectionChanged(true);
                Assert.That(controller.Session.CurrentStep.RequiredAction, Is.EqualTo(TutorialAction.ConfirmMulligan));
                battle.TutorialMulliganSelectionChanged(false);
                Assert.That(controller.Session.StepIndex, Is.EqualTo(step));
            }
            Button("PreviousButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(intro));
            Assert.That(Button("PreviousButton").interactable, Is.False);
        }

        [Test]
        public void BattleResolutionLocksPreviousUntilTheActionCompletes()
        {
            var controller = Create();
            Button("TutorialSelectionButton").onClick.Invoke(); Button("Chapter4").onClick.Invoke(); AttachCurrentScene();
            Button("NextButton").onClick.Invoke();
            var battle = PracticeComponent<BattleBootstrapper>(); var instruction = DialogueText().text;
            var resolving = typeof(BattleBootstrapper).GetField("_tutorialResolving", BindingFlags.Instance | BindingFlags.NonPublic);
            var refresh = typeof(TutorialSceneController).GetMethod("RefreshBattlePracticeInput", BindingFlags.Instance | BindingFlags.NonPublic);
            resolving.SetValue(battle, true); refresh.Invoke(controller, null);
            Assert.That(Button("PreviousButton").interactable, Is.False);
            Button("PreviousButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(instruction));
            resolving.SetValue(battle, false); refresh.Invoke(controller, null);
            Assert.That(Button("PreviousButton").interactable, Is.True);
        }

        [Test]
        public void ReviewingTheFarewellDoesNotExitUntilCurrentFinishIsPressed()
        {
            var controller = Create(); var returned = 0; controller.ConfigureReturnAction(() => returned++);
            // Prepare the existing final chapter model up to the concluding dialogue.
            controller.Session.SelectChapter(6);
            while (controller.Session.CurrentStep.Cue != TutorialCue.TutorialConclusion)
            {
                if (!controller.Session.WaitingForAction) { Assert.That(controller.Session.TryAdvance(), Is.True); continue; }
                if (controller.Session.CurrentStep.RequiredAction == TutorialAction.UpgradePracticeCard)
                {
                    controller.TryGetCardDefinitionAsset("Goblin", out var card);
                    Assert.That(controller.Session.UpgradePractice.TryUpgrade(card.ToDefinition()), Is.True);
                }
                Assert.That(controller.Session.PerformAction(controller.Session.CurrentStep.RequiredAction), Is.True);
            }
            typeof(TutorialSceneController).GetMethod("PresentStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            while (!controller.Session.IsComplete) Button("NextButton").onClick.Invoke();
            var farewell = DialogueText().text;
            Button("PreviousButton").onClick.Invoke();
            Assert.That(Button("NextButton").GetComponentInChildren<Text>().text, Is.EqualTo("다음"));
            Button("NextButton").onClick.Invoke();
            Assert.That(DialogueText().text, Is.EqualTo(farewell));
            Assert.That(returned, Is.Zero);
            Assert.That(Button("NextButton").GetComponentInChildren<Text>().text, Is.EqualTo("튜토리얼 마치기"));
            Button("NextButton").onClick.Invoke();
            Assert.That(returned, Is.EqualTo(1));
        }

        [Test]
        public void SkipClosesTheNativePreviewAndRequestsReturnOnlyOnce()
        {
            var controller = Create(); SelectChapterTwo(); AdvanceToAction(controller, TutorialAction.PreviewOffer);
            var target = Offer(); var data = Press(target); AdvanceHold(target, .5f);
            var requests = 0; controller.ConfigureReturnAction(() => requests++);
            Button("SkipButton").onClick.Invoke(); Button("SkipButton").onClick.Invoke();
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            Assert.That(requests, Is.EqualTo(1)); Assert.That(controller.Session.IsExited, Is.True);
            Assert.That(controller.Session.ManualPickCount, Is.Zero);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(target.GetComponent<CardHoldPreview>()), Is.False);
            Assert.That(VisiblePracticeAuras(), Is.Empty);
        }
    }
}
