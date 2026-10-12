using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.OwnedCards;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class OwnedCardsUpgradeFlipTests
    {
        private Scene _scene;
        private OwnedCardsSceneController _controller;

        [SetUp]
        public void OpenActualPreviewScene()
        {
            _scene = EditorSceneManager.OpenPreviewScene("Assets/OwnedCards_VSlice.unity");
            _controller = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OwnedCardsSceneController>(true)).Single();
            _controller.enabled = false;
            _controller.MaterializeOwnedCardsUiForEditor();
            OwnedCardsFlipTestAccess.Present(_controller, "Cerberus", 1);
            _controller.SelectCardFromGrid("Cerberus");
        }

        [TearDown]
        public void CloseOnlyPreviewScene()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void ActualArtworkButtonMaterializesOnceAndShowsAllThirteenRows()
        {
            var artwork = OwnedCardsFlipTestAccess.Field<Image>(_controller, "_detailArtworkImage");
            var button = OwnedCardsFlipTestAccess.Button(_controller);
            Assert.That(button.transform, Is.SameAs(artwork.transform));
            Assert.That(artwork.raycastTarget, Is.False);
            Assert.That(button.targetGraphic.name, Is.EqualTo("ArtworkFlipHitArea"));
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
            button.onClick.Invoke();
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.True);
            var cells = OwnedCardsFlipTestAccess.Cells(_controller);
            for (var row = 1; row <= 13; row++) Assert.That(cells[row, 0].text, Is.EqualTo("Lv." + row));
            Assert.That(cells[13, 1].text, Is.Not.Empty);
            Assert.That(OwnedCardsFlipTestAccess.Field<RectTransform>(_controller, "_detailUpgradeTableRoot").GetComponent<Image>().color, Is.EqualTo(Color.black));
            foreach (var cell in cells) { Assert.That(cell.color, Is.EqualTo(Color.white)); Assert.That(cell.raycastTarget, Is.False); }
            Assert.That(artwork.enabled, Is.False);
            Assert.That(OwnedCardsFlipTestAccess.Field<Text>(_controller, "_detailAttackValueText").enabled, Is.False);
            _controller.MaterializeOwnedCardsUiForEditor();
            Assert.That(OwnedCardsFlipTestAccess.Cells(_controller)[13, 0].text, Is.EqualTo("Lv.13"));
            Assert.That(artwork.transform.Cast<Transform>().Count(child => child.name == "UpgradeTableBack"), Is.EqualTo(1));
            Assert.That(artwork.GetComponents<Button>().Length, Is.EqualTo(1));
            button.onClick.Invoke();
            Assert.That(artwork.enabled, Is.True);
        }

        [Test]
        public void SameCardUpgradePreservesBackAndRefreshesHiddenFrontStats()
        {
            OwnedCardsFlipTestAccess.Button(_controller).onClick.Invoke();
            var cells = OwnedCardsFlipTestAccess.Cells(_controller);
            var level13 = cells[13, 2].text;
            OwnedCardsFlipTestAccess.Call(_controller, "ApplyUpgradeResponseToOwnedCards", new UpgradeCardResponse
            {
                UpgradedCard = new OwnedCardDto { CardId = "Cerberus", CopyCount = 95, UpgradeLevel = 2 }
            });
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.True);
            Assert.That(cells[13, 2].text, Is.EqualTo(level13), "Per-level increments must be independent of the owned level.");
            var asset = (CardDefinitionAsset)OwnedCardsFlipTestAccess.Call(_controller, "ResolveCardAsset", "Cerberus");
            var definition = (CardDefinition)typeof(OwnedCardsSceneController)
                .GetMethod("ResolveCurrentDefinition", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { asset });
            CardStatDisplay.TryCreate(definition, 2, out var expected);
            OwnedCardsFlipTestAccess.Button(_controller).onClick.Invoke();
            Assert.That(OwnedCardsFlipTestAccess.Field<Text>(_controller, "_detailAttackValueText").text, Is.EqualTo(expected.Attack.ToString()));
            Assert.That(OwnedCardsFlipTestAccess.Field<Text>(_controller, "_detailHpValueText").text, Is.EqualTo(expected.Hp.ToString()));
            Assert.That(OwnedCardsFlipTestAccess.Field<Text>(_controller, "_detailAttackValueText").enabled, Is.True);
        }

        [Test]
        public void CloseDuringUpgradeResetsFlipAndLateResponseKeepsModalClosed()
        {
            OwnedCardsFlipTestAccess.Button(_controller).onClick.Invoke();
            using (var cancellation = new CancellationTokenSource())
            {
                OwnedCardsFlipTestAccess.Set(_controller, "_upgradeCancellation", cancellation);
                OwnedCardsFlipTestAccess.Set(_controller, "_isUpgradingCard", true);
                _controller.CloseCardDetailFromUi();
                Assert.That(cancellation.IsCancellationRequested, Is.False);
                Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
                Assert.That(OwnedCardsFlipTestAccess.Field<Image>(_controller, "_detailArtworkImage").rectTransform.localScale, Is.EqualTo(Vector3.one));
                OwnedCardsFlipTestAccess.Call(_controller, "ApplyUpgradeResponseToOwnedCards", new UpgradeCardResponse
                {
                    UpgradedCard = new OwnedCardDto { CardId = "Cerberus", CopyCount = 95, UpgradeLevel = 2 }
                });
                Assert.That(_controller.IsCardDetailOpen, Is.False);
                OwnedCardsFlipTestAccess.Set(_controller, "_upgradeCancellation", null);
            }
            _controller.SelectCardFromGrid("Cerberus");
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
        }

        [Test]
        public void MissingArtworkStillHasClickableTableAndDifferentCardResetsFront()
        {
            var artwork = OwnedCardsFlipTestAccess.Field<Image>(_controller, "_detailArtworkImage");
            artwork.sprite = null;
            artwork.enabled = false;
            OwnedCardsFlipTestAccess.Button(_controller).onClick.Invoke();
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.True);
            Assert.That(OwnedCardsFlipTestAccess.Button(_controller).targetGraphic.enabled, Is.True);
            _controller.SelectCardFromGrid("Microreactor");
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
            OwnedCardsFlipTestAccess.Button(_controller).onClick.Invoke();
            Assert.That(OwnedCardsFlipTestAccess.Field<Text>(_controller, "_detailUpgradeTableEmpty").text, Is.EqualTo("강화 불가"));
            Assert.That(OwnedCardsFlipTestAccess.Field<RectTransform>(_controller, "_detailUpgradeTableRoot").Find("Grid").gameObject.activeSelf, Is.False);
        }
    }

    public sealed class OwnedCardsUpgradeFlipRuntimeTests
    {
        private OwnedCardsSceneController _controller;

        [UnitySetUp]
        public IEnumerator EnterActualSceneWithoutAccountRequests()
        {
            var scene = EditorSceneManager.OpenScene("Assets/OwnedCards_VSlice.unity", OpenSceneMode.Single);
            _controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OwnedCardsSceneController>(true)).Single();
            _controller.enabled = false;
            yield return new EnterPlayMode();
            _controller = Object.FindFirstObjectByType<OwnedCardsSceneController>();
            Assert.That(_controller.enabled, Is.False, "Disable account OnEnable; test only the actual scene's presentation.");
            OwnedCardsFlipTestAccess.Present(_controller, "Cerberus", 1);
            _controller.SelectCardFromGrid("Cerberus");
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            Time.timeScale = 1f;
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActualPointerClickRunsUnscaledFlipAndRapidRepeatKeepsOneTarget()
        {
            var artwork = OwnedCardsFlipTestAccess.Field<Image>(_controller, "_detailArtworkImage");
            var button = OwnedCardsFlipTestAccess.Button(_controller);
            Time.timeScale = 0f;
            var eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            var pointer = new PointerEventData(eventSystem)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, artwork.rectTransform.TransformPoint(artwork.rectTransform.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject, Is.SameAs(button.targetGraphic.gameObject), "Only the artwork's dedicated hit area receives this pointer.");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(OwnedCardsFlipTestAccess.Field<Coroutine>(_controller, "_detailArtworkFlipRoutine"), Is.Not.Null);
            Assert.That(button.interactable, Is.False);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False, "The initial face remains front until halfway.");
            Assert.That(artwork.rectTransform.localScale, Is.EqualTo(Vector3.one));
            button.onClick.Invoke(); // An additional programmatic click is also rejected while busy.
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
            // Sample the actual coroutine's phase setter at its exact seam, independent of editor frame hitches.
            OwnedCardsFlipTestAccess.Call(_controller, "SetDetailArtworkFlipProgress", .49f, true);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
            Assert.That(artwork.rectTransform.localScale.x, Is.InRange(0f, .01f));
            OwnedCardsFlipTestAccess.Call(_controller, "SetDetailArtworkFlipProgress", .5f, true);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.True);
            Assert.That(artwork.rectTransform.localScale.x, Is.Zero);
            yield return OwnedCardsFlipTestAccess.WaitForFlip(_controller);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.True);
            Assert.That(artwork.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
            yield return OwnedCardsFlipTestAccess.WaitForFlip(_controller);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
            Assert.That(artwork.enabled, Is.True);
            Assert.That(artwork.rectTransform.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator SelectionCloseAndDisableCancelPendingFlipWithoutStaleFaceChanges()
        {
            var button = OwnedCardsFlipTestAccess.Button(_controller);
            button.onClick.Invoke();
            _controller.SelectCardFromGrid("Microreactor");
            Assert.That(OwnedCardsFlipTestAccess.Field<Coroutine>(_controller, "_detailArtworkFlipRoutine"), Is.Null);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
            button.onClick.Invoke();
            _controller.CloseCardDetailFromUi();
            Assert.That(OwnedCardsFlipTestAccess.Field<Coroutine>(_controller, "_detailArtworkFlipRoutine"), Is.Null);
            Assert.That(OwnedCardsFlipTestAccess.Field<Image>(_controller, "_detailArtworkImage").rectTransform.localScale, Is.EqualTo(Vector3.one));
            _controller.SelectCardFromGrid("Cerberus");
            button.onClick.Invoke();
            // The controller is disabled to suppress authentication, so exercise its real lifecycle hook directly.
            OwnedCardsFlipTestAccess.Call(_controller, "OnDisable");
            Assert.That(_controller.IsCardDetailOpen, Is.False);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
            Assert.That(OwnedCardsFlipTestAccess.Field<Coroutine>(_controller, "_detailArtworkFlipRoutine"), Is.Null);
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
            Assert.That(OwnedCardsFlipTestAccess.Field<Image>(_controller, "_detailArtworkImage").rectTransform.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator SameCardResponseDuringFlipKeepsTargetAndLaterCloseDoesNotReopen()
        {
            OwnedCardsFlipTestAccess.Button(_controller).onClick.Invoke();
            OwnedCardsFlipTestAccess.Call(_controller, "ApplyUpgradeResponseToOwnedCards", new UpgradeCardResponse
            {
                UpgradedCard = new OwnedCardDto { CardId = "Cerberus", CopyCount = 95, UpgradeLevel = 2 }
            });
            Assert.That(OwnedCardsFlipTestAccess.Field<Coroutine>(_controller, "_detailArtworkFlipRoutine"), Is.Not.Null);
            yield return OwnedCardsFlipTestAccess.WaitForFlip(_controller);
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.True);
            _controller.CloseCardDetailFromUi();
            OwnedCardsFlipTestAccess.Present(_controller, "Cerberus", 2);
            Assert.That(_controller.IsCardDetailOpen, Is.False);
            _controller.SelectCardFromGrid("Cerberus");
            Assert.That(OwnedCardsFlipTestAccess.Back(_controller), Is.False);
        }
    }

    internal static class OwnedCardsFlipTestAccess
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static T Field<T>(OwnedCardsSceneController controller, string name) => (T)typeof(OwnedCardsSceneController).GetField(name, Hidden).GetValue(controller);
        internal static void Set(OwnedCardsSceneController controller, string name, object value) => typeof(OwnedCardsSceneController).GetField(name, Hidden).SetValue(controller, value);
        internal static object Call(OwnedCardsSceneController controller, string name, params object[] args) => typeof(OwnedCardsSceneController).GetMethod(name, Hidden).Invoke(controller, args);
        internal static Button Button(OwnedCardsSceneController controller) => Field<Button>(controller, "_detailArtworkFlipButton");
        internal static bool Back(OwnedCardsSceneController controller) => Field<bool>(controller, "_detailArtworkBackVisible");
        internal static Text[,] Cells(OwnedCardsSceneController controller) => Field<Text[,]>(controller, "_detailUpgradeTableCells");
        internal static void Present(OwnedCardsSceneController controller, string id, int level) => Call(controller, "PresentOwnedCards", new MeResponse
        {
            CollectionSummary = new CollectionSummaryDto { OwnedCards = new[] { new OwnedCardDto { CardId = id, CopyCount = 100, UpgradeLevel = level } } }
        });
        internal static IEnumerator WaitForFlip(OwnedCardsSceneController controller)
        {
            var deadline = Time.realtimeSinceStartup + 2f;
            while (Field<Coroutine>(controller, "_detailArtworkFlipRoutine") != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Field<Coroutine>(controller, "_detailArtworkFlipRoutine"), Is.Null, "The unscaled animation must finish within its bounded deadline.");
        }
    }
}
