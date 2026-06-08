using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;

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
            SetPrivateField(bootstrapper, "_battleFlowController", CreateFlowControllerInMainPhase());

            SetPrivateField(controller, "_battleBootstrapper", bootstrapper);
            SetPrivateField(controller, "_boardPresenter", boardPresenter);

            controller.RebindViews();
            return controller;
        }

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
