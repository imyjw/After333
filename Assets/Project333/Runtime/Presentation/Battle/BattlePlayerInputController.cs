using System;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattlePlayerInputController : MonoBehaviour
    {
        [SerializeField] private BattleBootstrapper _battleBootstrapper;
        [SerializeField] private BoardPresenter _boardPresenter;
        [SerializeField] private HandPresenter _handPresenter;
        [SerializeField] private string _selectedCardId;
        [SerializeField] private int _selectedSlotIndex = -1;
        [SerializeField] private bool _hasSelectedUnit;
        [SerializeField] private int _selectedUnitColumn;
        [SerializeField] private int _selectedUnitRow;
        [SerializeField] private float _selfCastDragReleaseThreshold = 140f;
        [TextArea(2, 6)]
        [SerializeField] private string _interactionStatus = "Select a hand card.";
        private readonly TargetingService _targetingService = new TargetingService();
        private bool _isDraggingCard;
        private string _draggedCardId = string.Empty;
        private int _draggedSlotIndex = -1;
        private Vector2 _dragStartScreenPosition;
        private Vector2 _dragCurrentScreenPosition;
        private TileView _dragHoveredTileView;

        public string SelectedCardId => _selectedCardId;

        public int SelectedSlotIndex => _selectedSlotIndex;

        public bool HasSelectedUnit => _hasSelectedUnit;

        public TileCoord SelectedUnitCoord => new TileCoord(_selectedUnitColumn, _selectedUnitRow);

        public string InteractionStatus => _interactionStatus;

        private void Start()
        {
            RebindViews();
        }

        private void LateUpdate()
        {
            RefreshHighlights();
        }

        private void OnDestroy()
        {
            UnbindViews();
        }

        [ContextMenu("Rebind Views")]
        public void RebindViews()
        {
            UnbindViews();

            if (_handPresenter != null)
            {
                foreach (var handCardView in _handPresenter.HandCardViews)
                {
                    if (handCardView == null)
                    {
                        continue;
                    }

                    handCardView.Clicked += HandleHandCardClicked;
                    handCardView.DragStarted += HandleHandCardDragStarted;
                    handCardView.Dragged += HandleHandCardDragged;
                    handCardView.DragEnded += HandleHandCardDragEnded;
                }
            }

            if (_boardPresenter != null)
            {
                foreach (var tileView in _boardPresenter.PlayerTileViews)
                {
                    if (tileView == null)
                    {
                        continue;
                    }

                    tileView.Clicked += HandleTileClicked;
                }

                foreach (var tileView in _boardPresenter.AITileViews)
                {
                    if (tileView == null)
                    {
                        continue;
                    }

                    tileView.Clicked += HandleTileClicked;
                }
            }
        }

        public void ClearSelection()
        {
            ClearCardSelection();
            ClearUnitSelection();
            ClearHandCardDragState();
            _interactionStatus = "Selection cleared.";
        }

        public void CastSelectedPersistentSpellFromUi()
        {
            if (_battleBootstrapper == null)
            {
                _interactionStatus = "Battle bootstrapper is not assigned.";
                return;
            }

            if (_battleBootstrapper.IsBusy)
            {
                _interactionStatus = "Animations are still resolving.";
                return;
            }

            var wasCast = _battleBootstrapper.TryCastPlayerPersistentSpell(_selectedCardId, out var message);
            _interactionStatus = message;

            if (wasCast)
            {
                ClearCardSelection();
            }
        }

        [ContextMenu("Refresh Highlights")]
        public void RefreshHighlights()
        {
            ClearAllHighlights();

            var battleState = _battleBootstrapper?.CurrentBattleState;
            if (battleState == null)
            {
                HighlightSelectedHandCard();
                return;
            }

            HighlightPlayableHandCards(battleState);
            HighlightSelectedHandCard();

            if (_isDraggingCard)
            {
                HighlightDragHoverTarget();
                return;
            }

            if (_hasSelectedUnit)
            {
                HighlightSelectedUnit(battleState);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_selectedCardId))
            {
                HighlightCardTargets(battleState);
            }
        }

        private void HighlightPlayableHandCards(BattleState battleState)
        {
            if (_handPresenter == null ||
                _battleBootstrapper == null ||
                battleState == null ||
                battleState.Phase != PhaseType.Main ||
                battleState.ActivePlayerId != PlayerId.Player)
            {
                return;
            }

            foreach (var handCardView in _handPresenter.HandCardViews)
            {
                if (handCardView == null || !handCardView.HasCard || string.IsNullOrWhiteSpace(handCardView.CardId))
                {
                    continue;
                }

                if (!_battleBootstrapper.TryGetCardDefinition(handCardView.CardId, out var definition) || definition == null)
                {
                    continue;
                }

                if (battleState.Player.Resources.CanAfford(definition.Cost))
                {
                    handCardView.SetHighlight(BattleHighlightState.Playable);
                }
            }
        }

        private void UnbindViews()
        {
            if (_handPresenter != null)
            {
                foreach (var handCardView in _handPresenter.HandCardViews)
                {
                    if (handCardView == null)
                    {
                        continue;
                    }

                    handCardView.Clicked -= HandleHandCardClicked;
                    handCardView.DragStarted -= HandleHandCardDragStarted;
                    handCardView.Dragged -= HandleHandCardDragged;
                    handCardView.DragEnded -= HandleHandCardDragEnded;
                }
            }

            if (_boardPresenter != null)
            {
                foreach (var tileView in _boardPresenter.PlayerTileViews)
                {
                    if (tileView == null)
                    {
                        continue;
                    }

                    tileView.Clicked -= HandleTileClicked;
                }

                foreach (var tileView in _boardPresenter.AITileViews)
                {
                    if (tileView == null)
                    {
                        continue;
                    }

                    tileView.Clicked -= HandleTileClicked;
                }
            }
        }

        private void HandleHandCardClicked(HandCardView handCardView)
        {
            if (handCardView == null)
            {
                return;
            }

            if (_battleBootstrapper != null && _battleBootstrapper.IsBusy)
            {
                _interactionStatus = "Animations are still resolving.";
                return;
            }

            if (!handCardView.HasCard || string.IsNullOrWhiteSpace(handCardView.CardId))
            {
                ClearCardSelection();
                ClearUnitSelection();
                _interactionStatus = "Selected hand slot is empty.";
                return;
            }

            if (_selectedSlotIndex == handCardView.SlotIndex && _selectedCardId == handCardView.CardId)
            {
                ClearCardSelection();
                _interactionStatus = $"Cancelled hand card selection from slot {handCardView.SlotIndex}.";
                return;
            }

            SetCardSelection(handCardView.CardId, handCardView.SlotIndex);
            _interactionStatus = BuildSelectionMessage(_selectedCardId, _selectedSlotIndex);
        }

        private void HandleHandCardDragStarted(HandCardView handCardView, Vector2 screenPosition)
        {
            if (handCardView == null || !handCardView.HasCard || string.IsNullOrWhiteSpace(handCardView.CardId))
            {
                return;
            }

            if (_battleBootstrapper != null && _battleBootstrapper.IsBusy)
            {
                _interactionStatus = "Animations are still resolving.";
                return;
            }

            SetCardSelection(handCardView.CardId, handCardView.SlotIndex);
            _isDraggingCard = true;
            _draggedCardId = handCardView.CardId;
            _draggedSlotIndex = handCardView.SlotIndex;
            _dragStartScreenPosition = screenPosition;
            _dragCurrentScreenPosition = screenPosition;
            _dragHoveredTileView = null;
            _interactionStatus = BuildDragMessage(handCardView.CardId);
        }

        private void HandleHandCardDragged(HandCardView handCardView, Vector2 screenPosition)
        {
            if (!_isDraggingCard ||
                handCardView == null ||
                handCardView.CardId != _draggedCardId ||
                handCardView.SlotIndex != _draggedSlotIndex)
            {
                return;
            }

            _dragCurrentScreenPosition = screenPosition;
            _dragHoveredTileView = ResolveLegalDragHoverTile(screenPosition);
        }

        private void HandleHandCardDragEnded(HandCardView handCardView, Vector2 screenPosition)
        {
            if (!_isDraggingCard ||
                handCardView == null ||
                handCardView.CardId != _draggedCardId ||
                handCardView.SlotIndex != _draggedSlotIndex)
            {
                return;
            }

            _dragCurrentScreenPosition = screenPosition;

            try
            {
                var cardId = _draggedCardId;
                var battleState = _battleBootstrapper?.CurrentBattleState;
                var hoveredTileView = ResolveLegalDragHoverTile(screenPosition);
                var dragReleasedUpwardEnough = screenPosition.y - _dragStartScreenPosition.y >= _selfCastDragReleaseThreshold;

                if (hoveredTileView != null && CanCardUseOnTile(battleState, cardId, hoveredTileView))
                {
                    _battleBootstrapper.TryUsePlayerCardOnTile(cardId, hoveredTileView.OwnerId, hoveredTileView.Coord, out var message);
                    _interactionStatus = message;
                    ClearCardSelection();
                    return;
                }

                if (dragReleasedUpwardEnough && CanCardCastWithoutTarget(battleState, cardId))
                {
                    _battleBootstrapper.TryCastPlayerPersistentSpell(cardId, out var message);
                    _interactionStatus = message;
                    ClearCardSelection();
                    return;
                }

                ClearCardSelection();
                _interactionStatus = $"Cancelled drag for '{cardId}'.";
            }
            finally
            {
                ClearHandCardDragState();
            }
        }

        private void HandleTileClicked(TileView tileView)
        {
            if (_isDraggingCard)
            {
                return;
            }

            if (tileView == null)
            {
                return;
            }

            if (_battleBootstrapper == null)
            {
                _interactionStatus = "Battle bootstrapper is not assigned.";
                return;
            }

            if (_battleBootstrapper.IsBusy)
            {
                _interactionStatus = "Animations are still resolving.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(_selectedCardId))
            {
                if (tileView.OwnerId == PlayerId.Player)
                {
                    var playerBattleState = _battleBootstrapper.CurrentBattleState;
                    var playerOccupant = playerBattleState?.PlayerBoard?.GetOccupant(tileView.Coord);
                    if (playerOccupant != null && CanSelectedCardTargetOccupant(playerBattleState, playerOccupant))
                    {
                        var wasPlayedOnAlly = _battleBootstrapper.TryUsePlayerCardOnTile(_selectedCardId, tileView.OwnerId, tileView.Coord, out var alliedTargetMessage);
                        _interactionStatus = alliedTargetMessage;

                        if (wasPlayedOnAlly)
                        {
                            ClearCardSelection();
                        }

                        return;
                    }

                    if (playerOccupant != null)
                    {
                        ClearCardSelection();

                        if (!CanOccupantAct(playerOccupant))
                        {
                            _interactionStatus = "This occupant cannot currently move or attack.";
                            return;
                        }

                        SetUnitSelection(tileView.Coord);
                        _interactionStatus = BuildOccupantSelectionMessage(tileView.Coord, playerOccupant);
                        return;
                    }
                }

                var wasPlayed = _battleBootstrapper.TryUsePlayerCardOnTile(_selectedCardId, tileView.OwnerId, tileView.Coord, out var message);
                _interactionStatus = message;

                if (wasPlayed)
                {
                    ClearCardSelection();
                }

                return;
            }

            if (tileView.OwnerId != PlayerId.Player)
            {
                if (!_hasSelectedUnit)
                {
                    _interactionStatus = "Select a hand card first, or click your own occupant to prepare a move or attack.";
                    return;
                }

                var attackFromCoord = new TileCoord(_selectedUnitColumn, _selectedUnitRow);
                var wasAttacked = _battleBootstrapper.TryAttackWithPlayerOccupant(attackFromCoord, tileView.Coord, out var attackMessage);
                _interactionStatus = attackMessage;

                if (wasAttacked)
                {
                    ClearUnitSelection();
                }

                return;
            }

            var battleState = _battleBootstrapper.CurrentBattleState;
            var occupant = battleState?.PlayerBoard?.GetOccupant(tileView.Coord);

            if (!_hasSelectedUnit)
            {
                if (occupant == null)
                {
                    _interactionStatus = "Click one of your own occupants first, or select a hand card.";
                    return;
                }

                if (!CanOccupantAct(occupant))
                {
                    _interactionStatus = "This occupant cannot currently move or attack.";
                    return;
                }

                SetUnitSelection(tileView.Coord);
                _interactionStatus = BuildOccupantSelectionMessage(tileView.Coord, occupant);
                return;
            }

            var fromCoord = new TileCoord(_selectedUnitColumn, _selectedUnitRow);
            if (fromCoord == tileView.Coord)
            {
                ClearUnitSelection();
                _interactionStatus = $"Cancelled unit selection at {fromCoord}.";
                return;
            }

            if (occupant != null)
            {
                var selectedOccupant = battleState?.PlayerBoard?.GetOccupant(fromCoord);
                if (CanOccupantMove(selectedOccupant) && CanOccupantMove(occupant))
                {
                    var wasSwapped = _battleBootstrapper.TryMovePlayerOccupant(fromCoord, tileView.Coord, out var swapMessage);
                    _interactionStatus = swapMessage;

                    if (wasSwapped)
                    {
                        ClearUnitSelection();
                    }

                    return;
                }

                if (!CanOccupantAct(occupant))
                {
                    _interactionStatus = "This occupant cannot currently move or attack.";
                    return;
                }

                SetUnitSelection(tileView.Coord);
                _interactionStatus = BuildOccupantSelectionMessage(tileView.Coord, occupant);
                return;
            }

            var wasMoved = _battleBootstrapper.TryMovePlayerOccupant(fromCoord, tileView.Coord, out var moveMessage);
            _interactionStatus = moveMessage;

            if (wasMoved)
            {
                ClearUnitSelection();
            }
        }

        private void HighlightCardTargets(BattleState battleState)
        {
            if (battleState.Phase != PhaseType.Main || battleState.ActivePlayerId != PlayerId.Player)
            {
                return;
            }

            if (!_battleBootstrapper.TryGetCardDefinition(_selectedCardId, out var definition) ||
                definition == null ||
                !battleState.Player.Hand.Contains(_selectedCardId) ||
                !battleState.Player.Resources.CanAfford(definition.Cost))
            {
                return;
            }

            switch (definition.CardType)
            {
                case CardType.Unit:
                case CardType.Building:
                    foreach (var tileView in _boardPresenter.PlayerTileViews)
                    {
                        if (tileView == null)
                        {
                            continue;
                        }

                        if (battleState.PlayerBoard.GetOccupant(tileView.Coord) == null)
                        {
                            tileView.SetHighlight(BattleHighlightState.Playable);
                        }
                    }

                    break;

                case CardType.Spell:
                    if (definition is DamageSpellCardDefinition)
                    {
                        foreach (var tileView in _boardPresenter.AITileViews)
                        {
                            if (tileView == null)
                            {
                                continue;
                            }

                            if (battleState.AIBoard.GetOccupant(tileView.Coord) != null)
                            {
                                tileView.SetHighlight(BattleHighlightState.SpellTarget);
                            }
                        }
                    }
                    else if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                             string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal))
                    {
                        HighlightCheonraJimangTargets(battleState);
                    }

                    break;
            }
        }

        private void HighlightCheonraJimangTargets(BattleState battleState)
        {
            HighlightUnitSpellTargets(_boardPresenter.PlayerTileViews, battleState.PlayerBoard);
            HighlightUnitSpellTargets(_boardPresenter.AITileViews, battleState.AIBoard);
        }

        private static void HighlightUnitSpellTargets(TileView[] tileViews, BoardState boardState)
        {
            foreach (var tileView in tileViews)
            {
                if (tileView == null)
                {
                    continue;
                }

                var occupant = boardState.GetOccupant(tileView.Coord);
                if (occupant?.Kind == OccupantKind.Unit)
                {
                    tileView.SetHighlight(BattleHighlightState.SpellTarget);
                }
            }
        }

        private bool CanSelectedCardTargetOccupant(BattleState battleState, OccupantState occupant)
        {
            if (battleState == null ||
                occupant == null ||
                !_battleBootstrapper.TryGetCardDefinition(_selectedCardId, out var definition) ||
                definition == null ||
                !battleState.Player.Hand.Contains(_selectedCardId) ||
                !battleState.Player.Resources.CanAfford(definition.Cost))
            {
                return false;
            }

            return definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                   string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal) &&
                   occupant.Kind == OccupantKind.Unit;
        }

        private void HighlightSelectedUnit(BattleState battleState)
        {
            var selectedCoord = new TileCoord(_selectedUnitColumn, _selectedUnitRow);
            var selectedTileView = FindTileView(_boardPresenter.PlayerTileViews, selectedCoord);
            selectedTileView?.SetHighlight(BattleHighlightState.Selected);

            if (battleState.Phase != PhaseType.Main || battleState.ActivePlayerId != PlayerId.Player)
            {
                return;
            }

            var selectedOccupant = battleState.PlayerBoard.GetOccupant(selectedCoord);
            if (selectedOccupant == null)
            {
                return;
            }

            if (CanOccupantMove(selectedOccupant))
            {
                foreach (var tileView in _boardPresenter.PlayerTileViews)
                {
                    if (tileView == null || tileView.Coord == selectedCoord)
                    {
                        continue;
                    }

                    var occupant = battleState.PlayerBoard.GetOccupant(tileView.Coord);
                    if (occupant == null)
                    {
                        tileView.SetHighlight(BattleHighlightState.MoveTarget);
                        continue;
                    }

                    if (CanOccupantMove(occupant))
                    {
                        tileView.SetHighlight(BattleHighlightState.SwapTarget);
                    }
                }
            }

            if (!CanOccupantAttack(selectedOccupant))
            {
                return;
            }

            foreach (var targetCoord in _targetingService.GetLegalTargets(battleState, PlayerId.Player, selectedCoord))
            {
                var targetTileView = FindTileView(_boardPresenter.AITileViews, targetCoord);
                targetTileView?.SetHighlight(BattleHighlightState.AttackTarget);
            }
        }

        private void ClearAllHighlights()
        {
            if (_handPresenter != null)
            {
                foreach (var handCardView in _handPresenter.HandCardViews)
                {
                    handCardView?.SetHighlight(BattleHighlightState.None);
                }
            }

            if (_boardPresenter != null)
            {
                foreach (var tileView in _boardPresenter.PlayerTileViews)
                {
                    tileView?.SetHighlight(BattleHighlightState.None);
                }

                foreach (var tileView in _boardPresenter.AITileViews)
                {
                    tileView?.SetHighlight(BattleHighlightState.None);
                }
            }
        }

        private void HighlightSelectedHandCard()
        {
            if (_handPresenter == null || _selectedSlotIndex < 0)
            {
                return;
            }

            foreach (var handCardView in _handPresenter.HandCardViews)
            {
                if (handCardView == null)
                {
                    continue;
                }

                if (handCardView.SlotIndex == _selectedSlotIndex &&
                    handCardView.HasCard &&
                    handCardView.CardId == _selectedCardId)
                {
                    handCardView.SetHighlight(BattleHighlightState.Selected);
                    return;
                }
            }
        }

        private void HighlightDragHoverTarget()
        {
            if (_dragHoveredTileView == null)
            {
                return;
            }

            _dragHoveredTileView.SetHighlight(BattleHighlightState.DragHoverTarget);
        }

        private static TileView FindTileView(TileView[] tileViews, TileCoord coord)
        {
            if (tileViews == null)
            {
                return null;
            }

            foreach (var tileView in tileViews)
            {
                if (tileView != null && tileView.Coord == coord)
                {
                    return tileView;
                }
            }

            return null;
        }

        private void SetCardSelection(string cardId, int slotIndex)
        {
            ClearUnitSelection();
            _selectedCardId = cardId ?? string.Empty;
            _selectedSlotIndex = slotIndex;
        }

        private void ClearCardSelection()
        {
            _selectedCardId = string.Empty;
            _selectedSlotIndex = -1;
        }

        private void ClearHandCardDragState()
        {
            _isDraggingCard = false;
            _draggedCardId = string.Empty;
            _draggedSlotIndex = -1;
            _dragStartScreenPosition = Vector2.zero;
            _dragCurrentScreenPosition = Vector2.zero;
            _dragHoveredTileView = null;
        }

        private void SetUnitSelection(TileCoord coord)
        {
            ClearCardSelection();
            _hasSelectedUnit = true;
            _selectedUnitColumn = coord.Column;
            _selectedUnitRow = coord.Row;
        }

        private void ClearUnitSelection()
        {
            _hasSelectedUnit = false;
            _selectedUnitColumn = 0;
            _selectedUnitRow = 0;
        }

        private static bool CanOccupantAct(OccupantState occupant)
        {
            return CanOccupantMove(occupant) || CanOccupantAttack(occupant);
        }

        private static bool CanOccupantMove(OccupantState occupant)
        {
            return occupant != null && !occupant.IsDisabled && occupant.CanMove;
        }

        private static bool CanOccupantAttack(OccupantState occupant)
        {
            if (occupant == null || occupant.IsDisabled || occupant.HasSummoningSickness || occupant.RemainingAttacksThisTurn <= 0 || occupant.Attack <= 0)
            {
                return false;
            }

            if (occupant is BuildingState buildingState && !buildingState.CanAttackAsBuilding)
            {
                return false;
            }

            return true;
        }

        private TileView ResolveLegalDragHoverTile(Vector2 screenPosition)
        {
            var battleState = _battleBootstrapper?.CurrentBattleState;
            if (!CanDragCardInCurrentState(battleState, _draggedCardId))
            {
                return null;
            }

            foreach (var tileView in EnumerateAllTileViews())
            {
                if (tileView == null || !TileContainsScreenPoint(tileView, screenPosition))
                {
                    continue;
                }

                if (CanCardUseOnTile(battleState, _draggedCardId, tileView))
                {
                    return tileView;
                }
            }

            return null;
        }

        private bool CanCardUseOnTile(BattleState battleState, string cardId, TileView tileView)
        {
            if (!TryGetPlayableCardDefinition(battleState, cardId, out var definition) || tileView == null)
            {
                return false;
            }

            switch (definition.CardType)
            {
                case CardType.Unit:
                case CardType.Building:
                    return tileView.OwnerId == PlayerId.Player &&
                           battleState.PlayerBoard.GetOccupant(tileView.Coord) == null;

                case CardType.Spell:
                    if (definition is DamageSpellCardDefinition)
                    {
                        return tileView.OwnerId == PlayerId.AI &&
                               battleState.AIBoard.GetOccupant(tileView.Coord) != null;
                    }

                    if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                        string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal))
                    {
                        var board = tileView.OwnerId == PlayerId.Player ? battleState.PlayerBoard : battleState.AIBoard;
                        return board.GetOccupant(tileView.Coord)?.Kind == OccupantKind.Unit;
                    }

                    return false;

                default:
                    return false;
            }
        }

        private bool CanCardCastWithoutTarget(BattleState battleState, string cardId)
        {
            if (!TryGetPlayableCardDefinition(battleState, cardId, out var definition) || definition.CardType != CardType.Spell)
            {
                return false;
            }

            if (definition is DamageSpellCardDefinition)
            {
                return false;
            }

            if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal))
            {
                return false;
            }

            return definition is PersistentResourceSpellCardDefinition ||
                   definition is ScriptedSpellCardDefinition;
        }

        private bool CanDragCardInCurrentState(BattleState battleState, string cardId)
        {
            return TryGetPlayableCardDefinition(battleState, cardId, out _);
        }

        private bool TryGetPlayableCardDefinition(BattleState battleState, string cardId, out CardDefinition definition)
        {
            definition = null;
            if (_battleBootstrapper == null ||
                battleState == null ||
                battleState.Phase != PhaseType.Main ||
                battleState.ActivePlayerId != PlayerId.Player ||
                string.IsNullOrWhiteSpace(cardId) ||
                !battleState.Player.Hand.Contains(cardId) ||
                !_battleBootstrapper.TryGetCardDefinition(cardId, out definition) ||
                definition == null)
            {
                return false;
            }

            return battleState.Player.Resources.CanAfford(definition.Cost);
        }

        private TileView[] EnumerateAllTileViews()
        {
            var playerTileViews = _boardPresenter == null ? Array.Empty<TileView>() : _boardPresenter.PlayerTileViews;
            var aiTileViews = _boardPresenter == null ? Array.Empty<TileView>() : _boardPresenter.AITileViews;
            var tileViews = new TileView[playerTileViews.Length + aiTileViews.Length];
            var index = 0;

            for (var i = 0; i < playerTileViews.Length; i++)
            {
                tileViews[index++] = playerTileViews[i];
            }

            for (var i = 0; i < aiTileViews.Length; i++)
            {
                tileViews[index++] = aiTileViews[i];
            }

            return tileViews;
        }

        private static bool TileContainsScreenPoint(TileView tileView, Vector2 screenPosition)
        {
            if (tileView == null)
            {
                return false;
            }

            var tileTextView = tileView.GetComponent<TileTextView>();
            return tileTextView != null && tileTextView.ContainsScreenPoint(screenPosition);
        }

        private static string BuildSelectionMessage(string cardId, int slotIndex)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return "Select a hand card.";
            }

            return $"Selected '{cardId}' from slot {slotIndex}. Drag it onto a tile to use it, or drag upward to cast self-use spells.";
        }

        private static string BuildDragMessage(string cardId)
        {
            return $"Dragging '{cardId}'. Release over a valid tile to use it.";
        }

        private static string BuildOccupantSelectionMessage(TileCoord coord, OccupantState occupant)
        {
            var canMove = CanOccupantMove(occupant);
            var canAttack = CanOccupantAttack(occupant);

            if (canMove && canAttack)
            {
                return $"Selected occupant at {coord}. Click an empty player tile to move, another movable ally to swap, or an enemy tile to attack.";
            }

            if (canMove)
            {
                return $"Selected occupant at {coord}. Click an empty player tile to move it, or another movable ally to swap.";
            }

            return $"Selected occupant at {coord}. Click an enemy tile to attack.";
        }
    }
}
