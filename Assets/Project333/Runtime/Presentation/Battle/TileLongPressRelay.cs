using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project333.Runtime.Presentation.Battle
{
    [DisallowMultipleComponent]
    public sealed class TileLongPressRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        private TileTextView _owner;
        private TileView _tileView;
        private bool _pointerDown;
        private bool _previewShown;
        private bool _suppressCurrentPressClick;
        private float _pointerDownStartedAt;

        public TileView TileView => _tileView;
        public event Action<TileView> PreviewReleased;

        public void Initialize(TileTextView owner, TileView tileView)
        {
            _owner = owner;
            _tileView = tileView;
        }

        private void Update()
        {
            if (!_pointerDown || _previewShown || _owner == null)
            {
                return;
            }

            if (Time.unscaledTime - _pointerDownStartedAt < _owner.LongPressPreviewHoldSeconds)
            {
                return;
            }

            if (_owner.TryShowCurrentCardPreview())
            {
                _previewShown = true;
                _suppressCurrentPressClick = true;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // A canceled/long press must never suppress a later, independent press.
            _suppressCurrentPressClick = false;

            if (_owner == null || !_owner.CanShowCurrentCardPreview())
            {
                return;
            }

            _pointerDown = true;
            _previewShown = false;
            _pointerDownStartedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var completedPreview = _pointerDown && _previewShown;
            if (_previewShown && _owner != null)
            {
                _owner.HideCurrentCardPreview();
            }

            ResetState();
            if (completedPreview) PreviewReleased?.Invoke(_tileView);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_previewShown && _owner != null)
            {
                _owner.HideCurrentCardPreview();
            }

            ResetState();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_suppressCurrentPressClick)
            {
                _suppressCurrentPressClick = false;
                return;
            }

            _tileView?.NotifyClicked();
        }

        private void OnDisable()
        {
            if (_previewShown && _owner != null)
            {
                _owner.HideCurrentCardPreview();
            }

            ResetState();
        }

        private void ResetState()
        {
            // Keep click suppression until this press clicks or a new press begins.
            // PointerUp/Exit can precede the release click of a long press.
            _pointerDown = false;
            _previewShown = false;
            _pointerDownStartedAt = 0f;
        }
    }
}
