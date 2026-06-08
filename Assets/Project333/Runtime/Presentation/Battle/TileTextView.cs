using System;
using System.Collections;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class TileTextView : MonoBehaviour
    {
        private const string AutoVisualName = "OccupantVisual";
        private const string AutoStatBarName = "OccupantStatBar";
        private const string AutoSpellEffectName = "SpellEffectVisual";
        private const string AutoSpellEffectOverlayName = "SpellEffectOverlay";
        private const string AutoValuePopupOverlayName = "ValuePopupOverlay";
        private const string AutoPersistentMarkName = "PersistentMarkVisual";
        private const string AutoPreviewOverlayName = "CardPreviewOverlay";
        private const string AutoPreviewBackdropName = "PreviewBackdrop";
        private const string AutoPreviewImageName = "PreviewCardImage";
        private const string AutoInteractionSurfaceName = "TileInteractionSurface";
        private const string CheonraJimangEffectId = "cheonra_jimang";
        private const string CheonraJimangMarkResourcePath = "Project333/SpellEffects/CheonraJimangMark";
        private static readonly Vector2 DefaultOccupantVisualLiftOffset = new Vector2(0f, 60f);

        private static RectTransform s_previewOverlayRoot;
        private static Image s_previewBackdropImage;
        private static Image s_previewCardImage;
        private static RectTransform s_spellEffectOverlayRoot;
        private static RectTransform s_valuePopupOverlayRoot;
        private static Sprite s_cheonraJimangMarkSprite;
        private static Sprite s_heartIconSprite;
        private static Sprite s_swordIconSprite;
        private static Sprite s_bowIconSprite;
        private static Sprite s_masterIconSprite;
        private static Sprite s_fallbackOccupantSprite;

        [SerializeField] private TileView _tileView;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private string _emptyContent = "Empty";
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _selectedColor = new Color(0.55f, 1f, 1f);
        [SerializeField] private Color _playableColor = new Color(0.65f, 1f, 0.65f);
        [SerializeField] private Color _moveTargetColor = new Color(0.65f, 0.9f, 1f);
        [SerializeField] private Color _swapTargetColor = new Color(1f, 0.95f, 0.6f);
        [SerializeField] private Color _attackTargetColor = new Color(1f, 0.7f, 0.7f);
        [SerializeField] private Color _spellTargetColor = new Color(1f, 0.78f, 0.55f);
        [SerializeField] private Color _tileBackgroundNormalColor = Color.white;
        [SerializeField] private Color _tileBackgroundSelectedTint = new Color(1f, 0.82f, 0.82f, 1f);
        [SerializeField] private Color _tileBackgroundDragHoverTint = new Color(1f, 0.78f, 0.78f, 1f);
        [SerializeField] private float _pulseScale = 1.035f;
        [SerializeField] private float _pulseSpeed = 5f;
        [Header("Tile Text")]
        [SerializeField] private bool _showTileText;
        [SerializeField] private bool _hideTextWhenVisualIsActive = true;
        [Header("Tile Interaction")]
        [SerializeField] private bool _useTileImageClickSurface = true;
        [SerializeField] private Vector2 _fallbackClickSurfaceSize = new Vector2(140f, 140f);
        [Header("Occupant Visual")]
        [SerializeField] private BattleBootstrapper _battleBootstrapper;
        [SerializeField] private RectTransform _occupantVisualRoot;
        [SerializeField] private Image _occupantVisualImage;
        [SerializeField] private Animator _occupantVisualAnimator;
        [SerializeField] private bool _showOccupantVisual = true;
        [SerializeField] private Vector2 _occupantVisualSize = new Vector2(96f, 96f);
        [SerializeField] private Vector2 _occupantVisualAnchoredPosition = Vector2.zero;
        [SerializeField] private Vector2 _occupantVisualFineOffset = Vector2.zero;
        [SerializeField] private float _occupantVisualScaleMultiplier = 1.5f;
        [SerializeField] private bool _overrideOccupantVisualSorting = true;
        [SerializeField] private int _occupantVisualSortingOrder = 250;
        [Header("Occupant Stats")]
        [SerializeField] private bool _showOccupantStats = true;
        [SerializeField] private Vector2 _occupantStatBarSize = new Vector2(112f, 26f);
        [SerializeField] private Vector2 _occupantStatBarOffset = new Vector2(0f, -64f);
        [SerializeField] private Vector2 _occupantStatIconSize = new Vector2(16f, 16f);
        [SerializeField] private int _occupantStatFontSize = 18;
        [SerializeField] private Color _occupantStatBarColor = new Color(0.08f, 0.08f, 0.1f, 0.82f);
        [SerializeField] private Color _occupantStatTextColor = Color.white;
        [SerializeField] private Color _occupantStatIconColor = Color.white;
        [SerializeField] private Color _occupantStatDisabledBarColor = new Color(0.18f, 0.18f, 0.2f, 0.88f);
        [SerializeField] private Color _occupantStatDisabledTextColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        [SerializeField] private Color _occupantStatDisabledIconColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        [Header("Spell Effects")]
        [SerializeField] private Vector2 _spellEffectSize = new Vector2(220f, 220f);
        [SerializeField] private Vector2 _spellEffectOffset = Vector2.zero;
        [SerializeField] private int _spellEffectSortingOrder = 4900;
        [Header("Value Popups")]
        [SerializeField] private Vector2 _valuePopupOffset = new Vector2(40f, 96f);
        [SerializeField] private float _valuePopupDuration = 1f;
        [SerializeField] private float _valuePopupRiseDistance = 26f;
        [SerializeField] private int _valuePopupFontSize = 28;
        [SerializeField] private int _valuePopupSortingOrder = 4975;
        [SerializeField] private Color _damagePopupColor = new Color(1f, 0.32f, 0.32f, 1f);
        [SerializeField] private Color _healingPopupColor = new Color(0.45f, 1f, 0.45f, 1f);
        [Header("Persistent Marks")]
        [SerializeField] private Vector2 _persistentMarkSize = new Vector2(150f, 180f);
        [SerializeField] private Vector2 _persistentMarkOffset = new Vector2(0f, 0f);
        [SerializeField] private int _persistentMarkSortingOrder = 360;
        [Header("Card Preview")]
        [SerializeField] private float _longPressPreviewHoldSeconds = 0.4f;
        [SerializeField] private Vector2 _cardPreviewSize = new Vector2(440f, 620f);
        [SerializeField] private Color _cardPreviewBackdropColor = new Color(0f, 0f, 0f, 0.74f);
        [SerializeField] private int _cardPreviewSortingOrder = 5000;
        [Header("Animation States")]
        [SerializeField] private string _idleStateName = "Idle";
        [SerializeField] private string _disabledStateName = "Disabled";
        [SerializeField] private string _runStateName = "Run";
        [SerializeField] private string _attackStateName = "Attack";
        [SerializeField] private string _beAttackedStateName = "BeAttacked";
        [SerializeField] private string _deathStateName = "Death";

        private string _title = string.Empty;
        private string _content = "Empty";
        private bool _isOccupied;
        private BattleHighlightState _lastHighlightState = BattleHighlightState.None;
        private string _lastRenderedText = string.Empty;
        private Vector3 _baseScale = Vector3.one;
        private string _lastVisualCardId = string.Empty;
        private string _lastVisualRuntimeId = string.Empty;
        private Sprite _lastVisualSprite;
        private RuntimeAnimatorController _lastVisualController;
        private bool _lastVisualWasDisabled;
        private bool _isVisualActive;
        private Canvas _occupantVisualCanvas;
        private SpriteAnimationSequence _activeSpriteSequence;
        private int _activeSequenceFrameIndex;
        private float _activeSequenceFrameTimer;
        private GameObject _sampleAnimationObject;
        private SpriteRenderer _sampleSpriteRenderer;
        private RectTransform _occupantStatRoot;
        private Image _occupantStatBackgroundImage;
        private Image _occupantStatHeartImage;
        private TMP_Text _occupantStatHpText;
        private Image _occupantStatAttackTypeImage;
        private TMP_Text _occupantStatAttackText;
        private RectTransform _spellEffectRoot;
        private Canvas _spellEffectCanvas;
        private Image _spellEffectImage;
        private Coroutine _spellEffectCoroutine;
        private RectTransform _persistentMarkRoot;
        private Canvas _persistentMarkCanvas;
        private Image _persistentMarkImage;
        private TileLongPressRelay _longPressRelay;
        private RectTransform _interactionSurfaceRoot;
        private Image _interactionSurfaceImage;

        public float LongPressPreviewHoldSeconds => Mathf.Max(0.05f, _longPressPreviewHoldSeconds);

        private void Awake()
        {
            AutoAssignView();
            AutoAssignBootstrapper();
            AutoAssignVisualReferences();
            CacheBaseScale();
            ApplyVisualLayout();
            ApplyStatBarLayout();
            EnsureInteractionSurface();
        }

        private void OnValidate()
        {
            AutoAssignView();
            AutoAssignBootstrapper();
            AutoAssignVisualReferences();
            ApplyVisualLayout();
            ApplyStatBarLayout();
            ApplyInteractionSurfaceLayout();
        }

        private void LateUpdate()
        {
            Refresh();
            EnsureInteractionSurface();
            AdvanceSampledAnimation();
            ApplyPulse();
        }

        private void OnDestroy()
        {
            if (_spellEffectCoroutine != null)
            {
                StopCoroutine(_spellEffectCoroutine);
                _spellEffectCoroutine = null;
            }

            if (_sampleAnimationObject != null)
            {
                Destroy(_sampleAnimationObject);
                _sampleAnimationObject = null;
                _sampleSpriteRenderer = null;
            }

            if (_spellEffectRoot != null)
            {
                Destroy(_spellEffectRoot.gameObject);
                _spellEffectRoot = null;
                _spellEffectCanvas = null;
                _spellEffectImage = null;
            }

            if (_persistentMarkRoot != null)
            {
                Destroy(_persistentMarkRoot.gameObject);
                _persistentMarkRoot = null;
                _persistentMarkCanvas = null;
                _persistentMarkImage = null;
            }

            HideCurrentCardPreview();
        }

        public void SetTile(string title, string content, bool isOccupied)
        {
            _title = title ?? string.Empty;
            _content = content ?? string.Empty;
            _isOccupied = isOccupied;
            Refresh();
        }

        public Vector3 GetVisualWorldPosition()
        {
            AutoAssignVisualReferences();

            if (_occupantVisualRoot != null)
            {
                return _occupantVisualRoot.position;
            }

            if (_text != null)
            {
                return _text.rectTransform.position;
            }

            return transform.position;
        }

        public Vector3 GetTileAnchorWorldPosition()
        {
            return GetTileAnchorWorldPosition(Vector2.zero);
        }

        public Vector3 GetTileAnchorWorldPosition(Vector2 localOffset)
        {
            var tileBackgroundRect = FindTileBackgroundRect();
            if (tileBackgroundRect != null)
            {
                return tileBackgroundRect.TransformPoint(localOffset);
            }

            if (_interactionSurfaceRoot != null)
            {
                return _interactionSurfaceRoot.TransformPoint(localOffset);
            }

            if (_text != null)
            {
                return _text.rectTransform.TransformPoint(localOffset);
            }

            return transform.TransformPoint(localOffset);
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            var targetRect = _interactionSurfaceRoot;
            if (targetRect == null)
            {
                targetRect = FindTileBackgroundRect();
            }

            if (targetRect == null && _text != null)
            {
                targetRect = _text.rectTransform;
            }

            return targetRect != null && RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPoint, null);
        }

        public void SetVisualWorldPosition(Vector3 worldPosition)
        {
            EnsureRuntimeVisualObjects();
            AutoAssignVisualReferences();

            if (_occupantVisualRoot == null)
            {
                return;
            }

            _occupantVisualRoot.position = worldPosition;
        }

        public void RestoreVisualLayout()
        {
            AutoAssignVisualReferences();
            ApplyVisualLayout();
            ApplyStatBarLayout();
        }

        public void PlayIdleAnimation()
        {
            PlayAnimationState(_idleStateName);
        }

        public void PlayDisabledAnimation()
        {
            PlayAnimationState(_disabledStateName);
        }

        public void PlayRunAnimation()
        {
            PlayAnimationState(_runStateName);
        }

        public void PlayAttackAnimation()
        {
            PlayAnimationState(_attackStateName);
        }

        public void PlayBeAttackedAnimation()
        {
            PlayAnimationState(_beAttackedStateName);
        }

        public void PlayDeathAnimation()
        {
            PlayAnimationState(_deathStateName);
        }

        public float GetIdleAnimationDurationSeconds()
        {
            return GetAnimationDurationSeconds(_idleStateName);
        }

        public float GetDisabledAnimationDurationSeconds()
        {
            return GetAnimationDurationSeconds(_disabledStateName);
        }

        public float GetRunAnimationDurationSeconds()
        {
            return GetAnimationDurationSeconds(_runStateName);
        }

        public float GetAttackAnimationDurationSeconds()
        {
            return GetAnimationDurationSeconds(_attackStateName);
        }

        public float GetBeAttackedAnimationDurationSeconds()
        {
            return GetAnimationDurationSeconds(_beAttackedStateName);
        }

        public float GetDeathAnimationDurationSeconds()
        {
            return GetAnimationDurationSeconds(_deathStateName);
        }

        public float PlayTransientSpriteEffect(IReadOnlyList<Sprite> frames, float framesPerSecond, Vector2 size)
        {
            if (frames == null || frames.Count == 0 || !UnityEngine.Application.isPlaying)
            {
                return 0f;
            }

            EnsureRuntimeSpellEffectObjects();
            if (_spellEffectRoot == null || _spellEffectImage == null)
            {
                return 0f;
            }

            if (_spellEffectCoroutine != null)
            {
                StopCoroutine(_spellEffectCoroutine);
            }

            var resolvedSize = size == Vector2.zero ? _spellEffectSize : size;
            _spellEffectCoroutine = StartCoroutine(PlayTransientSpriteEffectSequence(frames, framesPerSecond, resolvedSize));
            return frames.Count / Mathf.Max(1f, framesPerSecond);
        }

        public void QueueFloatingValuePopup(int amount, bool isHealing, float delaySeconds = 0f, int stackIndex = 0)
        {
            if (!UnityEngine.Application.isPlaying || amount <= 0)
            {
                return;
            }

            StartCoroutine(PlayFloatingValuePopupSequence(amount, isHealing, delaySeconds, stackIndex));
        }

        public bool CanShowCurrentCardPreview()
        {
            return TryGetPreviewSprite(out _);
        }

        public bool TryShowCurrentCardPreview()
        {
            if (!TryGetPreviewSprite(out var previewSprite))
            {
                return false;
            }

            EnsurePreviewOverlay();
            if (s_previewOverlayRoot == null || s_previewCardImage == null)
            {
                return false;
            }

            s_previewCardImage.sprite = previewSprite;
            s_previewCardImage.enabled = true;
            s_previewOverlayRoot.SetAsLastSibling();
            s_previewOverlayRoot.gameObject.SetActive(true);
            return true;
        }

        public void HideCurrentCardPreview()
        {
            if (s_previewCardImage != null)
            {
                s_previewCardImage.sprite = null;
                s_previewCardImage.enabled = false;
            }

            if (s_previewOverlayRoot != null)
            {
                s_previewOverlayRoot.gameObject.SetActive(false);
            }
        }

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            RefreshOccupantVisual();
            RefreshPersistentMark();
            RefreshOccupantStats();
            RefreshText();
            RefreshTileBackgroundHighlight();
        }

        private void RefreshText()
        {
            if (_text == null)
            {
                return;
            }

            var resolvedContent = _isOccupied || !string.IsNullOrWhiteSpace(_content)
                ? _content
                : _emptyContent;

            var highlightState = _tileView == null ? BattleHighlightState.None : _tileView.HighlightState;
            var highlightLabel = BattleUiFormatter.FormatHighlightLabel(highlightState);
            var renderedText = BattleRichTextStyler.StyleTile(_title, resolvedContent, highlightLabel, _isOccupied);

            if (_lastRenderedText != renderedText)
            {
                _text.text = renderedText;
                _lastRenderedText = renderedText;
            }

            if (_lastHighlightState != highlightState)
            {
                _lastHighlightState = highlightState;
            }

            UpdateTextVisibility(_isVisualActive);
        }

        private void RefreshOccupantVisual()
        {
            if (!_showOccupantVisual && !_showOccupantStats)
            {
                ClearVisual();
                UpdateTextVisibility(false);
                return;
            }

            AutoAssignBootstrapper();
            EnsureRuntimeVisualObjects();
            AutoAssignVisualReferences();

            if (_occupantVisualRoot == null || _occupantVisualImage == null || _occupantVisualAnimator == null)
            {
                UpdateTextVisibility(false);
                return;
            }

            EnsureOccupantVisualCanvas();

            var occupantCardId = _tileView == null ? string.Empty : _tileView.CurrentOccupantCardId;
            var occupantRuntimeId = _tileView == null ? string.Empty : _tileView.CurrentOccupantRuntimeId;

            if (!_isOccupied || string.IsNullOrWhiteSpace(occupantCardId) || string.IsNullOrWhiteSpace(occupantRuntimeId))
            {
                ClearVisual();
                UpdateTextVisibility(false);
                return;
            }

            CardDefinitionAsset cardAsset = null;
            var hasCardAsset = _battleBootstrapper != null &&
                               _battleBootstrapper.TryGetCardDefinitionAsset(occupantCardId, out cardAsset);
            var occupant = _tileView == null ? null : _tileView.CurrentOccupant;

            var boardSprite = hasCardAsset && cardAsset != null
                ? cardAsset.BoardSprite
                : null;
            if (boardSprite == null && CardArtworkLibrary.TryGetArtwork(occupantCardId, out var fallbackArtwork))
            {
                boardSprite = fallbackArtwork;
            }

            if (boardSprite == null)
            {
                boardSprite = GetFallbackOccupantSprite(occupant);
            }

            var boardAnimatorController = hasCardAsset && cardAsset != null
                ? cardAsset.BoardAnimatorController
                : null;
            var hasVisual = boardSprite != null || boardAnimatorController != null || _showOccupantStats;

            if (!hasVisual)
            {
                ClearVisual();
                UpdateTextVisibility(false);
                return;
            }

            var shouldRebind =
                !_isVisualActive ||
                _lastVisualCardId != occupantCardId ||
                _lastVisualRuntimeId != occupantRuntimeId ||
                _lastVisualSprite != boardSprite ||
                _lastVisualController != boardAnimatorController;

            SetVisualActive(true);

            if (shouldRebind)
            {
                _lastVisualCardId = occupantCardId;
                _lastVisualRuntimeId = occupantRuntimeId;
                _lastVisualSprite = boardSprite;
                _lastVisualController = boardAnimatorController;
                BindVisual(boardSprite, boardAnimatorController, occupant);
                _isVisualActive = true;
            }
            else if (_lastVisualWasDisabled != occupant.IsDisabled)
            {
                ApplyPassiveAnimationState(occupant);
            }

            UpdateTextVisibility(true);
        }

        private void RefreshOccupantStats()
        {
            if (!_showOccupantStats)
            {
                SetStatBarActive(false);
                return;
            }

            EnsureRuntimeVisualObjects();
            EnsureRuntimeStatObjects();

            if (_occupantStatRoot == null)
            {
                return;
            }

            var occupant = _tileView == null ? null : _tileView.CurrentOccupant;
            if (!_isOccupied || occupant == null)
            {
                SetStatBarActive(false);
                return;
            }

            BindStatBar(occupant);
            SetStatBarActive(true);
        }

        private void RefreshPersistentMark()
        {
            EnsureRuntimePersistentMarkObjects();
            if (_persistentMarkRoot == null || _persistentMarkImage == null)
            {
                return;
            }

            if (!TryShouldShowCheonraJimangMark())
            {
                SetPersistentMarkActive(false);
                return;
            }

            var markSprite = GetCheonraJimangMarkSprite();
            if (markSprite == null)
            {
                SetPersistentMarkActive(false);
                return;
            }

            _persistentMarkImage.sprite = markSprite;
            _persistentMarkImage.enabled = true;
            ApplyPersistentMarkLayout();
            SetPersistentMarkActive(true);
        }

        private void BindVisual(Sprite boardSprite, RuntimeAnimatorController boardAnimatorController, OccupantState occupant)
        {
            if (_occupantVisualImage == null || _occupantVisualAnimator == null)
            {
                return;
            }

            _occupantVisualImage.sprite = boardSprite;
            _occupantVisualImage.enabled = boardSprite != null;
            _occupantVisualAnimator.runtimeAnimatorController = boardAnimatorController;
            _occupantVisualAnimator.enabled = boardAnimatorController != null;
            RestoreVisualLayout();

            if (boardAnimatorController != null)
            {
                _occupantVisualAnimator.Rebind();

                if (_occupantVisualAnimator.isActiveAndEnabled)
                {
                    _occupantVisualAnimator.Update(0f);
                }

                ApplyPassiveAnimationState(occupant);
            }
            else
            {
                _activeSpriteSequence = null;
                _activeSequenceFrameIndex = 0;
                _activeSequenceFrameTimer = 0f;
            }
        }

        private void ApplyPassiveAnimationState(OccupantState occupant)
        {
            _lastVisualWasDisabled = occupant != null && occupant.IsDisabled;

            if (_lastVisualWasDisabled)
            {
                PlayDisabledAnimation();
                return;
            }

            PlayIdleAnimation();
        }

        private void BindStatBar(OccupantState occupant)
        {
            if (_occupantStatHeartImage == null || _occupantStatHpText == null || _occupantStatAttackTypeImage == null || _occupantStatAttackText == null)
            {
                return;
            }

            var barColor = occupant.IsDisabled
                ? _occupantStatDisabledBarColor
                : _occupantStatBarColor;
            var textColor = occupant.IsDisabled
                ? _occupantStatDisabledTextColor
                : _occupantStatTextColor;
            var iconColor = occupant.IsDisabled
                ? _occupantStatDisabledIconColor
                : _occupantStatIconColor;

            _occupantStatHeartImage.sprite = GetHeartIconSprite();
            _occupantStatAttackTypeImage.sprite = occupant.AttackType == AttackType.Melee
                ? GetSwordIconSprite()
                : GetBowIconSprite();
            _occupantStatHpText.text = Mathf.Max(0, occupant.CurrentHp).ToString();
            _occupantStatAttackText.text = Mathf.Max(0, occupant.Attack).ToString();
            if (_occupantStatBackgroundImage != null)
            {
                _occupantStatBackgroundImage.color = barColor;
            }

            _occupantStatHeartImage.color = iconColor;
            _occupantStatAttackTypeImage.color = iconColor;
            _occupantStatHpText.color = textColor;
            _occupantStatAttackText.color = textColor;
        }

        private void ClearVisual()
        {
            _lastVisualCardId = string.Empty;
            _lastVisualRuntimeId = string.Empty;
            _lastVisualSprite = null;
            _lastVisualController = null;
            _lastVisualWasDisabled = false;
            _isVisualActive = false;
            _activeSpriteSequence = null;
            _activeSequenceFrameIndex = 0;
            _activeSequenceFrameTimer = 0f;

            if (_occupantVisualAnimator != null)
            {
                _occupantVisualAnimator.runtimeAnimatorController = null;
                _occupantVisualAnimator.enabled = false;
            }

            if (_occupantVisualImage != null)
            {
                _occupantVisualImage.sprite = null;
                _occupantVisualImage.enabled = false;
            }

            SetStatBarActive(false);
            SetPersistentMarkActive(false);
            SetVisualActive(false);
        }

        private void SetVisualActive(bool isActive)
        {
            if (_occupantVisualRoot == null)
            {
                return;
            }

            if (_occupantVisualRoot.gameObject.activeSelf != isActive)
            {
                _occupantVisualRoot.gameObject.SetActive(isActive);
            }
        }

        private void SetStatBarActive(bool isActive)
        {
            if (_occupantStatRoot == null)
            {
                return;
            }

            if (_occupantStatRoot.gameObject.activeSelf != isActive)
            {
                _occupantStatRoot.gameObject.SetActive(isActive);
            }
        }

        private void SetPersistentMarkActive(bool isActive)
        {
            if (_persistentMarkRoot == null)
            {
                return;
            }

            if (_persistentMarkRoot.gameObject.activeSelf != isActive)
            {
                _persistentMarkRoot.gameObject.SetActive(isActive);
            }
        }

        private void UpdateTextVisibility(bool hasActiveVisual)
        {
            if (_text == null)
            {
                return;
            }

            var shouldShowText = _showTileText && (!_hideTextWhenVisualIsActive || !hasActiveVisual);
            var displayColor = ResolveColor(_lastHighlightState);
            displayColor.a = shouldShowText ? displayColor.a : 0f;
            _text.color = displayColor;
        }

        private void PlayAnimationState(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName) || _occupantVisualAnimator == null)
            {
                return;
            }

            if (!_occupantVisualAnimator.enabled || _occupantVisualAnimator.runtimeAnimatorController == null)
            {
                PlaySampledClip(stateName, false);
                return;
            }

            var animatorStateWasPlayed = false;

            foreach (var candidateStateName in EnumerateStateNameCandidates(stateName))
            {
                var stateHash = Animator.StringToHash(candidateStateName);
                if (!_occupantVisualAnimator.HasState(0, stateHash))
                {
                    continue;
                }

                _occupantVisualAnimator.Play(stateHash, 0, 0f);

                if (_occupantVisualAnimator.isActiveAndEnabled)
                {
                    _occupantVisualAnimator.Update(0f);
                }

                animatorStateWasPlayed = true;
                break;
            }

            PlaySampledClip(stateName, animatorStateWasPlayed);
        }

        private IEnumerable<string> EnumerateStateNameCandidates(string stateName)
        {
            yield return stateName;

            if (_tileView == null || string.IsNullOrWhiteSpace(_tileView.CurrentOccupantCardId))
            {
                yield break;
            }

            yield return $"{_tileView.CurrentOccupantCardId}_{stateName}";
            yield return $"{_tileView.CurrentOccupantCardId}{stateName}";
        }

        private void PlaySampledClip(string stateName, bool animatorStateWasPlayed)
        {
            if (_lastVisualController == null)
            {
                _activeSpriteSequence = null;
                return;
            }

            var matchingClip = FindMatchingClip(stateName);
            if (matchingClip == null)
            {
                if (!animatorStateWasPlayed)
                {
                    _activeSpriteSequence = null;
                }

                return;
            }

            var spriteSequence = CreateSpriteSequence(matchingClip, stateName);
            if (spriteSequence == null || spriteSequence.Frames.Count == 0)
            {
                return;
            }

            _activeSpriteSequence = spriteSequence;
            _activeSequenceFrameIndex = 0;
            _activeSequenceFrameTimer = 0f;
            ApplySequenceFrame(0);
        }

        private AnimationClip FindMatchingClip(string stateName)
        {
            if (_lastVisualController == null || string.IsNullOrWhiteSpace(stateName))
            {
                return null;
            }

            foreach (var candidateName in EnumerateStateNameCandidates(stateName))
            {
                foreach (var clip in _lastVisualController.animationClips)
                {
                    if (clip != null && clip.name == candidateName)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private void AdvanceSampledAnimation()
        {
            if (_activeSpriteSequence == null || _activeSpriteSequence.Frames.Count == 0)
            {
                return;
            }

            if (_activeSpriteSequence.Frames.Count == 1)
            {
                ApplySequenceFrame(0);
                return;
            }

            var frameDuration = 1f / Mathf.Max(1f, _activeSpriteSequence.FramesPerSecond);
            _activeSequenceFrameTimer += Time.unscaledDeltaTime;

            while (_activeSequenceFrameTimer >= frameDuration)
            {
                _activeSequenceFrameTimer -= frameDuration;
                var nextFrameIndex = _activeSequenceFrameIndex + 1;

                if (nextFrameIndex >= _activeSpriteSequence.Frames.Count)
                {
                    nextFrameIndex = _activeSpriteSequence.IsLooping
                        ? 0
                        : _activeSpriteSequence.Frames.Count - 1;
                }

                if (nextFrameIndex == _activeSequenceFrameIndex && !_activeSpriteSequence.IsLooping)
                {
                    break;
                }

                _activeSequenceFrameIndex = nextFrameIndex;
                ApplySequenceFrame(_activeSequenceFrameIndex);
            }
        }

        private void ApplySequenceFrame(int frameIndex)
        {
            if (_activeSpriteSequence == null || _occupantVisualImage == null)
            {
                return;
            }

            if (frameIndex < 0 || frameIndex >= _activeSpriteSequence.Frames.Count)
            {
                return;
            }

            var sprite = _activeSpriteSequence.Frames[frameIndex];
            if (sprite != null)
            {
                _occupantVisualImage.sprite = sprite;
                _occupantVisualImage.enabled = true;
            }
        }

        private SpriteAnimationSequence CreateSpriteSequence(AnimationClip matchingClip, string stateName)
        {
            if (matchingClip == null)
            {
                return null;
            }

#if UNITY_EDITOR
            var frames = new List<Sprite>();
            var bindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(matchingClip);
            foreach (var binding in bindings)
            {
                if (binding.propertyName != "m_Sprite")
                {
                    continue;
                }

                var keyframes = UnityEditor.AnimationUtility.GetObjectReferenceCurve(matchingClip, binding);
                foreach (var keyframe in keyframes)
                {
                    if (keyframe.value is Sprite sprite)
                    {
                        frames.Add(sprite);
                    }
                }

                break;
            }

            if (frames.Count > 0)
            {
                return new SpriteAnimationSequence(frames, matchingClip.frameRate, ShouldLoopState(stateName, matchingClip));
            }
#endif

            EnsureSampleAnimationObjects();
            if (_sampleAnimationObject == null || _sampleSpriteRenderer == null)
            {
                return null;
            }

            var sampledFrames = new List<Sprite>();
            var clipLength = Mathf.Max(0.0001f, matchingClip.length);
            var sampleRate = Mathf.Max(1f, matchingClip.frameRate);
            var estimatedFrameCount = Mathf.Max(1, Mathf.RoundToInt(clipLength * sampleRate));
            for (var frameIndex = 0; frameIndex < estimatedFrameCount; frameIndex++)
            {
                var sampleTime = Mathf.Min(clipLength, frameIndex / sampleRate);
                matchingClip.SampleAnimation(_sampleAnimationObject, sampleTime);
                if (_sampleSpriteRenderer.sprite != null)
                {
                    sampledFrames.Add(_sampleSpriteRenderer.sprite);
                }
            }

            return sampledFrames.Count == 0
                ? null
                : new SpriteAnimationSequence(sampledFrames, sampleRate, ShouldLoopState(stateName, matchingClip));
        }

        private void EnsureSampleAnimationObjects()
        {
            if (_sampleAnimationObject != null && _sampleSpriteRenderer != null)
            {
                return;
            }

            _sampleAnimationObject = new GameObject($"{AutoVisualName}_Sampler", typeof(SpriteRenderer));
            _sampleAnimationObject.hideFlags = HideFlags.HideAndDontSave;
            _sampleSpriteRenderer = _sampleAnimationObject.GetComponent<SpriteRenderer>();
            _sampleSpriteRenderer.enabled = false;
        }

        private void EnsureInteractionSurface()
        {
            if (_text == null)
            {
                return;
            }

            AutoAssignVisualReferences();

            if (_interactionSurfaceRoot == null)
            {
                var surfaceTransform = _text.rectTransform.parent == null
                    ? null
                    : _text.rectTransform.parent.Find(AutoInteractionSurfaceName);
                if (surfaceTransform is RectTransform surfaceRectTransform)
                {
                    _interactionSurfaceRoot = surfaceRectTransform;
                    _interactionSurfaceImage = surfaceRectTransform.GetComponent<Image>();
                }
            }

            if (_interactionSurfaceRoot == null && UnityEngine.Application.isPlaying)
            {
                var surfaceObject = new GameObject(AutoInteractionSurfaceName, typeof(RectTransform), typeof(Image));
                var interactionParent = _text.rectTransform.parent != null
                    ? _text.rectTransform.parent
                    : _text.rectTransform;
                surfaceObject.transform.SetParent(interactionParent, false);
                _interactionSurfaceRoot = surfaceObject.GetComponent<RectTransform>();
                _interactionSurfaceImage = surfaceObject.GetComponent<Image>();
            }

            ApplyInteractionSurfaceLayout();

            if (_interactionSurfaceImage != null)
            {
                _interactionSurfaceImage.color = new Color(1f, 1f, 1f, 0.001f);
                _interactionSurfaceImage.raycastTarget = _useTileImageClickSurface;
                _interactionSurfaceImage.sprite = null;
                _interactionSurfaceImage.type = Image.Type.Simple;
            }

            if (!UnityEngine.Application.isPlaying || _interactionSurfaceRoot == null)
            {
                return;
            }

            if (_longPressRelay == null)
            {
                _longPressRelay = _interactionSurfaceRoot.GetComponent<TileLongPressRelay>();
            }

            if (_longPressRelay == null)
            {
                _longPressRelay = _interactionSurfaceRoot.gameObject.AddComponent<TileLongPressRelay>();
            }

            _longPressRelay.Initialize(this, _tileView);

            if (_text != null)
            {
                _text.raycastTarget = false;
            }
        }

        private void ApplyInteractionSurfaceLayout()
        {
            if (_interactionSurfaceRoot == null || _text == null)
            {
                return;
            }

            RectTransform referenceRect = null;
            if (_text.rectTransform.parent != null)
            {
                var tileBackground = _text.rectTransform.parent.Find("TileBackground");
                if (tileBackground is RectTransform tileBackgroundRect)
                {
                    referenceRect = tileBackgroundRect;
                }
            }

            if (referenceRect != null)
            {
                _interactionSurfaceRoot.anchorMin = referenceRect.anchorMin;
                _interactionSurfaceRoot.anchorMax = referenceRect.anchorMax;
                _interactionSurfaceRoot.pivot = referenceRect.pivot;
                _interactionSurfaceRoot.anchoredPosition = referenceRect.anchoredPosition;
                _interactionSurfaceRoot.sizeDelta = referenceRect.sizeDelta;
                _interactionSurfaceRoot.localRotation = referenceRect.localRotation;
                _interactionSurfaceRoot.localScale = referenceRect.localScale;
            }
            else
            {
                _interactionSurfaceRoot.anchorMin = _text.rectTransform.anchorMin;
                _interactionSurfaceRoot.anchorMax = _text.rectTransform.anchorMax;
                _interactionSurfaceRoot.pivot = _text.rectTransform.pivot;
                _interactionSurfaceRoot.anchoredPosition = _text.rectTransform.anchoredPosition;
                _interactionSurfaceRoot.sizeDelta = _fallbackClickSurfaceSize;
                _interactionSurfaceRoot.localRotation = Quaternion.identity;
                _interactionSurfaceRoot.localScale = Vector3.one;
            }

            _interactionSurfaceRoot.SetAsLastSibling();
        }

        private bool TryGetPreviewSprite(out Sprite previewSprite)
        {
            previewSprite = null;

            var cardId = _tileView == null ? string.Empty : _tileView.CurrentOccupantCardId;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            if (CardArtworkLibrary.TryGetArtwork(cardId, out previewSprite) && previewSprite != null)
            {
                return true;
            }

            AutoAssignBootstrapper();
            if (_battleBootstrapper != null &&
                _battleBootstrapper.TryGetCardDefinitionAsset(cardId, out var cardAsset) &&
                cardAsset.BoardSprite != null)
            {
                previewSprite = cardAsset.BoardSprite;
                return true;
            }

            previewSprite = _occupantVisualImage == null ? null : _occupantVisualImage.sprite;
            return previewSprite != null;
        }

        private void EnsurePreviewOverlay()
        {
            if (s_previewOverlayRoot != null && s_previewBackdropImage != null && s_previewCardImage != null)
            {
                return;
            }

            var overlayCanvasTransform = ResolveOverlayCanvasTransform();
            if (overlayCanvasTransform == null)
            {
                return;
            }

            var existingOverlayTransform = overlayCanvasTransform.Find(AutoPreviewOverlayName);
            if (existingOverlayTransform is RectTransform existingOverlayRoot)
            {
                s_previewOverlayRoot = existingOverlayRoot;
                s_previewBackdropImage = existingOverlayRoot.Find(AutoPreviewBackdropName)?.GetComponent<Image>();
                s_previewCardImage = existingOverlayRoot.Find(AutoPreviewImageName)?.GetComponent<Image>();
            }

            if (s_previewOverlayRoot == null)
            {
                var overlayObject = new GameObject(
                    AutoPreviewOverlayName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                overlayObject.transform.SetParent(overlayCanvasTransform, false);
                s_previewOverlayRoot = overlayObject.GetComponent<RectTransform>();

                var backdropObject = new GameObject(AutoPreviewBackdropName, typeof(RectTransform), typeof(Image));
                backdropObject.transform.SetParent(s_previewOverlayRoot, false);
                s_previewBackdropImage = backdropObject.GetComponent<Image>();

                var previewImageObject = new GameObject(AutoPreviewImageName, typeof(RectTransform), typeof(Image));
                previewImageObject.transform.SetParent(s_previewOverlayRoot, false);
                s_previewCardImage = previewImageObject.GetComponent<Image>();
            }

            if (s_previewOverlayRoot == null || s_previewBackdropImage == null || s_previewCardImage == null)
            {
                return;
            }

            var overlayCanvas = s_previewOverlayRoot.GetComponent<Canvas>();
            if (overlayCanvas != null)
            {
                overlayCanvas.overrideSorting = true;
                overlayCanvas.sortingOrder = _cardPreviewSortingOrder;
            }

            s_previewOverlayRoot.anchorMin = Vector2.zero;
            s_previewOverlayRoot.anchorMax = Vector2.one;
            s_previewOverlayRoot.pivot = new Vector2(0.5f, 0.5f);
            s_previewOverlayRoot.offsetMin = Vector2.zero;
            s_previewOverlayRoot.offsetMax = Vector2.zero;
            s_previewOverlayRoot.localScale = Vector3.one;

            var backdropRect = s_previewBackdropImage.rectTransform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.pivot = new Vector2(0.5f, 0.5f);
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            s_previewBackdropImage.color = _cardPreviewBackdropColor;
            s_previewBackdropImage.raycastTarget = false;

            var previewRect = s_previewCardImage.rectTransform;
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = Vector2.zero;
            previewRect.sizeDelta = _cardPreviewSize;
            s_previewCardImage.color = Color.white;
            s_previewCardImage.preserveAspect = true;
            s_previewCardImage.raycastTarget = false;
            s_previewOverlayRoot.gameObject.SetActive(false);
        }

        private Transform ResolveOverlayCanvasTransform()
        {
            if (_text != null && _text.canvas != null)
            {
                var rootCanvas = _text.canvas.rootCanvas;
                if (rootCanvas != null)
                {
                    return rootCanvas.transform;
                }

                return _text.canvas.transform;
            }

            return null;
        }

        private void AutoAssignView()
        {
            if (_tileView == null)
            {
                _tileView = GetComponent<TileView>();
            }
        }

        private float GetAnimationDurationSeconds(string stateName)
        {
            var matchingClip = FindMatchingClip(stateName);
            return matchingClip == null ? 0f : matchingClip.length;
        }

        private bool ShouldLoopState(string stateName, AnimationClip matchingClip)
        {
            if (string.Equals(stateName, _idleStateName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, _disabledStateName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, _runStateName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return matchingClip != null && matchingClip.isLooping &&
                   !string.Equals(stateName, _attackStateName, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(stateName, _beAttackedStateName, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(stateName, _deathStateName, StringComparison.OrdinalIgnoreCase);
        }

        private void AutoAssignBootstrapper()
        {
            if (_battleBootstrapper == null)
            {
                _battleBootstrapper = UnityEngine.Object.FindFirstObjectByType<BattleBootstrapper>();
            }
        }

        private bool TryShouldShowCheonraJimangMark()
        {
            if (!_isOccupied || _tileView == null || _tileView.CurrentOccupant == null)
            {
                return false;
            }

            var runtimeId = _tileView.CurrentOccupantRuntimeId;
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return false;
            }

            AutoAssignBootstrapper();
            var battleState = _battleBootstrapper == null ? null : _battleBootstrapper.CurrentBattleState;
            if (battleState == null)
            {
                return false;
            }

            foreach (var persistentEffect in battleState.PersistentEffects)
            {
                if (persistentEffect == null || persistentEffect.IsExpired)
                {
                    continue;
                }

                if (!string.Equals(persistentEffect.EffectId, CheonraJimangEffectId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(persistentEffect.TargetRuntimeId, runtimeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Sprite GetCheonraJimangMarkSprite()
        {
            if (s_cheonraJimangMarkSprite != null)
            {
                return s_cheonraJimangMarkSprite;
            }

            s_cheonraJimangMarkSprite = Resources.Load<Sprite>(CheonraJimangMarkResourcePath);
            if (s_cheonraJimangMarkSprite != null)
            {
                return s_cheonraJimangMarkSprite;
            }

            var texture = Resources.Load<Texture2D>(CheonraJimangMarkResourcePath);
            if (texture == null)
            {
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            s_cheonraJimangMarkSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            s_cheonraJimangMarkSprite.name = "CheonraJimangMarkSprite";
            return s_cheonraJimangMarkSprite;
        }

        private void AutoAssignVisualReferences()
        {
            if (_occupantVisualRoot == null)
            {
                var existingTransform = transform.Find(AutoVisualName);
                if (existingTransform is RectTransform rectTransform)
                {
                    _occupantVisualRoot = rectTransform;
                }
            }

            if (_occupantVisualRoot == null && _text != null)
            {
                var existingTextTransform = _text.rectTransform.Find(AutoVisualName);
                if (existingTextTransform is RectTransform textRectTransform)
                {
                    _occupantVisualRoot = textRectTransform;
                }
            }

            if (_occupantVisualRoot == null && _text != null && _text.rectTransform.parent != null)
            {
                var existingCanvasTransform = _text.rectTransform.parent.Find(AutoVisualName);
                if (existingCanvasTransform is RectTransform canvasRectTransform)
                {
                    _occupantVisualRoot = canvasRectTransform;
                }
            }

            if (_occupantVisualRoot != null && _occupantVisualImage == null)
            {
                _occupantVisualImage = _occupantVisualRoot.GetComponent<Image>();
            }

            if (_occupantVisualRoot != null && _occupantVisualAnimator == null)
            {
                _occupantVisualAnimator = _occupantVisualRoot.GetComponent<Animator>();
            }

            if (_occupantVisualRoot != null && _occupantVisualCanvas == null)
            {
                _occupantVisualCanvas = _occupantVisualRoot.GetComponent<Canvas>();
            }

            if (_occupantVisualRoot != null && _occupantStatRoot == null)
            {
                var statRoot = _occupantVisualRoot.Find(AutoStatBarName);
                if (statRoot is RectTransform statRectTransform)
                {
                    _occupantStatRoot = statRectTransform;
                    _occupantStatBackgroundImage = statRectTransform.GetComponent<Image>();
                    _occupantStatHeartImage = statRectTransform.Find("HeartIcon")?.GetComponent<Image>();
                    _occupantStatHpText = statRectTransform.Find("HpText")?.GetComponent<TMP_Text>();
                    _occupantStatAttackTypeImage = statRectTransform.Find("AttackTypeIcon")?.GetComponent<Image>();
                    _occupantStatAttackText = statRectTransform.Find("AttackText")?.GetComponent<TMP_Text>();
                }
            }
        }

        private void EnsureRuntimeVisualObjects()
        {
            var visualParent = ResolveOccupantVisualParent();

            if (_occupantVisualRoot != null)
            {
                if (visualParent != null && _occupantVisualRoot.parent != visualParent)
                {
                    _occupantVisualRoot.SetParent(visualParent, false);
                }

                return;
            }

            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            var visualObject = new GameObject(AutoVisualName, typeof(RectTransform), typeof(Canvas), typeof(Image), typeof(Animator));
            visualObject.transform.SetParent(visualParent, false);

            _occupantVisualRoot = visualObject.GetComponent<RectTransform>();
            _occupantVisualCanvas = visualObject.GetComponent<Canvas>();
            _occupantVisualImage = visualObject.GetComponent<Image>();
            _occupantVisualAnimator = visualObject.GetComponent<Animator>();
        }

        private Transform ResolveOccupantVisualParent()
        {
            if (_text != null && _text.rectTransform.parent != null)
            {
                return _text.rectTransform.parent;
            }

            if (_text != null)
            {
                return _text.rectTransform;
            }

            return transform;
        }

        private RectTransform FindTileBackgroundRect()
        {
            if (_text == null || _text.rectTransform.parent == null)
            {
                return null;
            }

            var tileBackground = _text.rectTransform.parent.Find("TileBackground");
            return tileBackground as RectTransform;
        }

        private Image FindTileBackgroundImage()
        {
            var tileBackgroundRect = FindTileBackgroundRect();
            return tileBackgroundRect == null ? null : tileBackgroundRect.GetComponent<Image>();
        }

        private void EnsureRuntimeStatObjects()
        {
            if (_occupantStatRoot != null || !UnityEngine.Application.isPlaying || _occupantVisualRoot == null)
            {
                return;
            }

            var statBarObject = new GameObject(AutoStatBarName, typeof(RectTransform), typeof(Image));
            statBarObject.transform.SetParent(_occupantVisualRoot, false);

            _occupantStatRoot = statBarObject.GetComponent<RectTransform>();
            _occupantStatBackgroundImage = statBarObject.GetComponent<Image>();
            _occupantStatBackgroundImage.raycastTarget = false;

            _occupantStatHeartImage = CreateStatImage("HeartIcon", _occupantStatRoot);
            _occupantStatHpText = CreateStatText("HpText", _occupantStatRoot, TextAlignmentOptions.Left);
            _occupantStatAttackTypeImage = CreateStatImage("AttackTypeIcon", _occupantStatRoot);
            _occupantStatAttackText = CreateStatText("AttackText", _occupantStatRoot, TextAlignmentOptions.Left);
            ApplyStatBarLayout();
        }

        private void EnsureRuntimeSpellEffectObjects()
        {
            var effectParent = EnsureSpellEffectOverlayRoot();
            if (_spellEffectRoot != null)
            {
                if (effectParent != null && _spellEffectRoot.parent != effectParent)
                {
                    _spellEffectRoot.SetParent(effectParent, false);
                }

                ApplySpellEffectLayout(_spellEffectSize);
                return;
            }

            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            if (effectParent == null)
            {
                return;
            }

            var effectObject = new GameObject(AutoSpellEffectName, typeof(RectTransform), typeof(Canvas), typeof(Image));
            effectObject.transform.SetParent(effectParent, false);
            _spellEffectRoot = effectObject.GetComponent<RectTransform>();
            _spellEffectCanvas = effectObject.GetComponent<Canvas>();
            _spellEffectImage = effectObject.GetComponent<Image>();
            _spellEffectImage.raycastTarget = false;
            _spellEffectImage.preserveAspect = true;
            _spellEffectImage.color = Color.white;
            _spellEffectImage.enabled = false;
            ApplySpellEffectLayout(_spellEffectSize);
        }

        private void EnsureRuntimePersistentMarkObjects()
        {
            var markParent = ResolveOccupantVisualParent();
            if (_persistentMarkRoot != null)
            {
                if (markParent != null && _persistentMarkRoot.parent != markParent)
                {
                    _persistentMarkRoot.SetParent(markParent, false);
                }

                ApplyPersistentMarkLayout();
                return;
            }

            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            if (markParent == null)
            {
                return;
            }

            var markObject = new GameObject(AutoPersistentMarkName, typeof(RectTransform), typeof(Canvas), typeof(Image));
            markObject.transform.SetParent(markParent, false);
            _persistentMarkRoot = markObject.GetComponent<RectTransform>();
            _persistentMarkCanvas = markObject.GetComponent<Canvas>();
            _persistentMarkImage = markObject.GetComponent<Image>();
            _persistentMarkImage.raycastTarget = false;
            _persistentMarkImage.preserveAspect = true;
            _persistentMarkImage.color = Color.white;
            _persistentMarkImage.enabled = false;
            ApplyPersistentMarkLayout();
        }

        private IEnumerator PlayTransientSpriteEffectSequence(IReadOnlyList<Sprite> frames, float framesPerSecond, Vector2 size)
        {
            ApplySpellEffectLayout(size);
            if (_spellEffectRoot == null || _spellEffectImage == null)
            {
                yield break;
            }

            _spellEffectRoot.gameObject.SetActive(true);
            _spellEffectRoot.SetAsLastSibling();
            _spellEffectImage.enabled = true;

            var frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                _spellEffectImage.sprite = frames[frameIndex];
                yield return new WaitForSecondsRealtime(frameDuration);
            }

            _spellEffectImage.sprite = null;
            _spellEffectImage.enabled = false;
            _spellEffectRoot.gameObject.SetActive(false);
            _spellEffectCoroutine = null;
        }

        private IEnumerator PlayFloatingValuePopupSequence(int amount, bool isHealing, float delaySeconds, int stackIndex)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(delaySeconds);
            }

            var overlayRoot = EnsureValuePopupOverlayRoot();
            if (overlayRoot == null)
            {
                yield break;
            }

            var popupText = CreateFloatingValueText(overlayRoot, isHealing);
            if (popupText == null)
            {
                yield break;
            }

            var popupRect = popupText.rectTransform;
            var baseAnchoredPosition = ResolveValuePopupAnchoredPosition(stackIndex);
            var popupColor = isHealing ? _healingPopupColor : _damagePopupColor;
            popupText.text = amount.ToString();
            popupText.color = popupColor;
            popupRect.anchoredPosition = baseAnchoredPosition;
            popupRect.localScale = Vector3.one;

            var duration = Mathf.Max(0.05f, _valuePopupDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var easedProgress = 1f - Mathf.Pow(1f - progress, 2f);
                var currentColor = popupColor;
                currentColor.a = 1f - progress;
                popupText.color = currentColor;
                popupRect.anchoredPosition = baseAnchoredPosition + new Vector2(0f, _valuePopupRiseDistance * easedProgress);
                yield return null;
            }

            if (popupText != null)
            {
                Destroy(popupText.gameObject);
            }
        }

        private Transform EnsureSpellEffectOverlayRoot()
        {
            if (s_spellEffectOverlayRoot != null)
            {
                return s_spellEffectOverlayRoot;
            }

            var overlayCanvasTransform = ResolveOverlayCanvasTransform();
            if (overlayCanvasTransform == null)
            {
                return null;
            }

            var existingOverlayTransform = overlayCanvasTransform.Find(AutoSpellEffectOverlayName);
            if (existingOverlayTransform is RectTransform existingOverlayRoot)
            {
                s_spellEffectOverlayRoot = existingOverlayRoot;
            }

            if (s_spellEffectOverlayRoot == null)
            {
                var overlayObject = new GameObject(
                    AutoSpellEffectOverlayName,
                    typeof(RectTransform),
                    typeof(Canvas));
                overlayObject.transform.SetParent(overlayCanvasTransform, false);
                s_spellEffectOverlayRoot = overlayObject.GetComponent<RectTransform>();
            }

            var overlayCanvas = s_spellEffectOverlayRoot.GetComponent<Canvas>();
            if (overlayCanvas != null)
            {
                overlayCanvas.overrideSorting = true;
                overlayCanvas.sortingOrder = _spellEffectSortingOrder;
            }

            s_spellEffectOverlayRoot.anchorMin = Vector2.zero;
            s_spellEffectOverlayRoot.anchorMax = Vector2.one;
            s_spellEffectOverlayRoot.pivot = new Vector2(0.5f, 0.5f);
            s_spellEffectOverlayRoot.offsetMin = Vector2.zero;
            s_spellEffectOverlayRoot.offsetMax = Vector2.zero;
            s_spellEffectOverlayRoot.localScale = Vector3.one;
            s_spellEffectOverlayRoot.SetAsLastSibling();
            return s_spellEffectOverlayRoot;
        }

        private Transform EnsureValuePopupOverlayRoot()
        {
            if (s_valuePopupOverlayRoot != null)
            {
                return s_valuePopupOverlayRoot;
            }

            var overlayCanvasTransform = ResolveOverlayCanvasTransform();
            if (overlayCanvasTransform == null)
            {
                return null;
            }

            var existingOverlayTransform = overlayCanvasTransform.Find(AutoValuePopupOverlayName);
            if (existingOverlayTransform is RectTransform existingOverlayRoot)
            {
                s_valuePopupOverlayRoot = existingOverlayRoot;
            }

            if (s_valuePopupOverlayRoot == null)
            {
                var overlayObject = new GameObject(
                    AutoValuePopupOverlayName,
                    typeof(RectTransform),
                    typeof(Canvas));
                overlayObject.transform.SetParent(overlayCanvasTransform, false);
                s_valuePopupOverlayRoot = overlayObject.GetComponent<RectTransform>();
            }

            var overlayCanvas = s_valuePopupOverlayRoot.GetComponent<Canvas>();
            if (overlayCanvas != null)
            {
                overlayCanvas.overrideSorting = true;
                overlayCanvas.sortingOrder = _valuePopupSortingOrder;
            }

            s_valuePopupOverlayRoot.anchorMin = Vector2.zero;
            s_valuePopupOverlayRoot.anchorMax = Vector2.one;
            s_valuePopupOverlayRoot.pivot = new Vector2(0.5f, 0.5f);
            s_valuePopupOverlayRoot.offsetMin = Vector2.zero;
            s_valuePopupOverlayRoot.offsetMax = Vector2.zero;
            s_valuePopupOverlayRoot.localScale = Vector3.one;
            s_valuePopupOverlayRoot.SetAsLastSibling();
            return s_valuePopupOverlayRoot;
        }

        private TMP_Text CreateFloatingValueText(Transform parent, bool isHealing)
        {
            var textObject = new GameObject("ValuePopupText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var popupText = textObject.GetComponent<TextMeshProUGUI>();
            if (_text != null && _text.font != null)
            {
                popupText.font = _text.font;
            }

            popupText.fontSize = _valuePopupFontSize;
            popupText.alignment = TextAlignmentOptions.Center;
            popupText.enableWordWrapping = false;
            popupText.overflowMode = TextOverflowModes.Overflow;
            popupText.raycastTarget = false;
            popupText.text = "0";
            popupText.color = isHealing ? _healingPopupColor : _damagePopupColor;

            var popupRect = popupText.rectTransform;
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.sizeDelta = new Vector2(72f, 36f);
            popupRect.localScale = Vector3.one;
            return popupText;
        }

        private Vector2 ResolveValuePopupAnchoredPosition(int stackIndex)
        {
            var overlayRoot = s_valuePopupOverlayRoot;
            if (overlayRoot == null)
            {
                return _valuePopupOffset;
            }

            var worldPosition = _occupantVisualRoot != null
                ? _occupantVisualRoot.position
                : GetTileAnchorWorldPosition();
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayRoot,
                    screenPoint,
                    null,
                    out var localPoint))
            {
                return localPoint + _valuePopupOffset + new Vector2(0f, stackIndex * 16f);
            }

            return _valuePopupOffset + new Vector2(0f, stackIndex * 16f);
        }

        private void ApplyPersistentMarkLayout()
        {
            if (_persistentMarkRoot == null)
            {
                return;
            }

            if (_occupantVisualRoot != null)
            {
                _persistentMarkRoot.anchorMin = _occupantVisualRoot.anchorMin;
                _persistentMarkRoot.anchorMax = _occupantVisualRoot.anchorMax;
                _persistentMarkRoot.pivot = _occupantVisualRoot.pivot;
                _persistentMarkRoot.anchoredPosition = _occupantVisualRoot.anchoredPosition + _persistentMarkOffset;
                _persistentMarkRoot.localRotation = _occupantVisualRoot.localRotation;
            }
            else
            {
                var tileBackgroundRect = FindTileBackgroundRect();
                if (tileBackgroundRect != null)
                {
                    _persistentMarkRoot.anchorMin = tileBackgroundRect.anchorMin;
                    _persistentMarkRoot.anchorMax = tileBackgroundRect.anchorMax;
                    _persistentMarkRoot.pivot = tileBackgroundRect.pivot;
                    _persistentMarkRoot.anchoredPosition = tileBackgroundRect.anchoredPosition + _persistentMarkOffset;
                    _persistentMarkRoot.localRotation = tileBackgroundRect.localRotation;
                }
                else
                {
                    _persistentMarkRoot.anchorMin = new Vector2(0.5f, 0.5f);
                    _persistentMarkRoot.anchorMax = new Vector2(0.5f, 0.5f);
                    _persistentMarkRoot.pivot = new Vector2(0.5f, 0.5f);
                    _persistentMarkRoot.anchoredPosition = _persistentMarkOffset;
                    _persistentMarkRoot.localRotation = Quaternion.identity;
                }
            }

            _persistentMarkRoot.sizeDelta = _persistentMarkSize;
            _persistentMarkRoot.localScale = Vector3.one;
            _persistentMarkRoot.SetAsLastSibling();

            if (_persistentMarkCanvas == null)
            {
                _persistentMarkCanvas = _persistentMarkRoot.GetComponent<Canvas>();
            }

            if (_persistentMarkCanvas == null && UnityEngine.Application.isPlaying)
            {
                _persistentMarkCanvas = _persistentMarkRoot.gameObject.AddComponent<Canvas>();
            }

            if (_persistentMarkCanvas != null)
            {
                _persistentMarkCanvas.overrideSorting = true;
                _persistentMarkCanvas.sortingOrder = _persistentMarkSortingOrder;
            }
        }

        private Image CreateStatImage(string objectName, Transform parent)
        {
            var iconObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            var image = iconObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = _occupantStatIconColor;
            return image;
        }

        private TMP_Text CreateStatText(string objectName, Transform parent, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var runtimeText = textObject.GetComponent<TextMeshProUGUI>();
            if (_text != null && _text.font != null)
            {
                runtimeText.font = _text.font;
            }

            runtimeText.fontSize = _occupantStatFontSize;
            runtimeText.alignment = alignment;
            runtimeText.enableWordWrapping = false;
            runtimeText.overflowMode = TextOverflowModes.Overflow;
            runtimeText.raycastTarget = false;
            runtimeText.color = _occupantStatTextColor;
            return runtimeText;
        }

        private void CacheBaseScale()
        {
            if (_text != null)
            {
                _baseScale = _text.rectTransform.localScale;
            }
        }

        private void ApplyVisualLayout()
        {
            if (_occupantVisualRoot == null)
            {
                return;
            }

            EnsureOccupantVisualCanvas();
            var facingScaleX = GetOccupantFacingScaleX();
            var resolvedVisualOffset = _occupantVisualFineOffset + DefaultOccupantVisualLiftOffset;
            var tileBackgroundRect = FindTileBackgroundRect();
            if (tileBackgroundRect != null)
            {
                _occupantVisualRoot.anchorMin = tileBackgroundRect.anchorMin;
                _occupantVisualRoot.anchorMax = tileBackgroundRect.anchorMax;
                _occupantVisualRoot.pivot = tileBackgroundRect.pivot;
                _occupantVisualRoot.anchoredPosition = tileBackgroundRect.anchoredPosition + resolvedVisualOffset;
                _occupantVisualRoot.localRotation = tileBackgroundRect.localRotation;
            }
            else
            {
                _occupantVisualRoot.anchorMin = new Vector2(0.5f, 0.5f);
                _occupantVisualRoot.anchorMax = new Vector2(0.5f, 0.5f);
                _occupantVisualRoot.pivot = new Vector2(0.5f, 0.5f);
                _occupantVisualRoot.anchoredPosition = _occupantVisualAnchoredPosition + resolvedVisualOffset;
                _occupantVisualRoot.localRotation = Quaternion.identity;
            }

            _occupantVisualRoot.sizeDelta = _occupantVisualSize * Mathf.Max(0.1f, _occupantVisualScaleMultiplier);
            _occupantVisualRoot.localScale = new Vector3(facingScaleX, 1f, 1f);
            _occupantVisualRoot.SetAsFirstSibling();

            if (_occupantVisualImage != null)
            {
                _occupantVisualImage.color = Color.white;
                _occupantVisualImage.preserveAspect = true;
                _occupantVisualImage.raycastTarget = false;
            }
        }

        private void EnsureOccupantVisualCanvas()
        {
            if (_occupantVisualRoot == null)
            {
                return;
            }

            if (_occupantVisualCanvas == null)
            {
                _occupantVisualCanvas = _occupantVisualRoot.GetComponent<Canvas>();
            }

            if (_occupantVisualCanvas == null && UnityEngine.Application.isPlaying)
            {
                _occupantVisualCanvas = _occupantVisualRoot.gameObject.AddComponent<Canvas>();
            }

            if (_occupantVisualCanvas == null)
            {
                return;
            }

            _occupantVisualCanvas.overrideSorting = _overrideOccupantVisualSorting;
            _occupantVisualCanvas.sortingOrder = _occupantVisualSortingOrder;
        }

        private void ApplySpellEffectLayout(Vector2 size)
        {
            if (_spellEffectRoot == null)
            {
                return;
            }

            _spellEffectRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _spellEffectRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _spellEffectRoot.pivot = new Vector2(0.5f, 0.5f);
            _spellEffectRoot.localRotation = Quaternion.identity;
            _spellEffectRoot.localScale = Vector3.one;

            if (s_spellEffectOverlayRoot != null)
            {
                var worldPosition = _occupantVisualRoot != null
                    ? _occupantVisualRoot.position
                    : GetTileAnchorWorldPosition();
                var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        s_spellEffectOverlayRoot,
                        screenPoint,
                        null,
                        out var localPoint))
                {
                    _spellEffectRoot.anchoredPosition = localPoint + _spellEffectOffset;
                }
                else
                {
                    _spellEffectRoot.anchoredPosition = _spellEffectOffset;
                }
            }
            else
            {
                _spellEffectRoot.anchoredPosition = _spellEffectOffset;
            }

            _spellEffectRoot.sizeDelta = size == Vector2.zero ? _spellEffectSize : size;
            _spellEffectRoot.gameObject.SetActive(false);

            if (_spellEffectCanvas == null)
            {
                _spellEffectCanvas = _spellEffectRoot.GetComponent<Canvas>();
            }

            if (_spellEffectCanvas == null && UnityEngine.Application.isPlaying)
            {
                _spellEffectCanvas = _spellEffectRoot.gameObject.AddComponent<Canvas>();
            }

            if (_spellEffectCanvas != null)
            {
                _spellEffectCanvas.overrideSorting = true;
                _spellEffectCanvas.sortingOrder = _spellEffectSortingOrder;
            }
        }

        private void ApplyStatBarLayout()
        {
            if (_occupantStatRoot == null)
            {
                return;
            }

            var facingScaleX = GetOccupantFacingScaleX();
            _occupantStatRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _occupantStatRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _occupantStatRoot.pivot = new Vector2(0.5f, 0.5f);
            _occupantStatRoot.anchoredPosition = new Vector2(
                _occupantStatBarOffset.x,
                _occupantStatBarOffset.y * Mathf.Max(1f, _occupantVisualScaleMultiplier));
            _occupantStatRoot.sizeDelta = _occupantStatBarSize;
            _occupantStatRoot.localScale = new Vector3(facingScaleX, 1f, 1f);

            if (_occupantStatBackgroundImage != null)
            {
                _occupantStatBackgroundImage.color = _occupantStatBarColor;
                _occupantStatBackgroundImage.raycastTarget = false;
            }

            var halfWidth = _occupantStatBarSize.x * 0.5f;
            ApplyStatIconLayout(_occupantStatHeartImage, new Vector2(-halfWidth + 12f, 0f));
            ApplyStatTextLayout(_occupantStatHpText, new Vector2(-halfWidth + 34f, 0f), new Vector2(34f, 22f));
            ApplyStatIconLayout(_occupantStatAttackTypeImage, new Vector2(8f, 0f));
            ApplyStatTextLayout(_occupantStatAttackText, new Vector2(30f, 0f), new Vector2(34f, 22f));
        }

        private void ApplyStatIconLayout(Image image, Vector2 anchoredPosition)
        {
            if (image == null)
            {
                return;
            }

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = _occupantStatIconSize;
            rect.localScale = Vector3.one;
        }

        private void ApplyStatTextLayout(TMP_Text textComponent, Vector2 anchoredPosition, Vector2 size)
        {
            if (textComponent == null)
            {
                return;
            }

            var rect = textComponent.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            textComponent.fontSize = _occupantStatFontSize;
        }

        private float GetOccupantFacingScaleX()
        {
            if (_tileView?.CurrentOccupant?.OwnerId == PlayerId.AI)
            {
                return -1f;
            }

            return 1f;
        }

        private void ApplyPulse()
        {
            if (_text == null)
            {
                return;
            }

            var shouldPulse = _lastHighlightState != BattleHighlightState.None;
            var targetScale = _baseScale;

            if (shouldPulse)
            {
                var pulse = 1f + (Mathf.Sin(Time.unscaledTime * _pulseSpeed) * (_pulseScale - 1f));
                targetScale = _baseScale * pulse;
            }

            _text.rectTransform.localScale = Vector3.Lerp(
                _text.rectTransform.localScale,
                targetScale,
                Time.unscaledDeltaTime * 12f);
        }

        private Color ResolveColor(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _selectedColor,
                BattleHighlightState.Playable => _playableColor,
                BattleHighlightState.MoveTarget => _moveTargetColor,
                BattleHighlightState.SwapTarget => _swapTargetColor,
                BattleHighlightState.AttackTarget => _attackTargetColor,
                BattleHighlightState.SpellTarget => _spellTargetColor,
                BattleHighlightState.DragHoverTarget => _attackTargetColor,
                _ => _normalColor,
            };
        }

        private void RefreshTileBackgroundHighlight()
        {
            var tileBackgroundImage = FindTileBackgroundImage();
            if (tileBackgroundImage == null)
            {
                return;
            }

            tileBackgroundImage.color = _lastHighlightState switch
            {
                BattleHighlightState.Selected => _tileBackgroundSelectedTint,
                BattleHighlightState.DragHoverTarget => _tileBackgroundDragHoverTint,
                _ => _tileBackgroundNormalColor,
            };
        }

        private static Sprite GetHeartIconSprite()
        {
            if (s_heartIconSprite == null)
            {
                s_heartIconSprite = CreateIconSprite(
                    "TileHeartIcon",
                    new[]
                    {
                        ".XX...XX.",
                        "XXXX.XXXX",
                        "XXXXXXXXX",
                        "XXXXXXXXX",
                        ".XXXXXXX.",
                        "..XXXXX..",
                        "...XXX...",
                        "....X....",
                    },
                    new Color(0.95f, 0.24f, 0.3f));
            }

            return s_heartIconSprite;
        }

        private static Sprite GetSwordIconSprite()
        {
            if (s_swordIconSprite == null)
            {
                s_swordIconSprite = CreateIconSprite(
                    "TileSwordIcon",
                    new[]
                    {
                        "....X....",
                        "...XXX...",
                        "....X....",
                        "....X....",
                        "....X....",
                        "..XXXXX..",
                        "...XXX...",
                        "...X.X...",
                    },
                    new Color(0.9f, 0.9f, 0.96f));
            }

            return s_swordIconSprite;
        }

        private static Sprite GetBowIconSprite()
        {
            if (s_bowIconSprite == null)
            {
                s_bowIconSprite = CreateIconSprite(
                    "TileBowIcon",
                    new[]
                    {
                        "..X..X...",
                        ".X....X..",
                        "X......XX",
                        "X......XX",
                        ".X....X..",
                        "..X..X...",
                        "....X....",
                        "XXXXXXXXX",
                    },
                    new Color(0.96f, 0.83f, 0.46f));
            }

            return s_bowIconSprite;
        }

        private static Sprite GetFallbackOccupantSprite(OccupantState occupant)
        {
            if (occupant == null)
            {
                return null;
            }

            if (occupant.Kind == OccupantKind.Master)
            {
                if (s_masterIconSprite == null)
                {
                    s_masterIconSprite = CreateIconSprite(
                        "TileMasterIcon",
                        new[]
                        {
                            "..X.X.X..",
                            "..XXXXX..",
                            ".XXXXXXX.",
                            ".XX.X.XX.",
                            ".XXXXXXX.",
                            "..XXXXX..",
                            "..X...X..",
                            ".XXXXXXX.",
                            ".XXXXXXX.",
                        },
                        new Color(0.92f, 0.82f, 0.28f));
                }

                return s_masterIconSprite;
            }

            if (s_fallbackOccupantSprite == null)
            {
                s_fallbackOccupantSprite = CreateIconSprite(
                    "TileFallbackOccupantIcon",
                    new[]
                    {
                        "...XXX...",
                        "..XXXXX..",
                        "..XXXXX..",
                        "...XXX...",
                        "..X.X.X..",
                        ".XX.X.XX.",
                        ".XX...XX.",
                        ".XXXXXXX.",
                        "..XXXXX..",
                    },
                    new Color(0.72f, 0.76f, 0.9f));
            }

            return s_fallbackOccupantSprite;
        }

        private static Sprite CreateIconSprite(string name, IReadOnlyList<string> rows, Color color)
        {
            var height = rows.Count;
            var width = rows[0].Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            var clear = new Color(0f, 0f, 0f, 0f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var row = rows[y];
                    var pixelColor = row[x] == 'X' ? color : clear;
                    texture.SetPixel(x, height - y - 1, pixelColor);
                }
            }

            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = name;
            return sprite;
        }

        private sealed class SpriteAnimationSequence
        {
            public SpriteAnimationSequence(IReadOnlyList<Sprite> frames, float framesPerSecond, bool isLooping)
            {
                Frames = frames;
                FramesPerSecond = framesPerSecond;
                IsLooping = isLooping;
            }

            public IReadOnlyList<Sprite> Frames { get; }
            public float FramesPerSecond { get; }
            public bool IsLooping { get; }
        }
    }
}
