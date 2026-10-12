using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class BattlePlayerInputControllerTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ClickingSameHandCardTwice_ClearsCardSelection()
        {
            var controller = CreateControllerWithHand(out var handCardView);

            handCardView.NotifyClicked();
            handCardView.NotifyClicked();

            Assert.That(controller.SelectedCardId, Is.Empty);
            Assert.That(controller.SelectedSlotIndex, Is.EqualTo(-1));
            Assert.That(controller.HasSelectedUnit, Is.False);
            Assert.That(controller.InteractionStatus, Does.Contain("Cancelled hand card selection"));
        }

        [Test]
        public void ClickingHandCard_WhenUnitWasSelected_ClearsUnitSelection()
        {
            var controller = CreateControllerWithHand(out var handCardView);
            SetPrivateField(controller, "_hasSelectedUnit", true);
            SetPrivateField(controller, "_selectedUnitColumn", 2);
            SetPrivateField(controller, "_selectedUnitRow", 1);

            handCardView.NotifyClicked();

            Assert.That(controller.SelectedCardId, Is.EqualTo("sample_firebolt"));
            Assert.That(controller.SelectedSlotIndex, Is.EqualTo(0));
            Assert.That(controller.HasSelectedUnit, Is.False);
        }

        [Test]
        public void ClickingOwnOccupant_WhenCardWasSelected_SwitchesSelectionToOccupant()
        {
            var controller = CreateControllerWithBoardAndBootstrapper(out var tileView);
            SetPrivateField(controller, "_selectedCardId", "sample_firebolt");
            SetPrivateField(controller, "_selectedSlotIndex", 0);

            tileView.NotifyClicked();

            Assert.That(controller.SelectedCardId, Is.Empty);
            Assert.That(controller.SelectedSlotIndex, Is.EqualTo(-1));
            Assert.That(controller.HasSelectedUnit, Is.True);
            Assert.That(controller.SelectedUnitCoord, Is.EqualTo(new TileCoord(2, 1)));
            Assert.That(controller.InteractionStatus, Does.Contain("Selected occupant at (2,1)"));
        }

        [Test]
        public void ClickingBackground_WhenHandCardWasSelected_ClearsCardSelection()
        {
            var controller = CreateControllerWithHand(out var handCardView);
            handCardView.NotifyClicked();

            InvokePrivateMethod(controller, "TryClearCardSelectionFromPointerTarget", new object[] { null });

            Assert.That(controller.SelectedCardId, Is.Empty);
            Assert.That(controller.SelectedSlotIndex, Is.EqualTo(-1));
            Assert.That(controller.InteractionStatus, Does.Contain("selection cleared"));
        }

        [Test]
        public void ClickingButton_WhenHandCardWasSelected_KeepsCardSelection()
        {
            var controller = CreateControllerWithHand(out var handCardView);
            var buttonObject = Track(new GameObject("Button", typeof(RectTransform), typeof(Button)));
            handCardView.NotifyClicked();

            InvokePrivateMethod(controller, "TryClearCardSelectionFromPointerTarget", buttonObject);

            Assert.That(controller.SelectedCardId, Is.EqualTo("sample_firebolt"));
            Assert.That(controller.SelectedSlotIndex, Is.EqualTo(0));
        }

        [Test]
        public void ClickingTile_WhenHandCardWasSelected_KeepsCardSelection()
        {
            var controller = CreateControllerWithHand(out var handCardView);
            var tileObject = Track(new GameObject("Tile"));
            tileObject.AddComponent<TileView>();
            handCardView.NotifyClicked();

            InvokePrivateMethod(controller, "TryClearCardSelectionFromPointerTarget", tileObject);

            Assert.That(controller.SelectedCardId, Is.EqualTo("sample_firebolt"));
            Assert.That(controller.SelectedSlotIndex, Is.EqualTo(0));
        }

        [Test]
        public void ActiveButton_ClickingPlantShowsButton_ActivationConsumesOnceAndHides()
        {
            var controller = CreateControllerWithBoardAndBootstrapper(out var tile);
            var bootstrapper = (BattleBootstrapper)typeof(BattlePlayerInputController)
                .GetField("_battleBootstrapper", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            tile.Configure(PlayerId.Player,0,0);
            var state=bootstrapper.CurrentBattleState;
            var plant=new Project333.Runtime.Domain.Cards.BuildingState("active-plant","PowerPlant",
                PlayerId.Player,new TileCoord(0,0),false,0,30);
            state.PlayerBoard.Place(plant.Position,plant);
            tile.NotifyClicked();
            var button=tile.GetComponentInChildren<Button>(true);
            Assert.That(button,Is.Not.Null);
            Assert.That(button.gameObject.activeSelf,Is.True);
            Assert.That(button.GetComponentInChildren<Text>().text,Is.EqualTo("액티브"));
            var before=state.Player.Resources.Gold;
            button.onClick.Invoke();
            Assert.That(state.Player.Resources.Gold,Is.EqualTo(before-1));
            Assert.That(state.Player.Resources.Power,Is.EqualTo(2));
            Assert.That(button.gameObject.activeSelf,Is.False);
            tile.NotifyClicked();
            Assert.That(button.gameObject.activeSelf,Is.False);
            state.RestoreRuntimeState(state.TurnNumber+2,PlayerId.Player,PhaseType.Main);
            tile.NotifyClicked();
            Assert.That(button.gameObject.activeSelf,Is.True);
            plant.ApplyErasure();
            InvokePrivateMethod(controller,"RefreshActiveEffectButton");
            Assert.That(button.gameObject.activeSelf,Is.False);
        }

        [TestCase(false)][TestCase(true)]
        public void ActiveButton_NoGoldShowsWarningAndCanRetryAfterGainingGold(bool loseGoldAfterSelection)
        {
            var controller = CreateControllerWithBoardAndBootstrapper(out var tile);
            var bootstrapper = (BattleBootstrapper)typeof(BattlePlayerInputController)
                .GetField("_battleBootstrapper", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            tile.Configure(PlayerId.Player, 0, 0);
            var state = bootstrapper.CurrentBattleState;
            var plant = new Project333.Runtime.Domain.Cards.BuildingState("no-gold-plant", "PowerPlant",
                PlayerId.Player, tile.Coord, false, 0, 30);
            state.PlayerBoard.Place(tile.Coord, plant);
            if (loseGoldAfterSelection) tile.NotifyClicked();
            state.Player.Resources.Spend(state.Player.Resources.Clone());
            if (!loseGoldAfterSelection) tile.NotifyClicked();
            InvokePrivateMethod(controller, "RefreshActiveEffectButton");
            var button = tile.GetComponentInChildren<Button>(true);
            Assert.That(button, Is.Not.Null);
            Assert.That(button.gameObject.activeSelf && button.interactable, Is.True);
            button.onClick.Invoke();
            Assert.That(controller.InteractionStatus, Is.EqualTo("자원이 부족합니다."));
            var toast = (Text)typeof(BattleBootstrapper).GetField("_onlineErrorToastText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bootstrapper);
            Assert.That(toast.text, Does.Contain("자원이 부족합니다."));
            Assert.That(button.gameObject.activeSelf && button.interactable, Is.True);
            Assert.That(plant.LastActiveEffectTurnNumber, Is.EqualTo(-1));
            Assert.That(state.Player.Resources.Power, Is.Zero);
            state.Player.Resources.Add(new Project333.Runtime.Domain.Resources.ResourceSet(0, 0, 0, 1));
            button.onClick.Invoke();
            Assert.That(state.Player.Resources.Gold, Is.Zero);
            Assert.That(state.Player.Resources.Power, Is.EqualTo(2));
            Assert.That(button.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RealBattleScene_ActiveButtonUsesTileImageRectAndClickableCanvas()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Battle_VSlice.unity");
            try
            {
                var controllers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BattlePlayerInputController>(true));
                var controller=controllers.First();
                var bootstrapper=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BattleBootstrapper>(true)).First();
                var board=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BoardPresenter>(true)).First();
                var flow=CreateFlowControllerInMainPhase();
                SetPrivateField(bootstrapper,"_battleFlowController",flow);
                SetPrivateField(bootstrapper,"_battleGateway",new LocalBattleGateway(flow));
                SetPrivateField(controller,"_battleBootstrapper",bootstrapper);
                SetPrivateField(controller,"_boardPresenter",board);
                controller.RebindViews();
                var tile=board.PlayerTileViews.First(t=>t.Coord==new TileCoord(0,0));
                flow.CurrentBattleState.PlayerBoard.Place(tile.Coord,new Project333.Runtime.Domain.Cards.BuildingState(
                    "real-plant","PowerPlant",PlayerId.Player,tile.Coord,false,0,30));
                tile.NotifyClicked();
                var anchor=tile.GetComponent<TileTextView>().ActiveEffectButtonAnchor;
                Assert.That(anchor,Is.Not.Null);
                var button=anchor.GetComponentInChildren<Button>();
                Assert.That(button,Is.Not.Null);
                Assert.That(button.transform.parent,Is.SameAs(anchor));
                Assert.That(button.GetComponent<Canvas>().overrideSorting,Is.True);
                Assert.That(button.GetComponent<GraphicRaycaster>(),Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                var rect=(RectTransform)button.transform;
                Assert.That(rect.rect.width,Is.GreaterThan(0));
                Assert.That(rect.rect.height,Is.GreaterThan(0));
                button.onClick.Invoke();
                Assert.That(flow.CurrentBattleState.Player.Resources.Power,Is.EqualTo(2));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        private BattlePlayerInputController CreateControllerWithHand(out HandCardView handCardView)
        {
            var controllerGameObject = Track(new GameObject("BattlePlayerInputController"));
            var controller = controllerGameObject.AddComponent<BattlePlayerInputController>();

            var handPresenterObject = Track(new GameObject("HandPresenter"));
            var handPresenter = handPresenterObject.AddComponent<HandPresenter>();

            var handCardViewObject = Track(new GameObject("HandCard"));
            handCardView = handCardViewObject.AddComponent<HandCardView>();
            handCardView.Configure(0);
            handCardView.Present("sample_firebolt");

            SetPrivateField(handPresenter, "_handCardViews", new[] { handCardView });
            SetPrivateField(controller, "_handPresenter", handPresenter);
            handPresenter.SetExpanded(true); // Existing selection tests start with the hand open.

            controller.RebindViews();
            return controller;
        }

        private BattlePlayerInputController CreateControllerWithBoardAndBootstrapper(out TileView tileView)
        {
            var controllerGameObject = Track(new GameObject("BattlePlayerInputController"));
            var controller = controllerGameObject.AddComponent<BattlePlayerInputController>();

            var boardPresenterObject = Track(new GameObject("BoardPresenter"));
            var boardPresenter = boardPresenterObject.AddComponent<BoardPresenter>();

            var tileObject = Track(new GameObject("PlayerMasterTile"));
            tileView = tileObject.AddComponent<TileView>();
            tileView.Configure(PlayerId.Player, 2, 1);

            SetPrivateField(boardPresenter, "_playerTileViews", new[] { tileView });
            SetPrivateField(boardPresenter, "_aiTileViews", Array.Empty<TileView>());

            var bootstrapperObject = Track(new GameObject("BattleBootstrapper"));
            var bootstrapper = bootstrapperObject.AddComponent<BattleBootstrapper>();
            var battleFlowController = CreateFlowControllerInMainPhase();
            SetPrivateField(bootstrapper, "_battleFlowController", battleFlowController);
            SetPrivateField(bootstrapper, "_battleGateway", new LocalBattleGateway(battleFlowController));

            SetPrivateField(controller, "_battleBootstrapper", bootstrapper);
            SetPrivateField(controller, "_boardPresenter", boardPresenter);

            controller.RebindViews();
            return controller;
        }

        [Test]
        public void EndTurn_LocksButtonAndActionsUntilNextLocalMainPhase()
        {
            var bootstrapper = Track(new GameObject("TurnLock")).AddComponent<BattleBootstrapper>();
            var flow = CreateFlowControllerInMainPhase();
            SetPrivateField(bootstrapper, "_battleFlowController", flow);
            SetPrivateField(bootstrapper, "_battleGateway", new LocalBattleGateway(flow));
            SetPrivateField(bootstrapper, "_autoRunAiTurns", false);
            SetPrivateField(bootstrapper, "_animateAttackSequences", false);
            var presenter = Track(new GameObject("TurnButtonPresenter")).AddComponent<BattleScreenPresenter>();
            var button = Track(new GameObject("EndTurn", typeof(RectTransform), typeof(Image), typeof(Button))).GetComponent<Button>();
            SetPrivateField(presenter, "_battleBootstrapper", bootstrapper);
            SetPrivateField(presenter, "_endTurnButton", button);
            var turn = flow.CurrentBattleState.TurnNumber;
            presenter.EndTurnFromUi();
            Assert.That(button.interactable, Is.False);
            Assert.That(bootstrapper.IsBusy, Is.True);
            presenter.EndTurnFromUi();
            Assert.That(flow.CurrentBattleState.TurnNumber, Is.EqualTo(turn + 1));
            Assert.That(bootstrapper.TryMovePlayerOccupant(new TileCoord(2, 1), new TileCoord(1, 1), out _), Is.False);
            flow.ResolveTurnStart();
            bootstrapper.EndTurn(); // A player click cannot end the AI's turn.
            Assert.That(flow.CurrentBattleState.TurnNumber, Is.EqualTo(turn + 1));
            flow.EndTurn();
            Assert.That(bootstrapper.CanPlayerEndTurn, Is.False);
            flow.ResolveTurnStart();
            InvokePrivateMethod(presenter, "RefreshEndTurnInteractable");
            Assert.That(bootstrapper.IsBusy, Is.False);
            Assert.That(button.interactable, Is.True);
            Assert.That(flow.CurrentBattleState.TurnNumber, Is.EqualTo(turn + 2), "No buffered click runs on unlock.");
        }
        [Test]
        public void EndTurn_HandInspectionRemainsAvailableWhileCommandsStayLocked()
        {
            var controller = CreateControllerWithHand(out var card);
            var bootstrapper = Track(new GameObject("InspectionTurnLock")).AddComponent<BattleBootstrapper>();
            var flow = CreateFlowControllerInMainPhase();
            SetPrivateField(bootstrapper, "_battleFlowController", flow);
            SetPrivateField(bootstrapper, "_battleGateway", new LocalBattleGateway(flow));
            SetPrivateField(bootstrapper, "_autoRunAiTurns", false);
            SetPrivateField(bootstrapper, "_animateAttackSequences", false);
            SetPrivateField(controller, "_battleBootstrapper", bootstrapper);
            bootstrapper.EndTurn();
            Assert.That(bootstrapper.IsBusy, Is.True);
            card.NotifyClicked();
            Assert.That(controller.SelectedCardId, Is.EqualTo(card.CardId));
            InvokePrivateMethod(controller, "HighlightSelectedHandCard");
            Assert.That(card.HighlightState, Is.EqualTo(BattleHighlightState.Selected));
            card.NotifyDragStarted(Vector2.zero);
            Assert.That(typeof(BattlePlayerInputController).GetField("_isDraggingCard", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.False);
            var pos = new TileCoord(2, 1);
            Assert.That(bootstrapper.TryUsePlayerCardOnTile(card.CardId, PlayerId.AI, pos, out _), Is.False);
            Assert.That(bootstrapper.TryAttackWithPlayerOccupant(pos, pos, out _), Is.False);
            Assert.That(bootstrapper.TryMovePlayerOccupant(pos, new TileCoord(1,1), out _), Is.False);
            Assert.That(bootstrapper.TryActivatePlayerEffect(pos, "master", out _), Is.False);
            card.NotifyClicked();
            Assert.That(controller.SelectedCardId, Is.Empty);
        }

        [Test]
        public void CompactHand_FirstClickOnlyExpands_ThenCardCanBeSelected()
        {
            var controller = CreateControllerWithHand(out var card);
            var hand = (HandPresenter)typeof(BattlePlayerInputController).GetField("_handPresenter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            hand.SetExpanded(false);
            card.NotifyClicked();
            Assert.That(hand.IsExpanded, Is.True);
            Assert.That(controller.SelectedCardId, Is.Empty);
            card.NotifyClicked();
            Assert.That(controller.SelectedCardId, Is.EqualTo(card.CardId));
        }

        [Test]
        public void OutsideClick_CollapsesAfterDispatch_WhileHandPressAndDragDoNot()
        {
            var controller = CreateControllerWithHand(out var card);
            var hand = (HandPresenter)typeof(BattlePlayerInputController).GetField("_handPresenter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            card.NotifyClicked();
            SetPrivateField(controller, "_isTrackingBackgroundDeselectPointer", true);
            SetPrivateField(controller, "_backgroundDeselectPointerId", -1);
            SetPrivateField(controller, "_backgroundDeselectPointerStartedOverPreservingTarget", true); // Board/button consumes its click first.
            InvokePrivateMethod(controller, "EndBackgroundDeselectPointer", -1, Vector2.zero);
            Assert.That(hand.IsExpanded, Is.True);
            Assert.That(controller.SelectedCardId, Is.EqualTo(card.CardId));
            InvokePrivateMethod(controller, "ApplyQueuedHandCollapse");
            Assert.That(hand.IsExpanded, Is.False);
            Assert.That(controller.SelectedCardId, Is.Empty);
            hand.SetExpanded(true);
            SetPrivateField(controller, "_isTrackingBackgroundDeselectPointer", true);
            SetPrivateField(controller, "_backgroundDeselectPointerId", -1);
            SetPrivateField(controller, "_backgroundPointerStartedOverHandPreservingTarget", true);
            InvokePrivateMethod(controller, "EndBackgroundDeselectPointer", -1, Vector2.zero);
            InvokePrivateMethod(controller, "ApplyQueuedHandCollapse");
            Assert.That(hand.IsExpanded, Is.True);
            SetPrivateField(controller, "_isTrackingBackgroundDeselectPointer", true);
            SetPrivateField(controller, "_backgroundDeselectPointerId", -1);
            SetPrivateField(controller, "_backgroundDeselectPointerMovedTooFar", true);
            InvokePrivateMethod(controller, "EndBackgroundDeselectPointer", -1, Vector2.zero);
            InvokePrivateMethod(controller, "ApplyQueuedHandCollapse");
            Assert.That(hand.IsExpanded, Is.True);
        }

        [TestCase(PlayerId.Player, true)]
        [TestCase(PlayerId.AI, true)]
        public void HandCollapse_BothTileSidesAndDetachedSurfacesPreserveExpansion(PlayerId owner, bool expected)
        {
            var tile = Track(new GameObject("Tile")).AddComponent<TileView>();
            tile.Configure(owner, 0, 0);
            var child = new GameObject("TileImage");
            child.transform.SetParent(tile.transform);
            Assert.That(PreservesHand(child), Is.EqualTo(expected));
            var surface = Track(new GameObject("DetachedSurface")).AddComponent<TileLongPressRelay>();
            surface.Initialize(null, tile);
            Assert.That(PreservesHand(surface.gameObject), Is.EqualTo(expected));
        }

        [Test]
        public void HandCollapse_OnlyEndTurnAndGearButtonsAreRegistered()
        {
            var root = Track(new GameObject("Presenter"));
            root.SetActive(false);
            var presenter = root.AddComponent<BattleScreenPresenter>();
            var endTurn = Track(new GameObject("EndTurn", typeof(RectTransform), typeof(Button))).GetComponent<Button>();
            SetPrivateField(presenter, "_endTurnButton", endTurn);
            SetPrivateField(presenter, "_endTurnButtonVisualVersion", 1);
            InvokePrivateMethod(presenter, "ApplyEndTurnButtonVisual");
            Assert.That(PreservesHand(endTurn.gameObject), Is.True);
            var caption = new GameObject("Caption");
            caption.transform.SetParent(endTurn.transform);
            Assert.That(PreservesHand(caption), Is.True);

            var settingsRoot = Track(new GameObject("Settings", typeof(RectTransform)));
            settingsRoot.SetActive(false);
            var settings = settingsRoot.AddComponent<Project333.Runtime.Presentation.Settings.GlobalSettingsMenuController>();
            SetPrivateField(settings, "_safeAreaRoot", settingsRoot.GetComponent<RectTransform>());
            InvokePrivateMethod(settings, "EnsureGearButton");
            var gear = settingsRoot.GetComponentsInChildren<PreserveExpandedHand>(true).Single();
            Assert.That(PreservesHand(gear.gameObject), Is.True);
            var otherButton = Track(new GameObject("OtherButton", typeof(RectTransform), typeof(Button)));
            Assert.That(PreservesHand(otherButton), Is.False);
            Assert.That(PreservesHand(null), Is.False);
        }

        [TestCase(-1)]
        [TestCase(0)]
        public void HandCollapse_PreservedPointerDownSurvivesChangedReleaseTarget_ThenResets(int pointerId)
        {
            var controller = CreateControllerWithHand(out var card);
            var hand = (HandPresenter)typeof(BattlePlayerInputController).GetField("_handPresenter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            SetPrivateField(controller, "_isTrackingBackgroundDeselectPointer", true);
            SetPrivateField(controller, "_backgroundDeselectPointerId", pointerId);
            SetPrivateField(controller, "_backgroundPointerStartedOverHandPreservingTarget", true);
            // Opening settings changes the top release target; the original press still wins.
            InvokePrivateMethod(controller, "EndBackgroundDeselectPointer", pointerId, Vector2.zero);
            InvokePrivateMethod(controller, "ApplyQueuedHandCollapse");
            Assert.That(hand.IsExpanded, Is.True);
            SetPrivateField(controller, "_isTrackingBackgroundDeselectPointer", true);
            SetPrivateField(controller, "_backgroundDeselectPointerId", pointerId);
            InvokePrivateMethod(controller, "EndBackgroundDeselectPointer", pointerId, Vector2.zero);
            InvokePrivateMethod(controller, "ApplyQueuedHandCollapse");
            Assert.That(hand.IsExpanded, Is.False);
        }

        private static bool PreservesHand(GameObject target) =>
            (bool)typeof(BattlePlayerInputController).GetMethod("IsHandExpansionPreservingTarget", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { target });

        private static BattleFlowController CreateFlowControllerInMainPhase()
        {
            var battleFlowController = new BattleFlowController();
            battleFlowController.StartBattle(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));
            battleFlowController.PassMulligan(PlayerId.Player);
            battleFlowController.ResolveTurnStart();
            return battleFlowController;
        }

        private GameObject Track(GameObject gameObject)
        {
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetPrivateField<TTarget, TValue>(TTarget target, string fieldName, TValue value)
            where TTarget : class
        {
            var fieldInfo = typeof(TTarget).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null)
            {
                throw new InvalidOperationException($"Field '{fieldName}' was not found on '{typeof(TTarget).Name}'.");
            }

            fieldInfo.SetValue(target, value);
        }

        private static void InvokePrivateMethod<TTarget>(TTarget target, string methodName, params object[] arguments)
            where TTarget : class
        {
            var methodInfo = typeof(TTarget).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' was not found on '{typeof(TTarget).Name}'.");
            }

            methodInfo.Invoke(target, arguments);
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new List<string>
            {
                prefix + "-00",
                prefix + "-01",
                prefix + "-02",
                prefix + "-03",
                prefix + "-04",
                prefix + "-05",
                prefix + "-06",
                prefix + "-07",
                prefix + "-08",
                prefix + "-09",
            };
        }
    }
}
