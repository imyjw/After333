using TMPro;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;

namespace Project333.Runtime.Presentation.HUD
{
    public sealed class ResourceBarTextView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _turnText;
        [SerializeField] private TMP_Text _playerResourceText;
        [SerializeField] private TMP_Text _aiResourceText;

        public void SetResourceSummary(string turnSummary, string playerResourceSummary, string aiResourceSummary)
        {
            if (_turnText != null)
            {
                _turnText.text = BattleRichTextStyler.StyleTurnSummary(turnSummary);
            }

            if (_playerResourceText != null)
            {
                _playerResourceText.text = BattleRichTextStyler.StyleResourceSummary("PLAYER", playerResourceSummary);
            }

            if (_aiResourceText != null)
            {
                _aiResourceText.text = BattleRichTextStyler.StyleResourceSummary("AI", aiResourceSummary);
            }
        }
    }
}
