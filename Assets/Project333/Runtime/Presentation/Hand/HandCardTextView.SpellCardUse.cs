using System;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed partial class HandCardTextView
    {
        private string _suppressedSpellUseRuntimeId;
        private bool IsSpellUseSuppressed => _handCardView != null && _handCardView.HasCard &&
            !string.IsNullOrEmpty(_suppressedSpellUseRuntimeId) &&
            string.Equals(_suppressedSpellUseRuntimeId, _handCardView.RuntimeId, StringComparison.Ordinal);

        // Visibility follows identity, never the pooled slot or a fixed animation timer.
        internal void SetSpellUseSuppression(string runtimeId)
        {
            _suppressedSpellUseRuntimeId = runtimeId;
            if (_runtimeCanvasGroup == null) return;
            var visible = _targetRuntimeVisible && _hasCard && !IsHandPresentationSuppressed;
            _runtimeCanvasGroup.alpha = visible ? 1f : 0f;
            _runtimeCanvasGroup.blocksRaycasts = visible;
            _runtimeCanvasGroup.interactable = visible;
        }
    }
}
