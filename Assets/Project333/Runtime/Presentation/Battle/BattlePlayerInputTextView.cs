using TMPro;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattlePlayerInputTextView : MonoBehaviour
    {
        [SerializeField] private BattlePlayerInputController _inputController;
        [SerializeField] private TMP_Text _selectedCardText;
        [SerializeField] private TMP_Text _selectedOccupantText;
        [SerializeField] private TMP_Text _interactionStatusText;

        private string _lastSelectedCardText;
        private string _lastSelectedOccupantText;
        private string _lastInteractionStatusText;

        private void LateUpdate()
        {
            Refresh();
        }

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            var selectedCardText = _inputController == null
                ? BattlePlayerInputFormatter.FormatSelectedCard(string.Empty)
                : BattlePlayerInputFormatter.FormatSelectedCard(_inputController.SelectedCardId);

            var selectedOccupantText = _inputController == null
                ? BattlePlayerInputFormatter.FormatSelectedOccupant(false, default)
                : BattlePlayerInputFormatter.FormatSelectedOccupant(
                    _inputController.HasSelectedUnit,
                    _inputController.SelectedUnitCoord);

            var interactionStatusText = _inputController == null
                ? BattlePlayerInputFormatter.FormatInteractionStatus(string.Empty)
                : BattlePlayerInputFormatter.FormatInteractionStatus(_inputController.InteractionStatus);

            if (_selectedCardText != null && _lastSelectedCardText != selectedCardText)
            {
                _selectedCardText.text = BattleRichTextStyler.StylePlayerInputLabel(selectedCardText);
                _lastSelectedCardText = selectedCardText;
            }

            if (_selectedOccupantText != null && _lastSelectedOccupantText != selectedOccupantText)
            {
                _selectedOccupantText.text = BattleRichTextStyler.StylePlayerInputLabel(selectedOccupantText);
                _lastSelectedOccupantText = selectedOccupantText;
            }

            if (_interactionStatusText != null && _lastInteractionStatusText != interactionStatusText)
            {
                _interactionStatusText.text = BattleRichTextStyler.StyleInteractionStatus(interactionStatusText);
                _lastInteractionStatusText = interactionStatusText;
            }
        }
    }
}
