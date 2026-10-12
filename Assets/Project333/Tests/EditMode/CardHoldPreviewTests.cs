using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project333.Tests.EditMode
{
    public sealed class CardHoldPreviewTests
    {
        private GameObject _object;
        private CardHoldPreview _hold;
        private string _identity;
        private PointerEventData _pointer;
        [SetUp] public void SetUp()
        {
            _object = new GameObject("HoldCard");
            _hold = _object.AddComponent<CardHoldPreview>();
            _identity = "Golem";
            _hold.Configure(() => _identity, owner => DeckCardPreviewOverlay.ShowFor(owner, "Golem", null, null));
            _pointer = new PointerEventData(null) { pointerId = 1, button = PointerEventData.InputButton.Left, eligibleForClick = true };
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(_object);
            foreach (var overlay in Object.FindObjectsByType<DeckCardPreviewOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(overlay.gameObject);
        }
        private void Advance(float seconds)
        {
            var start = (float)typeof(CardHoldPreview).GetField("_pressedAt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_hold);
            typeof(CardHoldPreview).GetMethod("AdvanceHold", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_hold, new object[] { start + seconds });
        }
        [Test] public void HoldShowsAndReleaseClosesWithoutSelectingThenNextClickWorks()
        {
            _hold.OnPointerDown(_pointer); Advance(0.2f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.False);
            Advance(0.5f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.True);
            _hold.OnPointerUp(_pointer);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.False);
            Assert.That(_hold.SuppressClick, Is.True);
            Assert.That(_pointer.eligibleForClick, Is.False);
            _hold.OnPointerDown(_pointer); _hold.OnPointerUp(_pointer);
            Assert.That(_hold.SuppressClick, Is.False);
        }
        [Test] public void MovingAfterPreviewCannotStartCardUseDrag()
        {
            _hold.OnPointerDown(_pointer); Advance(0.5f);
            _pointer.position = new Vector2(100,100); _hold.OnPointerMove(_pointer);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.False);
            Assert.That(_hold.TryBeginDrag(), Is.False);
        }
        [Test] public void OrdinaryDragRemainsAvailableAndCancelsHold()
        {
            _hold.OnPointerDown(_pointer);
            Assert.That(_hold.TryBeginDrag(), Is.True);
            Advance(1f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.False);
            Assert.That(_hold.SuppressClick, Is.True);
        }
        [Test] public void CardReplacementCancelsPreviewAndReleaseClick()
        {
            _hold.OnPointerDown(_pointer); Advance(0.5f);
            _identity = "Other"; Advance(0.6f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.False);
            Assert.That(_hold.SuppressClick, Is.True);
        }
        [Test] public void AnotherFingerDoesNotCloseOwnedPreview()
        {
            _hold.OnPointerDown(_pointer); Advance(0.5f);
            _hold.OnPointerUp(new PointerEventData(null) { pointerId = 2 });
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.True);
            _hold.OnPointerUp(_pointer);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.False);
        }
        [Test] public void DisableClosesPreview()
        {
            _hold.OnPointerDown(_pointer); Advance(0.5f);
            _object.SetActive(false);
            // EditMode does not dispatch runtime MonoBehaviour callbacks.
            typeof(CardHoldPreview).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_hold, null);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.False);
        }
        [Test] public void OptionalReleaseCallbackRunsOnceAfterCloseAndClickSuppression()
        {
            var callbacks = 0;
            _hold.Configure(() => _identity, owner => DeckCardPreviewOverlay.ShowFor(owner, "Golem", null, null),
                previewReleased: () =>
                {
                    Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_hold), Is.False);
                    Assert.That(_pointer.eligibleForClick, Is.False);
                    callbacks++;
                });
            _hold.OnPointerDown(_pointer); Advance(.41f);
            Assert.That(callbacks, Is.Zero, "Opening alone is not a completed hold-and-release practice.");
            _hold.OnPointerUp(_pointer); _hold.OnPointerUp(_pointer);
            Assert.That(callbacks, Is.EqualTo(1));
        }
        [TestCase("quick")]
        [TestCase("otherPointer")]
        [TestCase("move")]
        [TestCase("exit")]
        [TestCase("drag")]
        [TestCase("replace")]
        [TestCase("cancel")]
        public void OptionalReleaseCallbackDoesNotReportUnfinishedOrCancelledHolds(string interruption)
        {
            var callbacks = 0;
            _hold.Configure(() => _identity, owner => DeckCardPreviewOverlay.ShowFor(owner, "Golem", null, null),
                previewReleased: () => callbacks++);
            _hold.OnPointerDown(_pointer);
            if (interruption != "quick") Advance(.41f);
            switch (interruption)
            {
                case "otherPointer":
                    _hold.OnPointerUp(new PointerEventData(null) { pointerId = 99 });
                    Assert.That(callbacks, Is.Zero);
                    _hold.CancelHold(); break;
                case "move": _pointer.position = new Vector2(100, 100); _hold.OnPointerMove(_pointer); break;
                case "exit": _hold.OnPointerExit(_pointer); break;
                case "drag": _hold.TryBeginDrag(); break;
                case "replace": _identity = "Other"; break;
                case "cancel": _hold.CancelHold(); break;
            }
            _hold.OnPointerUp(_pointer);
            Assert.That(callbacks, Is.Zero);
        }
        [Test] public void OptionalReleaseCallbackRequiresAnActuallyVisiblePreview()
        {
            var callbacks = 0;
            _hold.Configure(() => _identity, _ => { }, previewReleased: () => callbacks++);
            _hold.OnPointerDown(_pointer); Advance(.41f); _hold.OnPointerUp(_pointer);
            Assert.That(callbacks, Is.Zero);
        }
    }
}
