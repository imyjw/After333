using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using UnityEngine;
using UnityEngine.Events;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class TileView : MonoBehaviour
    {
        [System.Serializable]
        public sealed class TileChangedEvent : UnityEvent<string, string, bool>
        {
        }

        [SerializeField] private PlayerId _ownerId = PlayerId.Player;
        [SerializeField] private int _column;
        [SerializeField] private int _row;
        [SerializeField] private string _title;
        [TextArea(4, 12)]
        [SerializeField] private string _content = "Empty";
        [SerializeField] private bool _isOccupied;
        [SerializeField] private string _currentOccupantCardId = string.Empty;
        [SerializeField] private string _currentOccupantRuntimeId = string.Empty;
        [SerializeField] private BattleHighlightState _highlightState;
        [SerializeField] private TileChangedEvent _onTileChanged = new TileChangedEvent();

        private OccupantState _currentOccupant;
        private bool _suppressNextClick;

        public event Action<TileView> Clicked;

        public PlayerId OwnerId => _ownerId;

        public TileCoord Coord => new TileCoord(_column, _row);

        public string Title => _title;

        public string Content => _content;

        public bool IsOccupied => _isOccupied;

        public string CurrentOccupantCardId => _currentOccupantCardId;

        public string CurrentOccupantRuntimeId => _currentOccupantRuntimeId;

        public OccupantState CurrentOccupant => _currentOccupant;

        public BattleHighlightState HighlightState => _highlightState;

        public void Configure(PlayerId ownerId, int column, int row)
        {
            _ownerId = ownerId;
            _column = column;
            _row = row;
            _title = BattleUiFormatter.FormatTileTitle(_ownerId, _column, _row);
        }

        public void Present(OccupantState occupant)
        {
            _currentOccupant = occupant;
            _currentOccupantCardId = occupant?.CardId ?? string.Empty;
            _currentOccupantRuntimeId = occupant?.RuntimeId ?? string.Empty;
            _title = BattleUiFormatter.FormatTileTitle(_ownerId, _column, _row);
            _content = BattleUiFormatter.FormatTileOccupant(occupant);
            _isOccupied = occupant != null;
            _onTileChanged.Invoke(_title, _content, _isOccupied);
        }

        public void SetHighlight(BattleHighlightState highlightState)
        {
            _highlightState = highlightState;
        }

        public void SuppressNextClick()
        {
            _suppressNextClick = true;
        }

        public void NotifyClicked()
        {
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
                return;
            }

            Clicked?.Invoke(this);
        }
    }
}
