using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Hand;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    // Play-mode frames matter: the stale flag overwrites the layout and raycast state in LateUpdate.
    public sealed class HandCardDragLifecycleTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DestroyingSourceBeforeRuntimeVisual_DetachesCallbacksDuringSceneUnload()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                hand.Begin(slot);
                yield return null;
                var relay = slot.Rect.GetComponent<HandCardRuntimeDragRelay>();
                Object.DestroyImmediate(slot.Text);
                Assert.That(relay.CancelDrag, Is.Null);
                slot.Rect.gameObject.SetActive(false);
                Assert.That(hand.EndCount, Is.Zero);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InterruptedDrag_EmptySlotAndLaterDrawRestoreLayoutAndInteraction()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                hand.Begin(slot);
                yield return null;
                slot.AssertDragging();
                // Successful play/snapshot removes the visual before Unity supplies OnEndDrag.
                slot.Rect.gameObject.SetActive(false);
                slot.AssertNotDragging();
                slot.View.Present(string.Empty, string.Empty, false);
                slot.Layout(false);
                yield return null;
                slot.View.Present("TimedBomb", "draw-timed-bomb", false);
                slot.Layout();
                yield return null;
                yield return null;
                yield return slot.SettleGeometry();
                slot.AssertResting();
                Assert.That(hand.EndCount, Is.Zero, "Cancellation must not submit another card-use command.");
                hand.Click(slot);
                Assert.That(hand.ClickCount, Is.EqualTo(1), "The replacement card must accept a fresh tap.");
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReplacingDraggedIdentity_CancelsEvenWhenCardIdIsTheSame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                foreach (var cardId in new[] { "ManaShield", "NuclearPowerPlant" })
                {
                    hand.Begin(slot);
                    yield return null;
                    slot.AssertDragging();
                    slot.View.Present(cardId, "different-instance-" + cardId, false);
                    slot.Text.Refresh();
                    slot.AssertNotDragging();
                    slot.Layout();
                    yield return null;
                    yield return null;
                    yield return slot.SettleGeometry();
                    slot.AssertResting();
                }
                Assert.That(hand.EndCount, Is.Zero);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HandSlotShift_DoesNotTransferInterruptedDragToANeighbouringCard()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(3))
            {
                var left = hand.Slots[0];
                var centre = hand.Slots[1];
                var right = hand.Slots[2];
                hand.Begin(centre);
                yield return null;
                centre.AssertDragging();
                // A card to the left is used; presenter reuses each slot for the next runtime identity.
                left.View.Present(centre.View.CardId, centre.View.RuntimeId, false);
                centre.View.Present(right.View.CardId, right.View.RuntimeId, false);
                right.View.Present(string.Empty, string.Empty, false);
                foreach (var slot in hand.Slots) slot.Layout(slot.View.HasCard);
                centre.AssertNotDragging();
                yield return null;
                yield return null;
                yield return left.SettleGeometry();
                left.AssertResting();
                yield return centre.SettleGeometry();
                centre.AssertResting();
                // A later draw must also be interactive; no residual flag follows the vacated slot.
                right.View.Present("NuclearPowerPlant", "later-drawn-plant", false);
                right.Layout();
                yield return null;
                yield return null;
                yield return right.SettleGeometry();
                right.AssertResting();
                hand.Click(centre);
                hand.Click(right);
                Assert.That(hand.ClickCount, Is.EqualTo(2));
                Assert.That(hand.EndCount, Is.Zero);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DisableFocusAndPause_CancelDragWithoutReleasingACommand()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                foreach (var boundary in new[] { "owner-disable", "visual-disable", "focus", "pause" })
                {
                    hand.Begin(slot);
                    yield return null;
                    slot.AssertDragging();
                    if (boundary == "owner-disable")
                    {
                        slot.Text.enabled = false;
                        slot.Text.enabled = true;
                    }
                    else if (boundary == "visual-disable")
                    {
                        slot.Rect.gameObject.SetActive(false);
                        slot.AssertNotDragging();
                        slot.Rect.gameObject.SetActive(true);
                    }
                    else if (boundary == "focus") Invoke(slot.Text, "OnApplicationFocus", false);
                    else Invoke(slot.Text, "OnApplicationPause", true);
                    slot.AssertNotDragging();
                    slot.Layout();
                    yield return null;
                    yield return null;
                    yield return slot.SettleGeometry();
                    slot.AssertResting(boundary);
                }
                Assert.That(hand.EndCount, Is.Zero);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EndDragCallbackException_StillCleansVisualAndDoesNotNotifyTwice()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                var activeAtCallback = true;
                Action<HandCardView, Vector2> throwOnRelease = (_, __) =>
                {
                    activeAtCallback = Get<bool>(slot.Text, "_isRuntimeDragActive");
                    throw new InvalidOperationException("Deliberate release callback failure");
                };
                slot.View.DragEnded += throwOnRelease;
                hand.Begin(slot);
                yield return null;
                Assert.Throws<InvalidOperationException>(() => slot.Relay.OnEndDrag(hand.Pointer));
                slot.View.DragEnded -= throwOnRelease;
                slot.AssertNotDragging();
                slot.Layout();
                yield return null;
                yield return null;
                yield return slot.SettleGeometry();
                slot.AssertResting();
                Assert.That(activeAtCallback, Is.False, "External release callbacks must see drag already finished.");
                slot.Relay.OnEndDrag(hand.Pointer);
                Assert.That(hand.EndCount, Is.EqualTo(1), "A late duplicate release cannot retry the command.");
                hand.Click(slot);
                Assert.That(hand.ClickCount, Is.EqualTo(1));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SameRuntimeIdentitySnapshotRefresh_PreservesAnActiveDrag()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                hand.Begin(slot);
                yield return null;
                var dragPosition = slot.Rect.anchoredPosition;
                slot.View.Present(slot.View.CardId, slot.View.RuntimeId, false, spellPower: 2);
                slot.Text.Refresh();
                slot.Layout();
                yield return null;
                slot.AssertDragging();
                Assert.That(Vector2.Distance(slot.Rect.anchoredPosition, dragPosition), Is.LessThan(.1f));
                slot.Relay.OnEndDrag(hand.Pointer);
                slot.AssertNotDragging();
                slot.Layout();
                yield return null;
                yield return null;
                yield return slot.SettleGeometry();
                slot.AssertResting();
                Assert.That(hand.StartCount, Is.EqualTo(1));
                Assert.That(hand.EndCount, Is.EqualTo(1));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NormalDragShortTapAndHoldPreview_RetainTheirSeparateInputBehaviour()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                hand.Begin(slot);
                yield return null;
                slot.Relay.OnEndDrag(hand.Pointer);
                slot.Relay.OnEndDrag(hand.Pointer);
                Assert.That(hand.EndCount, Is.EqualTo(1));
                slot.AssertNotDragging();
                slot.Layout();
                yield return null;
                yield return null;
                yield return slot.SettleGeometry();
                slot.AssertResting();
                Assert.That(hand.StartCount, Is.EqualTo(1));
                Assert.That(hand.EndCount, Is.EqualTo(1));
                hand.Click(slot);
                Assert.That(hand.ClickCount, Is.EqualTo(1));

                var hold = slot.Rect.GetComponent<CardHoldPreview>();
                hand.NewPointer(slot);
                hold.OnPointerDown(hand.Pointer);
                Invoke(hold, "AdvanceHold", Get<float>(hold, "_pressedAt") + .5f);
                Assert.That(DeckCardPreviewOverlay.IsVisibleFor(hold), Is.True);
                slot.Relay.OnBeginDrag(hand.Pointer);
                Assert.That(Get<bool>(slot.Text, "_isRuntimeDragActive"), Is.False,
                    "A shown hold preview must never turn into a card-use drag.");
                Assert.That(hand.StartCount, Is.EqualTo(1));
                hold.OnPointerUp(hand.Pointer);
                Assert.That(DeckCardPreviewOverlay.IsVisibleFor(hold), Is.False);
                slot.Rect.GetComponent<Button>().OnPointerClick(hand.Pointer);
                Assert.That(hand.ClickCount, Is.EqualTo(1), "Releasing a preview is not a selection tap.");
                hand.Click(slot);
                Assert.That(hand.ClickCount, Is.EqualTo(2));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InterruptedVisualDisable_ClearsInputControllerDragAndSelection()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                var presenter = hand.Root.AddComponent<HandPresenter>();
                presenter.enabled = false;
                Set(presenter, "_handCardViews", new[] { slot.View });
                presenter.SetExpanded(true);
                slot.Text.SetHandInteractionOwner(presenter);
                var input = hand.Root.AddComponent<BattlePlayerInputController>();
                input.enabled = false;
                Set(input, "_handPresenter", presenter);
                input.RebindViews();
                hand.Begin(slot);
                Assert.That(Get<bool>(input, "_isDraggingCard"), Is.True);
                Assert.That(input.SelectedHandCardRuntimeId, Is.EqualTo(slot.View.RuntimeId));
                yield return null;
                slot.Rect.gameObject.SetActive(false);
                slot.AssertNotDragging();
                slot.Layout();
                yield return null;
                yield return null;
                yield return slot.SettleGeometry();
                slot.AssertResting();
                Assert.That(Get<bool>(input, "_isDraggingCard"), Is.False);
                Assert.That(input.SelectedCardId, Is.Empty);
                Assert.That(input.SelectedHandCardRuntimeId, Is.Empty);
                Assert.That(input.SelectedSlotIndex, Is.EqualTo(-1));
                Assert.That(hand.EndCount, Is.Zero);
                hand.Click(slot);
                Assert.That(input.SelectedHandCardRuntimeId, Is.EqualTo(slot.View.RuntimeId));
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ForeignPointerAndLateRelease_CannotTakeOverOrRepeatTheOwnedDrag()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var hand = new HandHarness(1))
            {
                var slot = hand.Slots[0];
                var acceptedMoves = 0;
                slot.View.Dragged += (_, __) => acceptedMoves++;
                hand.Begin(slot);
                var owned = hand.Pointer;
                yield return null;
                slot.AssertDragging();
                var ownedPosition = slot.Rect.anchoredPosition;
                var foreign = new PointerEventData(hand.Root.GetComponentInChildren<EventSystem>())
                {
                    pointerId = owned.pointerId + 1,
                    button = PointerEventData.InputButton.Left,
                    position = owned.position + new Vector2(300f, -200f)
                };
                slot.Relay.OnBeginDrag(foreign);
                slot.Relay.OnDrag(foreign);
                slot.Relay.OnEndDrag(foreign);
                yield return null;
                slot.AssertDragging();
                Assert.That(Vector2.Distance(slot.Rect.anchoredPosition, ownedPosition), Is.LessThan(.1f));
                Assert.That(hand.StartCount, Is.EqualTo(1));
                Assert.That(acceptedMoves, Is.EqualTo(1));
                Assert.That(hand.EndCount, Is.Zero);

                // A successful command can synchronously empty/hide the same slot during release.
                Action<HandCardView, Vector2> removeDuringRelease = (_, __) =>
                {
                    slot.View.Present(string.Empty, string.Empty, false);
                    slot.Text.Refresh();
                    slot.Rect.gameObject.SetActive(false);
                    slot.AssertNotDragging();
                    slot.Layout(false);
                };
                slot.View.DragEnded += removeDuringRelease;
                slot.Relay.OnEndDrag(owned);
                slot.View.DragEnded -= removeDuringRelease;
                slot.Relay.OnEndDrag(owned);
                Assert.That(hand.EndCount, Is.EqualTo(1));
                Assert.That(Get<bool>(slot.Text, "_isRuntimeDragActive"), Is.False);
                slot.View.Present("NuclearPowerPlant", "replacement-after-release", false);
                slot.Layout();
                yield return null;
                yield return null;
                yield return slot.SettleGeometry();
                slot.AssertResting();

                hand.Begin(slot);
                var cancelledPointer = hand.Pointer;
                yield return null;
                slot.Rect.gameObject.SetActive(false);
                slot.AssertNotDragging();
                slot.Layout();
                slot.Relay.OnEndDrag(cancelledPointer);
                Assert.That(hand.EndCount, Is.EqualTo(1), "Cancel must discard a late release without a command.");
                yield return null;
                yield return null;
                yield return slot.SettleGeometry();
                slot.AssertResting();

                hand.NewPointer(slot);
                hand.Pointer.pointerId = cancelledPointer.pointerId + 2;
                slot.Rect.GetComponent<CardHoldPreview>().OnPointerDown(hand.Pointer);
                slot.Relay.OnBeginDrag(hand.Pointer);
                slot.Relay.OnEndDrag(cancelledPointer);
                slot.Relay.OnDrag(foreign);
                slot.Relay.OnEndDrag(foreign);
                Assert.That(Get<bool>(slot.Text, "_isRuntimeDragActive"), Is.True);
                Assert.That(hand.StartCount, Is.EqualTo(3));
                Assert.That(hand.EndCount, Is.EqualTo(1));
                hand.Pointer.position += new Vector2(25f, 35f);
                slot.Relay.OnDrag(hand.Pointer);
                Assert.That(acceptedMoves, Is.EqualTo(3));
                slot.Relay.OnEndDrag(hand.Pointer);
                slot.Relay.OnEndDrag(hand.Pointer);
                Assert.That(hand.EndCount, Is.EqualTo(2));
                slot.AssertNotDragging();
                slot.Layout();
                yield return null;
                yield return null;
                yield return slot.SettleGeometry();
                slot.AssertResting();
                Assert.That(hand.EndCount, Is.EqualTo(2));
            }
            yield return new ExitPlayMode();
        }
        private static T Get<T>(object target, string field) =>
            (T)target.GetType().GetField(field, Hidden).GetValue(target);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Hidden).SetValue(target, value);
        private static void Invoke(object target, string method, params object[] args)
        {
            var callback = target.GetType().GetMethod(method, Hidden);
            Assert.That(callback, Is.Not.Null, method + " must handle the drag lifecycle boundary.");
            callback.Invoke(target, args);
        }

        private sealed class HandHarness : IDisposable
        {
            public readonly GameObject Root;
            public readonly Slot[] Slots;
            public PointerEventData Pointer;
            private readonly EventSystem _events;
            public int ClickCount, StartCount, EndCount;

            public HandHarness(int count)
            {
                Root = new GameObject("DragLifecycleHarness", typeof(RectTransform), typeof(Canvas));
                Root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var eventObject = new GameObject("TestEvents", typeof(EventSystem));
                eventObject.transform.SetParent(Root.transform, false);
                _events = eventObject.GetComponent<EventSystem>();
                var layoutObject = new GameObject("RuntimeHandRoot", typeof(RectTransform));
                var layoutRoot = (RectTransform)layoutObject.transform;
                layoutRoot.SetParent(Root.transform, false);
                layoutRoot.anchorMin = Vector2.zero;
                layoutRoot.anchorMax = new Vector2(1f, 0f);
                layoutRoot.pivot = new Vector2(.5f, 0f);
                layoutRoot.sizeDelta = new Vector2(0f, 440f);
                Slots = new Slot[count];
                for (var i = 0; i < count; i++)
                {
                    var owner = new GameObject("Slot" + i, typeof(RectTransform), typeof(HandCardView), typeof(HandCardTextView));
                    owner.transform.SetParent(Root.transform, false);
                    var view = owner.GetComponent<HandCardView>();
                    view.Configure(i);
                    view.Present("ManaShield", "original-" + i, false);
                    view.Clicked += _ => ClickCount++;
                    view.DragStarted += (_, __) => StartCount++;
                    view.DragEnded += (_, __) => EndCount++;
                    var text = owner.GetComponent<HandCardTextView>();
                    Set(text, "_layoutLerpSpeed", 1000f);
                    text.BindRuntimeHandRoot(layoutRoot);
                    Slots[i] = new Slot(view, text, i);
                    Slots[i].Layout();
                }
                Canvas.ForceUpdateCanvases();
            }

            public void NewPointer(Slot slot)
            {
                Pointer = new PointerEventData(_events)
                {
                    pointerId = 7,
                    button = PointerEventData.InputButton.Left,
                    position = new Vector2(Screen.width * .43f, Screen.height * .57f),
                    eligibleForClick = true,
                    pointerPress = slot.Rect.gameObject,
                    pointerDrag = slot.Rect.gameObject
                };
            }

            public void Begin(Slot slot)
            {
                NewPointer(slot);
                slot.Rect.GetComponent<CardHoldPreview>().OnPointerDown(Pointer);
                slot.Relay.OnBeginDrag(Pointer);
                Pointer.position += new Vector2(24f, 30f);
                slot.Relay.OnDrag(Pointer);
            }

            public void Click(Slot slot)
            {
                NewPointer(slot);
                var hold = slot.Rect.GetComponent<CardHoldPreview>();
                hold.OnPointerDown(Pointer);
                hold.OnPointerUp(Pointer);
                slot.Rect.GetComponent<Button>().OnPointerClick(Pointer);
            }

            public void Dispose()
            {
                foreach (var slot in Slots) DeckCardPreviewOverlay.HideFor(slot.Rect.GetComponent<CardHoldPreview>());
                Object.DestroyImmediate(Root);
            }
        }

        private sealed class Slot
        {
            public readonly HandCardView View;
            public readonly HandCardTextView Text;
            public readonly RectTransform Rect;
            public readonly HandCardRuntimeDragRelay Relay;
            private readonly CanvasGroup _group;
            private readonly Vector2 _position;
            private readonly float _rotation;
            private readonly int _index;

            public Slot(HandCardView view, HandCardTextView text, int index)
            {
                View = view;
                Text = text;
                Rect = Get<RectTransform>(text, "_runtimeCardRect");
                Relay = Rect.GetComponent<HandCardRuntimeDragRelay>();
                _group = Rect.GetComponent<CanvasGroup>();
                _position = new Vector2(-180f + index * 180f, 30f + index * 12f);
                _rotation = -12f + index * 8f;
                _index = index;
            }

            public void Layout(bool visible = true) => Text.ApplyRuntimeLayout(
                _position, _rotation, new Vector2(270f, 378f), 1f, _index, visible);

            public void AssertDragging()
            {
                Assert.That(Get<bool>(Text, "_isRuntimeDragActive"), Is.True);
                Assert.That(_group.blocksRaycasts, Is.False);
                Assert.That(_group.interactable, Is.False);
                Assert.That(Vector2.Distance(Rect.anchoredPosition, _position), Is.GreaterThan(20f));
            }

            public void AssertNotDragging() =>
                Assert.That(Get<bool>(Text, "_isRuntimeDragActive"), Is.False);

            public IEnumerator SettleGeometry()
            {
                // Cancellation/release and input recovery are synchronous requirements.
                // Only visual interpolation may take additional batch-mode frames.
                Assert.That(Get<bool>(Text, "_isRuntimeDragActive"), Is.False);
                Assert.That(_group.blocksRaycasts, Is.True);
                Assert.That(_group.interactable, Is.True);
                var deadline = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < deadline &&
                       (Vector2.Distance(Rect.anchoredPosition, _position) >= .5f ||
                        Quaternion.Angle(Rect.localRotation, Quaternion.Euler(0f, 0f, _rotation)) >= .2f ||
                        Mathf.Abs(Rect.localScale.x - 1f) >= .005f))
                {
                    yield return null;
                }
            }
            public void AssertResting(string context = null)
            {
                Assert.That(Get<bool>(Text, "_isRuntimeDragActive"), Is.False, context);
                Assert.That(Rect.gameObject.activeInHierarchy, Is.True, context);
                Assert.That(_group.alpha, Is.EqualTo(1f), context);
                Assert.That(_group.blocksRaycasts, Is.True, context + " must restore the touch hitbox.");
                Assert.That(_group.interactable, Is.True, context);
                Assert.That(Vector2.Distance(Rect.anchoredPosition, _position), Is.LessThan(.5f), context);
                Assert.That(Quaternion.Angle(Rect.localRotation, Quaternion.Euler(0f, 0f, _rotation)), Is.LessThan(.2f), context);
                Assert.That(Rect.localScale.x, Is.EqualTo(1f).Within(.005f), context);
                Assert.That(Rect.GetComponent<Canvas>().sortingOrder, Is.EqualTo(1200 + _index), context);
            }
        }
    }
}