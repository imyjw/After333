using TMPro;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class CombatLogTextView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [TextArea(4, 12)]
        [SerializeField] private string _fallbackText = "No actions yet.";

        public void SetLog(string logText)
        {
            if (_text == null)
            {
                return;
            }

            _text.text = BattleRichTextStyler.StyleCombatLog(logText, _fallbackText);
        }
    }
}
