using TMPro;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Retains the authored scene references while replacing the old small outcome text.
    public sealed class BattleOutcomeTextView : MonoBehaviour
    {
        [SerializeField] private BattleBootstrapper _battleBootstrapper;
        [SerializeField] private TMP_Text _text;

        private BattleOutcomeBannerView _banner;

        private void Awake()
        {
            ClearLegacyText();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            ClearLegacyText();
            var state = _battleBootstrapper == null ? null : _battleBootstrapper.CurrentBattleState;
            var outcome = BattleOutcomeFormatter.Format(state);
            if (string.IsNullOrEmpty(outcome))
            {
                _banner?.Hide();
                return;
            }
            // Finish lethal reactions and every already accepted card reveal before covering the center.
            if (_battleBootstrapper.IsBusy || _battleBootstrapper.IsOpponentPlayedCardRevealPending) return;
            if (_banner == null)
            {
                var bannerObject = new GameObject("BattleOutcomeBanner", typeof(RectTransform));
                bannerObject.transform.SetParent(transform, false);
                _banner = bannerObject.AddComponent<BattleOutcomeBannerView>();
            }
            _banner.Show(outcome);
        }

        private void ClearLegacyText()
        {
            if (_text == null) return;
            _text.text = string.Empty;
            _text.raycastTarget = false;
        }

        private void OnDisable()
        {
            _banner?.Hide();
            ClearLegacyText();
        }
    }
}
