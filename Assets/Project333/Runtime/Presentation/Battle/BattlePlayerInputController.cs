using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattlePlayerInputController : MonoBehaviour
    {
        private const string RobotFusionSelectionCanvasName = "RobotFusionSelectionCanvas";
        private static readonly string[] KoreanFontCandidates =
        {
            "Noto Sans CJK KR",
            "Noto Sans KR",
            "Malgun Gothic",
            "Apple SD Gothic Neo",
        };
        private static Font s_robotFusionKoreanFont;

        [SerializeField] private BattleBootstrapper _battleBootstrapper;
        [SerializeField] private BoardPresenter _boardPresenter;
        [SerializeField] private HandPresenter _handPresenter;
        [SerializeField] private string _selectedCardId;
        [SerializeField] private string _selectedHandCardRuntimeId;
        [SerializeField] private int _selectedSlotIndex = -1;
        [SerializeField] private bool _hasSelectedUnit;
        [SerializeField] private int _selectedUnitColumn;
        [SerializeField] private int _selectedUnitRow;
        [SerializeField] private float _selfCastDragReleaseThreshold = 140f;
        [SerializeField, Min(0f)] private float _backgroundDeselectMaxPointerTravel = 18f;
        [TextArea(2, 6)]
        [SerializeField] private string _interactionStatus = "Select a hand card.";
        [SerializeField, HideInInspector] private CanvasGroup _robotFusionSelectionCanvasGroup;
        [SerializeField, HideInInspector] private Button _robotFusionConfirmButton;
        [SerializeField, HideInInspector] private Text _robotFusionConfirmButtonText;
        private readonly TargetingService _targetingService = new TargetingService();
        private readonly List<RaycastResult> _backgroundDeselectRaycastResults = new List<RaycastResult>();
        private readonly List<TileCoord> _selectedRobotFusionCoords = new List<TileCoord>();
        private bool _isDraggingCard;
        private string _draggedCardId = string.Empty;
        private string _draggedHandCardRuntimeId = string.Empty;
        private int _draggedSlotIndex = -1;
        private Vector2 _dragStartScreenPosition;
        private Vector2 _dragCurrentScreenPosition;
        private TileView _dragHoveredTileView;
        private bool _isTrackingBackgroundDeselectPointer;
        private int _backgroundDeselectPointerId;
        private Vector2 _backgroundDeselectPointerStartPosition;
        private bool _backgroundDeselectPointerMovedTooFar;
        private bool _backgroundDeselectPointerStartedOverPreservingTarget;
        private bool _isRobotFusionSelectionMode;
        private string _pendingRobotFusionCardId = string.Empty;

        public string SelectedCardId => _selectedCardId;

        public string SelectedHandCardRuntimeId => _selectedHandCardRuntimeId;

        public int SelectedSlotIndex => _selectedSlotIndex;

        public bool HasSelectedUnit => _hasSelectedUnit;

        public TileCoord SelectedUnitCoord => new TileCoord(_selectedUnitColumn, _selectedUnitRow);

        public string InteractionStatus => _interactionStatus;

        public bool IsRobotFusionSelectionMode => _isRobotFusionSelectionMode;

        public IReadOnlyList<TileCoord> SelectedRobotFusionCoords => _selectedRobotFusionCoords;

        private void Start()
        {
            EnsureRobotFusionSelectionUi();
            RebindViews();
        }

        private void Update()
        {
            SyncRobotFusionSelectionState();
            UpdateBackgroundDeselectInput();
        }

        private void LateUpdate()
        {
            RefreshHighlights();
        }

        private void OnDestroy()
        {
            if (_robotFusionConfirmButton != null)
            {
                _robotFusionConfirmButton.onClick.RemoveListener(ConfirmRobotFusionSelection);
            }

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

            var isMulliganPhase = battleState.Phase == PhaseType.Mulligan;
            if (isMulliganPhase)
            {
                ExitRobotFusionSelectionMode();
                ClearCardSelection();
                ClearUnitSelection();
                ClearHandCardDragState();
                return;
            }

            if (_isRobotFusionSelectionMode)
            {
                HighlightRobotFusionTargets(battleState);
                UpdateRobotFusionSelectionUi();
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

                if (battleState.Player.Resources.CanAfford(definition.Cost) &&
                    IsCardBoardConditionMet(battleState, definition))
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

            var battleState = _battleBootstrapper?.CurrentBattleState;
            if (battleState?.Phase == PhaseType.Mulligan)
            {
                _interactionStatus = "화면 중앙의 멀리건 카드에서 교체 대상을 선택하세요.";
                return;
            }

            if (_selectedSlotIndex == handCardView.SlotIndex &&
                _selectedCardId == handCardView.CardId &&
                _selectedHandCardRuntimeId == handCardView.RuntimeId)
            {
                ClearCardSelection();
                _interactionStatus = $"Cancelled hand card selection from slot {handCardView.SlotIndex}.";
                return;
            }

            SetCardSelection(handCardView.CardId, handCardView.RuntimeId, handCardView.SlotIndex);
            _interactionStatus = BuildSelectionMessage(_selectedCardId, _selectedSlotIndex);
        }

        private void UpdateBackgroundDeselectInput()
        {
            if (UnityEngine.Input.touchCount > 0)
            {
                UpdateTouchBackgroundDeselectInput();
                return;
            }

            const int mousePointerId = -1;
            var mousePosition = (Vector2)UnityEngine.Input.mousePosition;
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                BeginBackgroundDeselectPointer(mousePointerId, mousePosition);
            }

            if (UnityEngine.Input.GetMouseButton(0))
            {
                UpdateBackgroundDeselectPointer(mousePointerId, mousePosition);
            }

            if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                EndBackgroundDeselectPointer(mousePointerId, mousePosition);
            }
        }

        private void UpdateTouchBackgroundDeselectInput()
        {
            for (var touchIndex = 0; touchIndex < UnityEngine.Input.touchCount; touchIndex++)
            {
                var touch = UnityEngine.Input.GetTouch(touchIndex);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (!_isTrackingBackgroundDeselectPointer)
                        {
                            BeginBackgroundDeselectPointer(touch.fingerId, touch.position);
                        }

                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        UpdateBackgroundDeselectPointer(touch.fingerId, touch.position);
                        break;

                    case TouchPhase.Ended:
                        EndBackgroundDeselectPointer(touch.fingerId, touch.position);
                        break;

                    case TouchPhase.Canceled:
                        if (_isTrackingBackgroundDeselectPointer && _backgroundDeselectPointerId == touch.fingerId)
                        {
                            ResetBackgroundDeselectPointer();
                        }

                        break;
                }
            }
        }

        private void BeginBackgroundDeselectPointer(int pointerId, Vector2 screenPosition)
        {
            _isTrackingBackgroundDeselectPointer = true;
            _backgroundDeselectPointerId = pointerId;
            _backgroundDeselectPointerStartPosition = screenPosition;
            _backgroundDeselectPointerMovedTooFar = false;
            _backgroundDeselectPointerStartedOverPreservingTarget = IsSelectionPreservingScreenTarget(screenPosition);
        }

        private void UpdateBackgroundDeselectPointer(int pointerId, Vector2 screenPosition)
        {
            if (!_isTrackingBackgroundDeselectPointer || _backgroundDeselectPointerId != pointerId)
            {
                return;
            }

            var maxTravel = Mathf.Max(0f, _backgroundDeselectMaxPointerTravel);
            if ((screenPosition - _backgroundDeselectPointerStartPosition).sqrMagnitude > maxTravel * maxTravel)
            {
                _backgroundDeselectPointerMovedTooFar = true;
            }
        }

        private void EndBackgroundDeselectPointer(int pointerId, Vector2 screenPosition)
        {
            if (!_isTrackingBackgroundDeselectPointer || _backgroundDeselectPointerId != pointerId)
            {
                return;
            }

            UpdateBackgroundDeselectPointer(pointerId, screenPosition);
            var shouldTryDeselect = !_backgroundDeselectPointerMovedTooFar &&
                                    !_backgroundDeselectPointerStartedOverPreservingTarget &&
                                    !_isDraggingCard;
            ResetBackgroundDeselectPointer();

            if (shouldTryDeselect)
            {
                TryClearCardSelectionFromPointerTarget(ResolveTopScreenTarget(screenPosition));
            }
        }

        private void ResetBackgroundDeselectPointer()
        {
            _isTrackingBackgroundDeselectPointer = false;
            _backgroundDeselectPointerId = 0;
            _backgroundDeselectPointerStartPosition = Vector2.zero;
            _backgroundDeselectPointerMovedTooFar = false;
            _backgroundDeselectPointerStartedOverPreservingTarget = false;
        }

        private bool IsSelectionPreservingScreenTarget(Vector2 screenPosition)
        {
            return IsSelectionPreservingPointerTarget(ResolveTopScreenTarget(screenPosition));
        }

        private GameObject ResolveTopScreenTarget(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            var pointerEventData = new PointerEventData(eventSystem)
            {
                position = screenPosition,
            };

            _backgroundDeselectRaycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, _backgroundDeselectRaycastResults);
            var topTarget = _backgroundDeselectRaycastResults.Count > 0
                ? _backgroundDeselectRaycastResults[0].gameObject
                : null;
            _backgroundDeselectRaycastResults.Clear();
            return topTarget;
        }

        private void TryClearCardSelectionFromPointerTarget(GameObject pointerTarget)
        {
            if (string.IsNullOrWhiteSpace(_selectedCardId) || IsSelectionPreservingPointerTarget(pointerTarget))
            {
                return;
            }

            ClearCardSelection();
            _interactionStatus = "Hand card selection cleared.";
        }

        private static bool IsSelectionPreservingPointerTarget(GameObject pointerTarget)
        {
            return pointerTarget != null &&
                   (pointerTarget.GetComponentInParent<Button>() != null ||
                    pointerTarget.GetComponentInParent<HandCardView>() != null ||
                    pointerTarget.GetComponentInParent<TileView>() != null ||
                    pointerTarget.GetComponentInParent<TileTextView>() != null ||
                    pointerTarget.GetComponentInParent<TileLongPressRelay>() != null);
        }

        private void HandleHandCardDragStarted(HandCardView handCardView, Vector2 screenPosition)
        {
            if (handCardView == null || !handCardView.HasCard || string.IsNullOrWhiteSpace(handCardView.CardId))
            {
                return;
            }

            if (_battleBootstrapper?.CurrentBattleState?.Phase == PhaseType.Mulligan)
            {
                _interactionStatus = "멀리건에서는 카드를 클릭해 교체 대상을 선택하세요.";
                return;
            }

            if (_battleBootstrapper != null && _battleBootstrapper.IsBusy)
            {
                _interactionStatus = "Animations are still resolving.";
                return;
            }

            SetCardSelection(handCardView.CardId, handCardView.RuntimeId, handCardView.SlotIndex);
            _isDraggingCard = true;
            _draggedCardId = handCardView.CardId;
            _draggedHandCardRuntimeId = handCardView.RuntimeId;
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
                handCardView.RuntimeId != _draggedHandCardRuntimeId ||
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
                handCardView.RuntimeId != _draggedHandCardRuntimeId ||
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
                    var resolvedTargetCoord = ResolveCardTargetCoord(cardId, hoveredTileView);
                    _battleBootstrapper.TryUsePlayerCardOnTile(
                        cardId,
                        _draggedHandCardRuntimeId,
                        hoveredTileView.OwnerId,
                        resolvedTargetCoord,
                        out var message);
                    _interactionStatus = message;
                    ClearCardSelection();
                    return;
                }

                if (dragReleasedUpwardEnough && CanCardCastWithoutTarget(battleState, cardId))
                {
                    var beganRobotFusion = IsRobotFusionCard(battleState, cardId);
                    if (beganRobotFusion)
                    {
                        _battleBootstrapper.TryBeginPlayerRobotFusion(
                            cardId,
                            _draggedHandCardRuntimeId,
                            out var message);
                        _interactionStatus = message;
                    }
                    else
                    {
                        _battleBootstrapper.TryCastPlayerPersistentSpell(
                            cardId,
                            _draggedHandCardRuntimeId,
                            out var message);
                        _interactionStatus = message;
                    }

                    ClearCardSelection();
                    return;
                }

                ClearCardSelection();
                if (ShouldShowCardUseFailureToast(battleState, cardId))
                {
                    _interactionStatus = BuildCardDragFailureMessage(battleState, cardId, dragReleasedUpwardEnough);
                    _battleBootstrapper?.ShowCommandErrorToast(_interactionStatus);
                    return;
                }

                _interactionStatus = string.Empty;
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

            if (_battleBootstrapper.CurrentBattleState?.Phase == PhaseType.Mulligan)
            {
                _interactionStatus = "멀리건을 확정한 뒤 전장을 조작할 수 있습니다.";
                return;
            }

            if (_isRobotFusionSelectionMode)
            {
                HandleRobotFusionTileClicked(tileView);
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
                        var wasPlayedOnAlly = _battleBootstrapper.TryUsePlayerCardOnTile(
                            _selectedCardId,
                            _selectedHandCardRuntimeId,
                            tileView.OwnerId,
                            tileView.Coord,
                            out var alliedTargetMessage);
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

                var selectedCardId = _selectedCardId;
                var selectedBattleState = _battleBootstrapper.CurrentBattleState;
                if (!CanCardUseOnTile(selectedBattleState, selectedCardId, tileView))
                {
                    if (ShouldShowCardUseFailureToast(selectedBattleState, selectedCardId))
                    {
                        _interactionStatus = BuildCardDragFailureMessage(selectedBattleState, selectedCardId, dragReleasedUpwardEnough: false);
                        _battleBootstrapper.ShowCommandErrorToast(_interactionStatus);
                        return;
                    }

                    _interactionStatus = string.Empty;
                    return;
                }

                var wasPlayed = _battleBootstrapper.TryUsePlayerCardOnTile(
                    selectedCardId,
                    _selectedHandCardRuntimeId,
                    tileView.OwnerId,
                    ResolveCardTargetCoord(selectedCardId, tileView),
                    out var message);
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
                        HighlightOccupiedTiles(_boardPresenter.PlayerTileViews, battleState.PlayerBoard, allowHiding: true);
                        HighlightOccupiedTiles(_boardPresenter.AITileViews, battleState.AIBoard, allowHiding: false);
                    }
                    else if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                             string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal))
                    {
                        HighlightCheonraJimangTargets(battleState);
                    }
                    else if (definition is ScriptedSpellCardDefinition firewallDefinition &&
                             string.Equals(firewallDefinition.EffectId, "firewall", StringComparison.Ordinal))
                    {
                        HighlightAllTiles(_boardPresenter.PlayerTileViews, BattleHighlightState.SpellTarget);
                        HighlightAllTiles(_boardPresenter.AITileViews, BattleHighlightState.SpellTarget);
                    }
                    else if (IsBiochemicalBombDefinition(definition))
                    {
                        HighlightAllTiles(_boardPresenter.AITileViews, BattleHighlightState.SpellTarget);
                    }
                    else if (IsGuDefinition(definition))
                    {
                        HighlightGuTargets(battleState);
                    }
                    else if (IsHuanShuDefinition(definition))
                    {
                        HighlightHuanShuTargets(battleState);
                    }

                    break;
            }
        }

        private void HighlightCheonraJimangTargets(BattleState battleState)
        {
            HighlightUnitSpellTargets(_boardPresenter.PlayerTileViews, battleState.PlayerBoard, allowHiding: true);
            HighlightUnitSpellTargets(_boardPresenter.AITileViews, battleState.AIBoard, allowHiding: false);
        }

        private void HighlightGuTargets(BattleState battleState)
        {
            if (!GuRules.TryFindDestination(battleState, PlayerId.Player, out _))
            {
                return;
            }

            foreach (var tileView in _boardPresenter.AITileViews)
            {
                if (tileView != null &&
                    GuRules.IsLegalTarget(
                        battleState,
                        PlayerId.Player,
                        PlayerId.AI,
                        tileView.Coord))
                {
                    tileView.SetHighlight(BattleHighlightState.SpellTarget);
                }
            }
        }

        private void HighlightHuanShuTargets(BattleState battleState)
        {
            foreach (var tileView in _boardPresenter.AITileViews)
            {
                if (tileView != null &&
                    HuanShuRules.IsLegalTarget(
                        battleState,
                        PlayerId.Player,
                        PlayerId.AI,
                        tileView.Coord))
                {
                    tileView.SetHighlight(BattleHighlightState.SpellTarget);
                }
            }
        }

        private static void HighlightUnitSpellTargets(
            TileView[] tileViews,
            BoardState boardState,
            bool allowHiding)
        {
            foreach (var tileView in tileViews)
            {
                if (tileView == null)
                {
                    continue;
                }

                var occupant = boardState.GetOccupant(tileView.Coord);
                if (occupant?.Kind == OccupantKind.Unit &&
                    occupant.CanBeAffected &&
                    (allowHiding || !occupant.IsHiding))
                {
                    tileView.SetHighlight(BattleHighlightState.SpellTarget);
                }
            }
        }

        private static void HighlightOccupiedTiles(
            TileView[] tileViews,
            BoardState boardState,
            bool allowHiding)
        {
            foreach (var tileView in tileViews)
            {
                var occupant = tileView == null ? null : boardState.GetOccupant(tileView.Coord);
                if (occupant?.CanBeAffected == true && (allowHiding || !occupant.IsHiding))
                {
                    tileView.SetHighlight(BattleHighlightState.SpellTarget);
                }
            }
        }

        private static void HighlightAllTiles(TileView[] tileViews, BattleHighlightState highlightState)
        {
            foreach (var tileView in tileViews)
            {
                tileView?.SetHighlight(highlightState);
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

            if (definition is DamageSpellCardDefinition)
            {
                return occupant.CanBeAffected;
            }

            if (definition is not ScriptedSpellCardDefinition scriptedSpellDefinition)
            {
                return false;
            }

            if (string.Equals(scriptedSpellDefinition.EffectId, "firewall", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(scriptedSpellDefinition.EffectId, GuRules.EffectId, StringComparison.Ordinal))
            {
                return occupant.OwnerId == PlayerId.AI &&
                       GuRules.TryFindDestination(battleState, PlayerId.Player, out _) &&
                       GuRules.IsLegalTarget(
                           battleState,
                           PlayerId.Player,
                           PlayerId.AI,
                           occupant.Position);
            }

            if (string.Equals(scriptedSpellDefinition.EffectId, HuanShuRules.EffectId, StringComparison.Ordinal))
            {
                return occupant.OwnerId == PlayerId.AI &&
                       HuanShuRules.IsLegalTarget(
                           battleState,
                           PlayerId.Player,
                           PlayerId.AI,
                           occupant.Position);
            }

            return string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal) &&
                   occupant.Kind == OccupantKind.Unit &&
                   occupant.CanBeAffected;
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
                    handCardView.CardId == _selectedCardId &&
                    handCardView.RuntimeId == _selectedHandCardRuntimeId)
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

            if (_battleBootstrapper != null &&
                _battleBootstrapper.TryGetCardDefinition(_draggedCardId, out var definition) &&
                IsBiochemicalBombDefinition(definition))
            {
                var startColumn = BiochemicalBombRules.ResolveStartColumnFromHoveredColumn(
                    _dragHoveredTileView.Coord.Column);
                foreach (var tileView in _boardPresenter.AITileViews)
                {
                    if (tileView != null &&
                        BiochemicalBombRules.ContainsColumn(startColumn, tileView.Coord.Column))
                    {
                        tileView.SetHighlight(BattleHighlightState.DragHoverTarget);
                    }
                }

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

        private void SetCardSelection(string cardId, string handCardRuntimeId, int slotIndex)
        {
            ClearUnitSelection();
            _selectedCardId = cardId ?? string.Empty;
            _selectedHandCardRuntimeId = handCardRuntimeId ?? string.Empty;
            _selectedSlotIndex = slotIndex;
        }

        private void ClearCardSelection()
        {
            _selectedCardId = string.Empty;
            _selectedHandCardRuntimeId = string.Empty;
            _selectedSlotIndex = -1;
        }

        private void ClearHandCardDragState()
        {
            _isDraggingCard = false;
            _draggedCardId = string.Empty;
            _draggedHandCardRuntimeId = string.Empty;
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

        private void SyncRobotFusionSelectionState()
        {
            var battleState = _battleBootstrapper?.CurrentBattleState;
            var pending = battleState?.PendingRobotFusion;
            var hasLocalPendingFusion = pending != null && pending.OwnerId == PlayerId.Player;
            if (!hasLocalPendingFusion)
            {
                if (_isRobotFusionSelectionMode)
                {
                    ExitRobotFusionSelectionMode();
                }

                return;
            }

            if (!_isRobotFusionSelectionMode ||
                !string.Equals(_pendingRobotFusionCardId, pending.CardId, StringComparison.Ordinal))
            {
                EnterRobotFusionSelectionMode(pending.CardId);
            }

            for (var index = _selectedRobotFusionCoords.Count - 1; index >= 0; index -= 1)
            {
                if (!IsLivingFriendlyRobot(battleState, _selectedRobotFusionCoords[index]))
                {
                    _selectedRobotFusionCoords.RemoveAt(index);
                }
            }

            UpdateRobotFusionSelectionUi();
        }

        private void EnterRobotFusionSelectionMode(string cardId)
        {
            ClearCardSelection();
            ClearUnitSelection();
            ClearHandCardDragState();
            _selectedRobotFusionCoords.Clear();
            _pendingRobotFusionCardId = cardId ?? RobotFusionRules.CardId;
            _isRobotFusionSelectionMode = true;
            _interactionStatus = "합체할 살아 있는 아군 로봇 유닛을 순서대로 선택하세요.";
            UpdateRobotFusionSelectionUi();
        }

        private void ExitRobotFusionSelectionMode()
        {
            _isRobotFusionSelectionMode = false;
            _pendingRobotFusionCardId = string.Empty;
            _selectedRobotFusionCoords.Clear();
            SetRobotFusionSelectionUiVisible(false);
        }

        private void HandleRobotFusionTileClicked(TileView tileView)
        {
            var battleState = _battleBootstrapper?.CurrentBattleState;
            if (tileView == null || tileView.OwnerId != PlayerId.Player ||
                !IsLivingFriendlyRobot(battleState, tileView.Coord))
            {
                _interactionStatus = "내 필드의 살아 있는 로봇 유닛만 선택할 수 있습니다.";
                return;
            }

            var selectedIndex = _selectedRobotFusionCoords.IndexOf(tileView.Coord);
            if (selectedIndex >= 0)
            {
                _selectedRobotFusionCoords.RemoveAt(selectedIndex);
            }
            else
            {
                _selectedRobotFusionCoords.Add(tileView.Coord);
            }

            _interactionStatus = $"로봇 {_selectedRobotFusionCoords.Count}기 선택됨.";
            UpdateRobotFusionSelectionUi();
        }

        private void ConfirmRobotFusionSelection()
        {
            if (!_isRobotFusionSelectionMode ||
                _selectedRobotFusionCoords.Count < RobotFusionRules.MinimumRobotCount ||
                _battleBootstrapper == null)
            {
                return;
            }

            var orderedTargets = new List<TileCoord>(_selectedRobotFusionCoords);
            _battleBootstrapper.TryResolvePlayerRobotFusion(
                _pendingRobotFusionCardId,
                orderedTargets,
                out var message);
            _interactionStatus = message;
        }

        private void HighlightRobotFusionTargets(BattleState battleState)
        {
            if (_boardPresenter == null || battleState == null)
            {
                return;
            }

            foreach (var tileView in _boardPresenter.PlayerTileViews)
            {
                if (tileView == null || !IsLivingFriendlyRobot(battleState, tileView.Coord))
                {
                    continue;
                }

                tileView.SetHighlight(_selectedRobotFusionCoords.Contains(tileView.Coord)
                    ? BattleHighlightState.Selected
                    : BattleHighlightState.SpellTarget);
            }
        }

        private static bool IsLivingFriendlyRobot(BattleState battleState, TileCoord coord)
        {
            return battleState?.PlayerBoard?.GetOccupant(coord) is UnitState
            {
                HasRobot: true,
                IsAlive: true,
            };
        }

        private static bool CanOccupantAct(OccupantState occupant)
        {
            return CanOccupantMove(occupant) || CanOccupantAttack(occupant);
        }

        private static bool CanOccupantMove(OccupantState occupant)
        {
            return occupant != null && !occupant.CannotMoveDueToState && occupant.CanMove;
        }

        private static bool CanOccupantAttack(OccupantState occupant)
        {
            if (occupant == null || occupant.CannotAttack || occupant.HasSummoningSickness || occupant.RemainingAttacksThisTurn <= 0 || occupant.Attack <= 0)
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
                        var board = tileView.OwnerId == PlayerId.Player
                            ? battleState.PlayerBoard
                            : battleState.AIBoard;
                        var target = board.GetOccupant(tileView.Coord);
                        return target?.CanBeAffected == true &&
                               (tileView.OwnerId == PlayerId.Player || !target.IsHiding);
                    }

                    if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                        string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal))
                    {
                        var board = tileView.OwnerId == PlayerId.Player ? battleState.PlayerBoard : battleState.AIBoard;
                        var target = board.GetOccupant(tileView.Coord);
                        return target?.Kind == OccupantKind.Unit &&
                               target.CanBeAffected &&
                               (tileView.OwnerId == PlayerId.Player || !target.IsHiding);
                    }

                    if (definition is ScriptedSpellCardDefinition firewallDefinition &&
                        string.Equals(firewallDefinition.EffectId, "firewall", StringComparison.Ordinal))
                    {
                        return tileView.OwnerId == PlayerId.Player || tileView.OwnerId == PlayerId.AI;
                    }

                    if (IsBiochemicalBombDefinition(definition))
                    {
                        return tileView.OwnerId == PlayerId.AI;
                    }

                    if (IsGuDefinition(definition))
                    {
                        return GuRules.IsLegalTarget(
                            battleState,
                            PlayerId.Player,
                            tileView.OwnerId,
                            tileView.Coord);
                    }

                    if (IsHuanShuDefinition(definition))
                    {
                        return HuanShuRules.IsLegalTarget(
                            battleState,
                            PlayerId.Player,
                            tileView.OwnerId,
                            tileView.Coord);
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
                (string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal) ||
                 string.Equals(scriptedSpellDefinition.EffectId, "firewall", StringComparison.Ordinal) ||
                 string.Equals(scriptedSpellDefinition.EffectId, BiochemicalBombRules.EffectId, StringComparison.Ordinal) ||
                 string.Equals(scriptedSpellDefinition.EffectId, GuRules.EffectId, StringComparison.Ordinal) ||
                 string.Equals(scriptedSpellDefinition.EffectId, HuanShuRules.EffectId, StringComparison.Ordinal)))
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

        private string BuildCardDragFailureMessage(BattleState battleState, string cardId, bool dragReleasedUpwardEnough)
        {
            if (_battleBootstrapper == null)
            {
                return "Battle bootstrapper is not assigned.";
            }

            if (battleState == null)
            {
                return "Battle is not ready.";
            }

            if (battleState.Phase != PhaseType.Main || battleState.ActivePlayerId != PlayerId.Player)
            {
                return "You can only use cards during your main phase.";
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return "No card is selected.";
            }

            if (!battleState.Player.Hand.Contains(cardId))
            {
                return $"Card '{cardId}' is not in your hand.";
            }

            if (!_battleBootstrapper.TryGetCardDefinition(cardId, out var definition) || definition == null)
            {
                return $"Card '{cardId}' definition was not found.";
            }

            if (!battleState.Player.Resources.CanAfford(definition.Cost))
            {
                return $"Not enough resources to use '{cardId}'.";
            }

            if (!IsCardBoardConditionMet(battleState, definition))
            {
                if (IsRobotFusionDefinition(definition))
                {
                    return "살아 있는 아군 로봇 유닛이 2기 이상 필요합니다.";
                }

                if (IsGuDefinition(definition))
                {
                    return GuRules.TryFindDestination(battleState, PlayerId.Player, out _)
                        ? "복종시킬 수 있는 상대방 유닛이 없습니다."
                        : "고독을 사용하려면 내 필드에 빈 타일이 필요합니다.";
                }

                if (IsHuanShuDefinition(definition))
                {
                    return "환술을 부여할 수 있는 상대방 유닛이 없습니다.";
                }

                return $"Card '{cardId}' cannot be used in the current board state.";
            }

            if (dragReleasedUpwardEnough && !CanCardCastWithoutTarget(battleState, cardId))
            {
                return $"Card '{cardId}' needs a valid target tile.";
            }

            return $"Release '{cardId}' over a valid target tile.";
        }

        private bool ShouldShowCardUseFailureToast(BattleState battleState, string cardId)
        {
            if (_battleBootstrapper == null ||
                battleState == null ||
                battleState.Phase != PhaseType.Main ||
                battleState.ActivePlayerId != PlayerId.Player ||
                string.IsNullOrWhiteSpace(cardId) ||
                !battleState.Player.Hand.Contains(cardId))
            {
                return true;
            }

            if (!_battleBootstrapper.TryGetCardDefinition(cardId, out var definition) || definition == null)
            {
                return true;
            }

            return !battleState.Player.Resources.CanAfford(definition.Cost) ||
                   !IsCardBoardConditionMet(battleState, definition);
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

            return battleState.Player.Resources.CanAfford(definition.Cost) &&
                   IsCardBoardConditionMet(battleState, definition);
        }

        private static bool IsCardBoardConditionMet(BattleState battleState, CardDefinition definition)
        {
            if (IsRobotFusionDefinition(definition))
            {
                return RobotFusionRules.CountLivingRobots(battleState?.PlayerBoard) >=
                       RobotFusionRules.MinimumRobotCount;
            }

            if (IsGuDefinition(definition))
            {
                return GuRules.CanCast(battleState, PlayerId.Player);
            }

            return !IsHuanShuDefinition(definition) ||
                   HuanShuRules.CanCast(battleState, PlayerId.Player);
        }

        private bool IsRobotFusionCard(BattleState battleState, string cardId)
        {
            return battleState != null &&
                   _battleBootstrapper != null &&
                   _battleBootstrapper.TryGetCardDefinition(cardId, out var definition) &&
                   IsRobotFusionDefinition(definition);
        }

        private static bool IsRobotFusionDefinition(CardDefinition definition)
        {
            return definition is ScriptedSpellCardDefinition scriptedSpell &&
                   string.Equals(scriptedSpell.EffectId, RobotFusionRules.EffectId, StringComparison.Ordinal);
        }

        private static bool IsGuDefinition(CardDefinition definition)
        {
            return definition is ScriptedSpellCardDefinition scriptedSpell &&
                   string.Equals(scriptedSpell.EffectId, GuRules.EffectId, StringComparison.Ordinal);
        }

        private static bool IsHuanShuDefinition(CardDefinition definition)
        {
            return definition is ScriptedSpellCardDefinition scriptedSpell &&
                   string.Equals(scriptedSpell.EffectId, HuanShuRules.EffectId, StringComparison.Ordinal);
        }

        private static bool IsBiochemicalBombDefinition(CardDefinition definition)
        {
            return definition is ScriptedSpellCardDefinition scriptedSpell &&
                   string.Equals(
                       scriptedSpell.EffectId,
                       BiochemicalBombRules.EffectId,
                       StringComparison.Ordinal);
        }

        private TileCoord ResolveCardTargetCoord(string cardId, TileView tileView)
        {
            if (tileView == null ||
                _battleBootstrapper == null ||
                !_battleBootstrapper.TryGetCardDefinition(cardId, out var definition) ||
                !IsBiochemicalBombDefinition(definition))
            {
                return tileView?.Coord ?? default;
            }

            var startColumn = BiochemicalBombRules.ResolveStartColumnFromHoveredColumn(
                tileView.Coord.Column);
            return new TileCoord(startColumn, 0);
        }

        private void UpdateRobotFusionSelectionUi()
        {
            var shouldShow = _isRobotFusionSelectionMode &&
                             _selectedRobotFusionCoords.Count >= RobotFusionRules.MinimumRobotCount;
            SetRobotFusionSelectionUiVisible(shouldShow);
            if (_robotFusionConfirmButton != null)
            {
                _robotFusionConfirmButton.interactable = shouldShow &&
                                                         (_battleBootstrapper == null || !_battleBootstrapper.IsBusy);
            }
        }

        private void SetRobotFusionSelectionUiVisible(bool visible)
        {
            if (visible)
            {
                EnsureRobotFusionSelectionUi();
            }

            if (_robotFusionSelectionCanvasGroup == null)
            {
                return;
            }

            _robotFusionSelectionCanvasGroup.gameObject.SetActive(visible);
            _robotFusionSelectionCanvasGroup.alpha = visible ? 1f : 0f;
            _robotFusionSelectionCanvasGroup.interactable = visible;
            _robotFusionSelectionCanvasGroup.blocksRaycasts = visible;
        }

        private void EnsureRobotFusionSelectionUi(bool allowCreationInEditMode = false)
        {
            if (_robotFusionSelectionCanvasGroup == null)
            {
                foreach (var candidate in Resources.FindObjectsOfTypeAll<CanvasGroup>())
                {
                    if (candidate != null &&
                        candidate.gameObject.scene == gameObject.scene &&
                        string.Equals(candidate.gameObject.name, RobotFusionSelectionCanvasName, StringComparison.Ordinal))
                    {
                        _robotFusionSelectionCanvasGroup = candidate;
                        break;
                    }
                }
            }

            if (_robotFusionSelectionCanvasGroup != null)
            {
                _robotFusionConfirmButton ??=
                    _robotFusionSelectionCanvasGroup.GetComponentInChildren<Button>(includeInactive: true);
                _robotFusionConfirmButtonText ??=
                    _robotFusionConfirmButton?.GetComponentInChildren<Text>(includeInactive: true);
                BindRobotFusionConfirmButton();
                return;
            }

            if (!UnityEngine.Application.isPlaying && !allowCreationInEditMode)
            {
                return;
            }

            var canvasObject = new GameObject(
                RobotFusionSelectionCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            if (gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
            }

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Robot Fusion Selection UI");
            }
#endif

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 36000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _robotFusionSelectionCanvasGroup = canvasObject.GetComponent<CanvasGroup>();

            var buttonObject = new GameObject(
                "ConfirmFusionButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, 260f);
            buttonRect.sizeDelta = new Vector2(280f, 96f);

            var buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.58f, 0.10f, 0.08f, 0.96f);
            _robotFusionConfirmButton = buttonObject.GetComponent<Button>();
            var colors = _robotFusionConfirmButton.colors;
            colors.highlightedColor = new Color(0.78f, 0.18f, 0.12f, 1f);
            colors.pressedColor = new Color(0.38f, 0.05f, 0.04f, 1f);
            _robotFusionConfirmButton.colors = colors;

            var textObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);
            _robotFusionConfirmButtonText = textObject.GetComponent<Text>();
            _robotFusionConfirmButtonText.text = "합체";
            _robotFusionConfirmButtonText.alignment = TextAnchor.MiddleCenter;
            _robotFusionConfirmButtonText.font = ResolveRobotFusionKoreanFont();
            _robotFusionConfirmButtonText.fontSize = 42;
            _robotFusionConfirmButtonText.fontStyle = FontStyle.Bold;
            _robotFusionConfirmButtonText.color = Color.white;
            _robotFusionConfirmButtonText.raycastTarget = false;

            BindRobotFusionConfirmButton();
            SetRobotFusionSelectionUiVisible(false);
        }

        private void BindRobotFusionConfirmButton()
        {
            if (_robotFusionConfirmButton == null)
            {
                return;
            }

            _robotFusionConfirmButton.onClick.RemoveListener(ConfirmRobotFusionSelection);
            _robotFusionConfirmButton.onClick.AddListener(ConfirmRobotFusionSelection);
            if (_robotFusionConfirmButtonText != null)
            {
                _robotFusionConfirmButtonText.text = "합체";
            }
        }

        private static Font ResolveRobotFusionKoreanFont()
        {
#if UNITY_EDITOR
            var editorFont = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static/NotoSansKR-Bold.ttf");
            if (editorFont != null)
            {
                return editorFont;
            }
#endif
            if (s_robotFusionKoreanFont != null)
            {
                return s_robotFusionKoreanFont;
            }

            try
            {
                s_robotFusionKoreanFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 42);
            }
            catch
            {
                s_robotFusionKoreanFont = null;
            }

            return s_robotFusionKoreanFont != null
                ? s_robotFusionKoreanFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

#if UNITY_EDITOR
        [ContextMenu("Materialize Robot Fusion Selection UI")]
        public void MaterializeRobotFusionSelectionUiForEditor()
        {
            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            EnsureRobotFusionSelectionUi(allowCreationInEditMode: true);
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif

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
