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
        private bool _suppressNextClick;
        private float _pointerDownStartedAt;

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
                _suppressNextClick = true;
                _tileView?.SuppressNextClick();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
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
            if (_previewShown && _owner != null)
            {
                _owner.HideCurrentCardPreview();
            }

            ResetState();
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
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
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
            _pointerDown = false;
            _previewShown = false;
            _pointerDownStartedAt = 0f;
        }
    }
}
