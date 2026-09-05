using TMPro;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleSummaryTextView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [TextArea(4, 12)]
        [SerializeField] private string _fallbackText = "No active battle.";

        public void SetSummary(string summary)
        {
            if (_text == null)
            {
                return;
            }

            _text.text = BattleRichTextStyler.StyleSummary(summary, _fallbackText);
        }
    }
}
