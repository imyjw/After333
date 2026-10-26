using System;
using UnityEngine;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed partial class HandCardTextView
    {
        private string _drawSuppressedRuntimeId;
        private bool IsDrawSuppressed => _handCardView != null && _handCardView.HasCard &&
            !string.IsNullOrEmpty(_drawSuppressedRuntimeId) &&
            string.Equals(_drawSuppressedRuntimeId, _handCardView.RuntimeId, StringComparison.Ordinal);
        private bool IsHandPresentationSuppressed => IsSpellUseSuppressed || IsDrawSuppressed;

        internal void SetDrawSuppression(string runtimeId)
        {
            _drawSuppressedRuntimeId = runtimeId;
            if (_runtimeCanvasGroup == null) return;
            var visible = _targetRuntimeVisible && _hasCard && !IsHandPresentationSuppressed;
            _runtimeCanvasGroup.alpha = visible ? 1 : 0;
            _runtimeCanvasGroup.blocksRaycasts = visible;
            _runtimeCanvasGroup.interactable = visible;
        }

        // Settle only the hidden destination, never animate/reparent the real input object.
        internal UnityEngine.UI.Image PrepareDrawTarget()
        {
            if (!IsDrawSuppressed || !HasRuntimeVisual || IsSpellUseSuppressed) return null;
            // Present binds the model before this slot's LateUpdate refreshes artwork/stats.
            Refresh();
            _runtimeCardRect.anchoredPosition = _targetRuntimeAnchoredPosition;
            _runtimeCardRect.sizeDelta = _targetRuntimeSize;
            _runtimeCardRect.localRotation = Quaternion.Euler(0, 0, _targetRuntimeRotationDegrees);
            _runtimeCardRect.localScale = Vector3.one * _targetRuntimeScale;
            _runtimeCardRect.ForceUpdateRectTransforms();
            TutorialArtwork?.rectTransform.ForceUpdateRectTransforms();
            return TutorialArtwork;
        }
    }
}
