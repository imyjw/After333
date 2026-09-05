using TMPro;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleOutcomeTextView : MonoBehaviour
    {
        [SerializeField] private BattleBootstrapper _battleBootstrapper;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private float _pulseScale = 1.06f;
        [SerializeField] private float _pulseSpeed = 4f;

        private string _lastOutcomeText;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            if (_text != null)
            {
                _baseScale = _text.rectTransform.localScale;
            }
        }

        private void LateUpdate()
        {
            Refresh();
            ApplyPulse();
        }

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            var outcomeText = BattleOutcomeFormatter.Format(_battleBootstrapper?.CurrentBattleState);
            if (_text == null || _lastOutcomeText == outcomeText)
            {
                return;
            }

            _text.text = BattleRichTextStyler.StyleOutcome(outcomeText);
            _lastOutcomeText = outcomeText;
        }

        private void ApplyPulse()
        {
            if (_text == null)
            {
                return;
            }

            var hasOutcome = !string.IsNullOrWhiteSpace(_lastOutcomeText);
            var targetScale = _baseScale;

            if (hasOutcome)
            {
                var pulse = 1f + (Mathf.Sin(Time.unscaledTime * _pulseSpeed) * (_pulseScale - 1f));
                targetScale = _baseScale * pulse;
            }

            _text.rectTransform.localScale = Vector3.Lerp(
                _text.rectTransform.localScale,
                targetScale,
                Time.unscaledDeltaTime * 10f);
        }
    }
}
