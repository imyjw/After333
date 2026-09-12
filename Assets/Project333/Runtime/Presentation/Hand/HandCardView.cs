using System;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;
using UnityEngine.Events;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed class HandCardView : MonoBehaviour
    {
        [System.Serializable]
        public sealed class HandCardChangedEvent : UnityEvent<string, bool>
        {
        }

        [SerializeField] private int _slotIndex;
        [TextArea(2, 4)]
        [SerializeField] private string _label = "Slot 0: Empty";
        [SerializeField] private bool _hasCard;
        [SerializeField] private string _cardId;
        [SerializeField] private string _runtimeId;
        [SerializeField] private bool _isTemporaryReplicate;
        [SerializeField] private BattleHighlightState _highlightState;
        [SerializeField] private HandCardChangedEvent _onHandCardChanged = new HandCardChangedEvent();

        public event Action<HandCardView> Clicked;
        public event Action<HandCardView, Vector2> DragStarted;
        public event Action<HandCardView, Vector2> Dragged;
        public event Action<HandCardView, Vector2> DragEnded;

        public int SlotIndex => _slotIndex;

        public string Label => _label;

        public bool HasCard => _hasCard;

        public string CardId => _cardId;

        public string RuntimeId => _runtimeId;

        public bool IsTemporaryReplicate => _isTemporaryReplicate;

        public int SpellPower { get; private set; }

        public BattleHighlightState HighlightState => _highlightState;

        // Availability is independent of the selected visual overlay.
        public bool IsPlayable { get; private set; }

        public void Configure(int slotIndex)
        {
            _slotIndex = slotIndex;
            _label = BattleUiFormatter.FormatHandCard(_slotIndex, _cardId);
        }

        public void Present(string cardId)
        {
            Present(cardId, string.Empty, isTemporaryReplicate: false);
        }

        public void Present(string cardId, string runtimeId, bool isTemporaryReplicate, int spellPower = 0)
        {
            var nextCardId = cardId ?? string.Empty;
            var nextRuntimeId = runtimeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextCardId) ||
                !string.Equals(_cardId, nextCardId, StringComparison.Ordinal) ||
                !string.Equals(_runtimeId, nextRuntimeId, StringComparison.Ordinal))
            {
                IsPlayable = false;
            }

            SpellPower = spellPower;
            _cardId = nextCardId;
            _runtimeId = nextRuntimeId;
            _isTemporaryReplicate = isTemporaryReplicate;
            _hasCard = !string.IsNullOrWhiteSpace(cardId);
            _label = BattleUiFormatter.FormatHandCard(_slotIndex, cardId);
            _onHandCardChanged.Invoke(_label, _hasCard);
        }

        public void SetHighlight(BattleHighlightState highlightState)
        {
            // RefreshHighlights clears availability, recalculates it, then overlays selection.
            IsPlayable = highlightState == BattleHighlightState.Playable ||
                         (highlightState == BattleHighlightState.Selected && IsPlayable);
            _highlightState = highlightState;
        }

        public void NotifyClicked()
        {
            Clicked?.Invoke(this);
        }

        public void NotifyDragStarted(Vector2 screenPosition)
        {
            DragStarted?.Invoke(this, screenPosition);
        }

        public void NotifyDragged(Vector2 screenPosition)
        {
            Dragged?.Invoke(this, screenPosition);
        }

        public void NotifyDragEnded(Vector2 screenPosition)
        {
            DragEnded?.Invoke(this, screenPosition);
        }
    }
}
