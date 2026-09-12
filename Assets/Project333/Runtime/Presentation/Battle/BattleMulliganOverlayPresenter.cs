using System;
using Project333.Runtime.Presentation.Hand;
using System.Collections;
using System.Collections.Generic;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BattleMulliganOverlayPresenter : MonoBehaviour
    {
        private const int DefaultCanvasSortingOrder = 6000;

        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Noto Sans CJK KR",
            "Noto Sans KR",
            "Arial Unicode MS"
        };
        private static Font s_koreanFont;

        [Header("Scene References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Text _confirmButtonText;
        [SerializeField] private Button[] _cardButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _cardArtworkImages = Array.Empty<Image>();
        [SerializeField] private Image[] _selectionOverlays = Array.Empty<Image>();
        [SerializeField] private Text[] _fallbackCardIdTexts = Array.Empty<Text>();
        [SerializeField] private Text[] _attackValueTexts = Array.Empty<Text>();
        [SerializeField] private Text[] _hpValueTexts = Array.Empty<Text>();

        [Header("Presentation")]
        [SerializeField] private int _canvasSortingOrder = DefaultCanvasSortingOrder;
        [SerializeField] [Min(0f)] private float _resultDisplaySeconds = 3f;
        [SerializeField] private bool _showCardStats = true;
        [SerializeField] private Vector2 _attackStatNormalizedPosition = HandCardStatOverlayLayout.DefaultAttackPosition;
        [SerializeField] private Vector2 _hpStatNormalizedPosition = HandCardStatOverlayLayout.DefaultHpPosition;
        [SerializeField] private Vector2 _statTextSize = new Vector2(72f, 54f);
        [SerializeField] private int _statFontSize = 34;
        [SerializeField] private Color _statTextColor = Color.white;
        [SerializeField] private Color _statOutlineColor = new Color(0f, 0f, 0f, 0.95f);
        [SerializeField] private Vector2 _statOutlineDistance = new Vector2(2f, -2f);

        private readonly List<string> _openingHandCardIds = new List<string>(4);
        private readonly List<string> _displayCardIds = new List<string>(4);
        private readonly HashSet<int> _selectedSlotIndices = new HashSet<int>();

        private BattleBootstrapper _battleBootstrapper;
        private UnityAction[] _cardButtonActions = Array.Empty<UnityAction>();
        private Coroutine _resultSequence;
        private bool _buttonsBound;
        private bool _sessionActive;
        private bool _confirmationSent;
        private bool _hasReceivedConfirmedHand;
        private bool _isShowingFinalResult;
        private bool _isLocalFirstPlayer;
#if UNITY_EDITOR
        private bool _editorStatTextEnsureQueued;
#endif

        public int SelectedCardCount => _selectedSlotIndices.Count;

        public bool IsSessionActive => _sessionActive;

        public void Bind(BattleBootstrapper battleBootstrapper)
        {
            _battleBootstrapper = battleBootstrapper;
            EnsureCardStatTexts();
        }

        private void Awake()
        {
            ApplyCanvasSortingOrder();
            EnsureCardStatTexts();
            ApplyKoreanFonts();
            BindButtons();
        }

        private void OnEnable()
        {
            ApplyCanvasSortingOrder();
            EnsureCardStatTexts();
            ApplyKoreanFonts();
            BindButtons();
        }

        private void LateUpdate()
        {
            if (_sessionActive)
            {
                RefreshCardStatLayouts();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyCanvasSortingOrder();

            if (UnityEngine.Application.isPlaying || _editorStatTextEnsureQueued)
            {
                return;
            }

            _editorStatTextEnsureQueued = true;
            UnityEditor.EditorApplication.delayCall += EnsureEditorCardStatTexts;
        }

        private void EnsureEditorCardStatTexts()
        {
            _editorStatTextEnsureQueued = false;
            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            EnsureCardStatTexts();
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif

        private void ApplyCanvasSortingOrder()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            if (_canvas == null)
            {
                return;
            }

            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _canvasSortingOrder;
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        public void Present(BattleState battleState)
        {
            if (battleState == null)
            {
                EndSessionAndHide();
                return;
            }

            if (battleState.Phase == PhaseType.Mulligan)
            {
                PresentSelectionOrWaitingState(battleState);
                return;
            }

            if (_sessionActive)
            {
                PresentFinalResult(battleState);
                return;
            }

            SetVisible(false);
        }

        public void ToggleCardFromUi(int slotIndex)
        {
            if (!_sessionActive ||
                _confirmationSent ||
                _hasReceivedConfirmedHand ||
                slotIndex < 0 ||
                slotIndex >= _openingHandCardIds.Count)
            {
                return;
            }

            if (!_selectedSlotIndices.Add(slotIndex))
            {
                _selectedSlotIndices.Remove(slotIndex);
            }

            RefreshSelectionVisuals();
            UpdateSelectionStatus();
        }

        public void ConfirmSelectionFromUi()
        {
            if (!_sessionActive || _confirmationSent || _hasReceivedConfirmedHand)
            {
                return;
            }

            if (_battleBootstrapper == null)
            {
                SetStatus("전투 연결을 확인할 수 없습니다.");
                return;
            }

            var selectedCardIds = new List<string>(_selectedSlotIndices.Count);
            for (var slotIndex = 0; slotIndex < _openingHandCardIds.Count; slotIndex++)
            {
                if (_selectedSlotIndices.Contains(slotIndex))
                {
                    selectedCardIds.Add(_openingHandCardIds[slotIndex]);
                }
            }

            _confirmationSent = true;
            SetSelectionInteractable(false);
            SetStatus("멀리건을 처리하는 중...");

            if (_battleBootstrapper.SubmitPlayerMulligan(selectedCardIds))
            {
                return;
            }

            _confirmationSent = false;
            SetSelectionInteractable(true);
            UpdateSelectionStatus();
        }

        public static IReadOnlyList<string> BuildConfirmedCardLayout(
            IReadOnlyList<string> openingHandCardIds,
            IReadOnlyCollection<int> selectedSlotIndices,
            IReadOnlyList<string> currentHandCardIds)
        {
            var openingCards = openingHandCardIds ?? Array.Empty<string>();
            var selectedSlots = selectedSlotIndices == null
                ? new HashSet<int>()
                : new HashSet<int>(selectedSlotIndices);
            var replacementCandidates = currentHandCardIds == null
                ? new List<string>()
                : new List<string>(currentHandCardIds);

            for (var slotIndex = 0; slotIndex < openingCards.Count; slotIndex++)
            {
                if (!selectedSlots.Contains(slotIndex))
                {
                    RemoveFirst(replacementCandidates, openingCards[slotIndex]);
                }
            }

            var result = new List<string>(openingCards.Count);
            var replacementIndex = 0;
            for (var slotIndex = 0; slotIndex < openingCards.Count; slotIndex++)
            {
                if (!selectedSlots.Contains(slotIndex))
                {
                    result.Add(openingCards[slotIndex]);
                    continue;
                }

                result.Add(replacementIndex < replacementCandidates.Count
                    ? replacementCandidates[replacementIndex++]
                    : openingCards[slotIndex]);
            }

            return result;
        }

        private void PresentSelectionOrWaitingState(BattleState battleState)
        {
            if (!_sessionActive)
            {
                BeginSession(battleState);
            }

            SetVisible(true);
            if (!battleState.Player.HasUsedMulligan)
            {
                if (!_confirmationSent)
                {
                    _displayCardIds.Clear();
                    _displayCardIds.AddRange(_openingHandCardIds);
                    SetTitle("시작 카드를 선택하세요");
                    SetSelectionInteractable(true);
                    UpdateSelectionStatus();
                    RefreshCardVisuals();
                }

                return;
            }

            if (!_hasReceivedConfirmedHand)
            {
                CaptureConfirmedHand(battleState.Player.Hand.CardIds);
            }

            SetTitle("멀리건 완료");
            SetStatus("상대의 멀리건을 기다리는 중...");
            SetSelectionInteractable(false);
            RefreshCardVisuals();
        }

        private void PresentFinalResult(BattleState battleState)
        {
            if (!_hasReceivedConfirmedHand)
            {
                if (_confirmationSent)
                {
                    CaptureConfirmedHand(battleState.Player.Hand.CardIds);
                }
                else
                {
                    // A server timeout keeps the opening hand even if the player had highlighted cards locally.
                    _displayCardIds.Clear();
                    _displayCardIds.AddRange(_openingHandCardIds);
                    _hasReceivedConfirmedHand = true;
                }
            }

            SetVisible(true);
            SetTitle("멀리건 결과");
            SetStatus("잠시 후 전투가 시작됩니다.");
            SetSelectionInteractable(false);
            if (_confirmButton != null)
            {
                _confirmButton.gameObject.SetActive(false);
            }

            RefreshCardVisuals();
            if (_isShowingFinalResult)
            {
                return;
            }

            _isShowingFinalResult = true;
            if (_resultSequence != null)
            {
                StopCoroutine(_resultSequence);
            }

            _resultSequence = StartCoroutine(HideAfterResultDelay());
        }

        public static string BuildTurnOrderTitle(bool isLocalFirstPlayer, string title)
        {
            return $"{(isLocalFirstPlayer ? "선공" : "후공")}\n{title ?? string.Empty}";
        }

        private void BeginSession(BattleState battleState)
        {
            if (_resultSequence != null)
            {
                StopCoroutine(_resultSequence);
                _resultSequence = null;
            }

            _openingHandCardIds.Clear();
            var openingHandCardIds = battleState?.Player?.Hand?.CardIds;
            if (openingHandCardIds != null)
            {
                for (var i = 0; i < openingHandCardIds.Count && i < _cardButtons.Length; i++)
                {
                    _openingHandCardIds.Add(openingHandCardIds[i]);
                }
            }

            _displayCardIds.Clear();
            _displayCardIds.AddRange(_openingHandCardIds);
            _selectedSlotIndices.Clear();
            _confirmationSent = false;
            _hasReceivedConfirmedHand = false;
            _isShowingFinalResult = false;
            _isLocalFirstPlayer = battleState != null &&
                                  battleState.ActivePlayerId == PlayerId.Player;
            _sessionActive = true;

            SetVisible(true);
            SetTitle("시작 카드를 선택하세요");
            SetSelectionInteractable(true);
            UpdateSelectionStatus();
            RefreshCardVisuals();
        }

        private void CaptureConfirmedHand(IReadOnlyList<string> currentHandCardIds)
        {
            _displayCardIds.Clear();
            if (_confirmationSent)
            {
                _displayCardIds.AddRange(BuildConfirmedCardLayout(
                    _openingHandCardIds,
                    _selectedSlotIndices,
                    currentHandCardIds));
            }
            else
            {
                for (var i = 0; i < _openingHandCardIds.Count; i++)
                {
                    var cardId = currentHandCardIds != null && i < currentHandCardIds.Count
                        ? currentHandCardIds[i]
                        : _openingHandCardIds[i];
                    _displayCardIds.Add(cardId);
                }
            }

            _hasReceivedConfirmedHand = true;
            _selectedSlotIndices.Clear();
            RefreshSelectionVisuals();
        }

        private IEnumerator HideAfterResultDelay()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, _resultDisplaySeconds));
            _resultSequence = null;
            EndSessionAndHide();
        }

        private void EndSessionAndHide()
        {
            if (_resultSequence != null)
            {
                StopCoroutine(_resultSequence);
                _resultSequence = null;
            }

            _openingHandCardIds.Clear();
            _displayCardIds.Clear();
            _selectedSlotIndices.Clear();
            _confirmationSent = false;
            _hasReceivedConfirmedHand = false;
            _isShowingFinalResult = false;
            _isLocalFirstPlayer = false;
            _sessionActive = false;
            SetVisible(false);
        }

        private void RefreshCardVisuals()
        {
            EnsureCardStatTexts();
            for (var slotIndex = 0; slotIndex < _cardButtons.Length; slotIndex++)
            {
                var hasCard = slotIndex < _displayCardIds.Count &&
                              !string.IsNullOrWhiteSpace(_displayCardIds[slotIndex]);
                var button = _cardButtons[slotIndex];
                if (button != null)
                {
                    button.gameObject.SetActive(hasCard);
                }

                if (!hasCard)
                {
                    SetCardStatVisible(slotIndex, false);
                    continue;
                }

                var cardId = _displayCardIds[slotIndex];
                var artworkImage = GetAt(_cardArtworkImages, slotIndex);
                var fallbackText = GetAt(_fallbackCardIdTexts, slotIndex);
                var hasArtwork = CardArtworkLibrary.TryGetArtwork(cardId, out var artwork);
                if (artworkImage != null)
                {
                    artworkImage.sprite = artwork;
                    artworkImage.color = hasArtwork ? Color.white : new Color(0.12f, 0.14f, 0.19f, 1f);
                    artworkImage.preserveAspect = true;
                }

                if (fallbackText != null)
                {
                    fallbackText.gameObject.SetActive(!hasArtwork);
                    fallbackText.text = hasArtwork ? string.Empty : cardId;
                }

                RefreshCardStats(slotIndex, cardId);
            }

            RefreshSelectionVisuals();
            Canvas.ForceUpdateCanvases();
            RefreshCardStatLayouts();
        }

        private void RefreshSelectionVisuals()
        {
            for (var slotIndex = 0; slotIndex < _cardButtons.Length; slotIndex++)
            {
                var isSelected = !_hasReceivedConfirmedHand && _selectedSlotIndices.Contains(slotIndex);
                var selectionOverlay = GetAt(_selectionOverlays, slotIndex);
                if (selectionOverlay != null)
                {
                    selectionOverlay.gameObject.SetActive(isSelected);
                }

            }
        }

        private void SetSelectionInteractable(bool interactable)
        {
            for (var i = 0; i < _cardButtons.Length; i++)
            {
                if (_cardButtons[i] != null)
                {
                    _cardButtons[i].interactable = interactable;
                }
            }

            if (_confirmButton != null)
            {
                _confirmButton.gameObject.SetActive(true);
                _confirmButton.interactable = interactable;
            }

            if (_confirmButtonText != null)
            {
                _confirmButtonText.text = interactable ? "교체" : "확정 완료";
            }
        }

        private void UpdateSelectionStatus()
        {
            SetStatus(_selectedSlotIndices.Count == 0
                ? "교체하지 않으려면 바로 확정하세요."
                : $"선택한 카드 {_selectedSlotIndices.Count}장을 교체합니다.");
        }

        private void SetVisible(bool visible)
        {
            if (!gameObject.activeSelf && visible)
            {
                gameObject.SetActive(true);
            }

            if (_canvas != null)
            {
                _canvas.enabled = visible;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }

            if (gameObject.activeSelf && !visible)
            {
                gameObject.SetActive(false);
            }
        }

        private void SetTitle(string value)
        {
            if (_titleText != null)
            {
                _titleText.text = BuildTurnOrderTitle(_isLocalFirstPlayer, value);
            }
        }

        private void SetStatus(string value)
        {
            if (_statusText != null)
            {
                _statusText.text = value;
            }
        }

        private void BindButtons()
        {
            if (_buttonsBound)
            {
                return;
            }

            _cardButtonActions = new UnityAction[_cardButtons.Length];
            for (var slotIndex = 0; slotIndex < _cardButtons.Length; slotIndex++)
            {
                var capturedSlotIndex = slotIndex;
                _cardButtonActions[slotIndex] = () => ToggleCardFromUi(capturedSlotIndex);
                if (_cardButtons[slotIndex] != null)
                {
                    _cardButtons[slotIndex].onClick.AddListener(_cardButtonActions[slotIndex]);
                }
            }

            _confirmButton?.onClick.AddListener(ConfirmSelectionFromUi);
            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound)
            {
                return;
            }

            for (var slotIndex = 0; slotIndex < _cardButtons.Length; slotIndex++)
            {
                if (_cardButtons[slotIndex] != null && slotIndex < _cardButtonActions.Length)
                {
                    _cardButtons[slotIndex].onClick.RemoveListener(_cardButtonActions[slotIndex]);
                }
            }

            _confirmButton?.onClick.RemoveListener(ConfirmSelectionFromUi);
            _buttonsBound = false;
        }

        private void ApplyKoreanFonts()
        {
            var font = ResolveKoreanFont();
            if (font == null)
            {
                return;
            }

            ApplyFont(_titleText, font);
            ApplyFont(_statusText, font);
            ApplyFont(_confirmButtonText, font);
            ApplyFont(_fallbackCardIdTexts, font);
        }

        private void EnsureCardStatTexts()
        {
            var slotCount = _cardButtons?.Length ?? 0;
            if (slotCount <= 0)
            {
                return;
            }

            if (_attackValueTexts == null || _attackValueTexts.Length != slotCount)
            {
                Array.Resize(ref _attackValueTexts, slotCount);
            }

            if (_hpValueTexts == null || _hpValueTexts.Length != slotCount)
            {
                Array.Resize(ref _hpValueTexts, slotCount);
            }

            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var parent = _cardButtons[slotIndex]?.transform as RectTransform;
                if (parent == null)
                {
                    continue;
                }

                _attackValueTexts[slotIndex] = ResolveOrCreateStatText(
                    parent,
                    _attackValueTexts[slotIndex],
                    "AttackValueText",
                    isHp: false);
                _hpValueTexts[slotIndex] = ResolveOrCreateStatText(
                    parent,
                    _hpValueTexts[slotIndex],
                    "HpValueText",
                    isHp: true);
            }
        }

        private Text ResolveOrCreateStatText(
            RectTransform parent,
            Text current,
            string objectName,
            bool isHp)
        {
            if (current == null &&
                parent.Find(objectName) is RectTransform existingRect)
            {
                current = existingRect.GetComponent<Text>();
            }

            if (current == null)
            {
                var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
                textObject.layer = parent.gameObject.layer;
                textObject.transform.SetParent(parent, false);
                current = textObject.GetComponent<Text>();

#if UNITY_EDITOR
                if (!UnityEngine.Application.isPlaying)
                {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(textObject, $"Create {objectName}");
                }
#endif
            }

            ApplyStatTextStyle(current, isHp);
            current.transform.SetAsLastSibling();
            return current;
        }

        private void ApplyStatTextStyle(Text text, bool isHp)
        {
            if (text == null)
            {
                return;
            }

            // Numeric stat labels keep their per-object Inspector font across initialization and reactivation.
            if (text.font == null)
            {
                text.font = ResolveKoreanFont();
            }

            text.fontSize = Mathf.Max(1, _statFontSize);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.color = _statTextColor;
            text.raycastTarget = false;

            var normalizedPosition = isHp
                ? _hpStatNormalizedPosition
                : _attackStatNormalizedPosition;
            var rectTransform = text.rectTransform;
            rectTransform.anchorMin = normalizedPosition;
            rectTransform.anchorMax = normalizedPosition;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = _statTextSize;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            if (text.TryGetComponent<Outline>(out var outline))
            {
                outline.effectColor = _statOutlineColor;
                outline.effectDistance = _statOutlineDistance;
                outline.useGraphicAlpha = true;
            }
        }

        private void RefreshCardStats(int slotIndex, string cardId)
        {
            if (!_showCardStats ||
                _battleBootstrapper == null ||
                !_battleBootstrapper.TryGetCardDefinition(cardId, out var definition) ||
                !CardStatDisplay.TryCreate(definition, AccountSessionState.GetOwnedCardUpgradeLevel(cardId), out var stats))
            {
                SetCardStatVisible(slotIndex, false);
                return;
            }

            var attackText = GetAt(_attackValueTexts, slotIndex);
            var hpText = GetAt(_hpValueTexts, slotIndex);
            if (attackText != null)
            {
                attackText.text = stats.Attack.ToString();
            }

            if (hpText != null)
            {
                hpText.text = stats.HasHp ? stats.Hp.ToString() : string.Empty;
            }

            SetCardStatVisible(slotIndex, true, stats.HasHp);
        }

        private void RefreshCardStatLayouts()
        {
            var slotCount = _cardButtons?.Length ?? 0;
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var button = GetAt(_cardButtons, slotIndex);
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var artworkImage = GetAt(_cardArtworkImages, slotIndex);
                PositionCardStatText(
                    GetAt(_attackValueTexts, slotIndex),
                    artworkImage,
                    _attackStatNormalizedPosition);
                PositionCardStatText(
                    GetAt(_hpValueTexts, slotIndex),
                    artworkImage,
                    _hpStatNormalizedPosition);
            }
        }

        private static void PositionCardStatText(Text text, Image artworkImage, Vector2 normalizedSpritePosition)
        {
            if (artworkImage == null) return;
            HandCardStatOverlayLayout.PositionStatText(text?.rectTransform, artworkImage.rectTransform,
                HandCardStatOverlayLayout.CalculateRenderedSpriteRect(artworkImage), normalizedSpritePosition);
        }


        public static bool TryBuildCardStatValues(
            CardDefinition definition,
            int upgradeLevel,
            out int attack,
            out int hp)
        {
            var hasStats = CardStatDisplay.TryCreate(definition, upgradeLevel, out var stats);
            attack = stats.Attack;
            hp = stats.Hp;
            return hasStats;
        }

        private void SetCardStatVisible(int slotIndex, bool visible, bool showHp = true)
        {
            var attackText = GetAt(_attackValueTexts, slotIndex);
            if (attackText != null)
            {
                attackText.enabled = visible;
            }

            var hpText = GetAt(_hpValueTexts, slotIndex);
            if (hpText != null)
            {
                hpText.enabled = visible && showHp;
            }
        }

        private static Font ResolveKoreanFont()
        {
            if (s_koreanFont != null)
            {
                return s_koreanFont;
            }

            for (var i = 0; i < KoreanFontCandidates.Length; i++)
            {
                var font = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates[i], 32);
                if (font != null)
                {
                    s_koreanFont = font;
                    return s_koreanFont;
                }
            }

            s_koreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return s_koreanFont;
        }

        private static void ApplyFont(Text text, Font font)
        {
            if (text != null)
            {
                text.font = font;
            }
        }

        private static void ApplyFont(Text[] texts, Font font)
        {
            if (texts == null)
            {
                return;
            }

            for (var i = 0; i < texts.Length; i++)
            {
                ApplyFont(texts[i], font);
            }
        }

        private static T GetAt<T>(T[] values, int index) where T : class
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index]
                : null;
        }

        private static void RemoveFirst(List<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (!string.Equals(values[i], value, StringComparison.Ordinal))
                {
                    continue;
                }

                values.RemoveAt(i);
                return;
            }
        }
    }
}
