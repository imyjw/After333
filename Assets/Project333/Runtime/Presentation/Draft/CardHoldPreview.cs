using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project333.Runtime.Presentation.Draft
{
    [DisallowMultipleComponent]
    public sealed class CardHoldPreview : MonoBehaviour, IPointerDownHandler,
        IPointerUpHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private Func<string> _identity;
        private Action<Component> _show;
        private Action _pressStarted;
        private Action _previewReleased;
        private PointerEventData _pointer;
        private string _heldIdentity;
        private Vector2 _startPosition;
        private float _pressedAt;
        private bool _previewTriggered;
        public bool SuppressClick { get; private set; }

        public void Configure(Func<string> identity, Action<Component> show, Action pressStarted = null,
            Action previewReleased = null)
        {
            _identity = identity;
            _show = show;
            _pressStarted = pressStarted;
            _previewReleased = previewReleased;
            if (_pointer != null && _heldIdentity != _identity?.Invoke()) CancelHold();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data == null || data.button != PointerEventData.InputButton.Left || _pointer != null) return;
            CancelHold();
            SuppressClick = false;
            _previewTriggered = false;
            _pressStarted?.Invoke();
            var identity = _identity?.Invoke();
            if (string.IsNullOrEmpty(identity)) return;
            _pointer = data;
            _heldIdentity = identity;
            _startPosition = data.position;
            _pressedAt = Time.unscaledTime;
        }

        private void Update() => AdvanceHold(Time.unscaledTime);
        private void AdvanceHold(float now)
        {
            if (_pointer == null) return;
            if (_heldIdentity != _identity?.Invoke()) { SuppressClick = true; CancelHold(); return; }
            if (HasMoved(_pointer)) { SuppressClick = true; CancelHold(); return; }
            if (SuppressClick || now - _pressedAt < 0.4f) return;
            _show?.Invoke(this);
            _previewTriggered = true;
            SuppressClick = true;
        }

        private bool HasMoved(PointerEventData data)
        {
            var threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 12;
            return (data.position - _startPosition).sqrMagnitude > threshold * threshold;
        }

        public void OnPointerMove(PointerEventData data)
        {
            if (Owns(data) && HasMoved(data)) { SuppressClick = true; CancelHold(); }
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (!Owns(data)) return;
            var completed = _previewTriggered && data.button == PointerEventData.InputButton.Left &&
                !HasMoved(data) && _heldIdentity == _identity?.Invoke() && DeckCardPreviewOverlay.IsVisibleFor(this);
            var notifyReleased = _previewReleased;
            if (_heldIdentity != _identity?.Invoke()) SuppressClick = true;
            if (SuppressClick) data.eligibleForClick = false;
            CancelHold();
            // The callback may switch the tutorial step and disable this object.
            // Close the real preview and suppress its click before notifying it.
            if (completed) notifyReleased?.Invoke();
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (Owns(data)) CancelHold();
        }

        // Called by the existing hand drag relay; this component does not capture drag events.
        public bool TryBeginDrag()
        {
            var previewWasShown = _previewTriggered;
            CancelHold();
            SuppressClick = true;
            return !previewWasShown;
        }

        private bool Owns(PointerEventData data) =>
            _pointer != null && data != null && data.pointerId == _pointer.pointerId;

        public void CancelHold()
        {
            DeckCardPreviewOverlay.HideFor(this);
            _pointer = null;
            _heldIdentity = null;
        }

        private void OnDisable() { SuppressClick = true; CancelHold(); }
        private void OnApplicationFocus(bool focused) { if (!focused) { SuppressClick = true; CancelHold(); } }
        private void OnApplicationPause(bool paused) { if (paused) { SuppressClick = true; CancelHold(); } }
    }
}
