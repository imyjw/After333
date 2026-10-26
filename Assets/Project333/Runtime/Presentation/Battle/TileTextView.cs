using Project333.Runtime.Presentation.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Hand;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class TileTextView : MonoBehaviour
    {
        private TileTargetPulse _cardTargetPulse;
        private TrapStatusMarker _trapStatusMarker;

        private const string AutoVisualName = "OccupantVisual";
        private const string AutoStatBarName = "OccupantStatBar";
        private const string AutoHuanShuIconName = "HuanShuStatusIcon";
        private const string AutoSpellEffectName = "SpellEffectVisual";
        private const string AutoSpellEffectOverlayName = "SpellEffectOverlay";
        private const string AutoValuePopupOverlayName = "ValuePopupOverlay";
        private const string AutoPersistentMarkName = "PersistentMarkVisual";
        private const string AutoPreviewOverlayName = "CardPreviewOverlay";
        private const string AutoPreviewBackdropName = "PreviewBackdrop";
        private const string AutoPreviewImageName = "PreviewCardImage";
        private const string AutoPreviewTooltipRootName = "PreviewSpecialEffectTooltips";
        private const string AutoPreviewTooltipPanelPrefix = "SpecialEffectTooltip";
        private const string AutoPreviewTooltipTitleName = "Title";
        private const string AutoPreviewTooltipDescriptionName = "Description";
        private const string AutoInteractionSurfaceName = "TileInteractionSurface";
        private const string CheonraJimangEffectId = "cheonra_jimang";
        private const string CheonraJimangMarkResourcePath = "Project333/SpellEffects/CheonraJimangMark";
        private static readonly Vector2 DefaultOccupantVisualLiftOffset = new Vector2(0f, 60f);
        private static readonly Vector2 DefaultOccupantVisualReferenceTileSize = new Vector2(80f, 80f);
        private static readonly Color PhysicalMeleeAttackIconColor = new Color(0.96f, 0.22f, 0.18f, 1f);
        private static readonly Color MagicAttackIconColor = new Color(0.22f, 0.62f, 1f, 1f);
        private static readonly Color PhysicalRangedAttackIconColor = new Color(0.96f, 0.83f, 0.46f, 1f);
        private static readonly Color NeutralAttackIconColor = new Color(0.9f, 0.9f, 0.96f, 1f);

        private static RectTransform s_previewOverlayRoot;
        private static Image s_previewBackdropImage;
        private static Image s_previewCardImage;
        private static TMP_Text s_previewAttackText;
        private static TMP_Text s_previewHpText;
        private static TileTextView s_previewOwner;
        private static RectTransform s_previewTooltipRoot;
        private static readonly List<PreviewTooltipPanel> PreviewTooltipPanels =
            new List<PreviewTooltipPanel>();
        private static readonly List<PreviewTooltipPanel> PreviewAttachedPanels =
            new List<PreviewTooltipPanel>();
        private static readonly List<PreviewTooltipPanel> PreviewInfluencePanels =
            new List<PreviewTooltipPanel>();
        private static RectTransform s_spellEffectOverlayRoot;
        private static RectTransform s_valuePopupOverlayRoot;
        private static Sprite s_cheonraJimangMarkSprite;
        private static Sprite s_heartIconSprite;
        private static Sprite s_swordIconSprite;
        private static Sprite s_bowIconSprite;
        private static Sprite s_magicStaffIconSprite;
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
        [SerializeField] private SpriteRenderer _occupantVisualSpriteRenderer;
        [SerializeField] private bool _showOccupantVisual = true;
        [SerializeField] private Vector2 _occupantVisualSize = new Vector2(96f, 96f);
        [SerializeField] private Vector2 _occupantVisualAnchoredPosition = Vector2.zero;
        [SerializeField] private Vector2 _occupantVisualFineOffset = Vector2.zero;
        [SerializeField] private float _occupantVisualScaleMultiplier = 1.5f;
        [Tooltip("Tile size at which Occupant Visual Size is used without additional responsive scaling.")]
        [SerializeField] private Vector2 _occupantVisualReferenceTileSize = new Vector2(80f, 80f);
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
        [FormerlySerializedAs("_occupantStatDisabledBarColor")]
        [SerializeField] private Color _occupantStatErasureBarColor = new Color(0.18f, 0.18f, 0.2f, 0.88f);
        [FormerlySerializedAs("_occupantStatDisabledTextColor")]
        [SerializeField] private Color _occupantStatErasureTextColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        [FormerlySerializedAs("_occupantStatDisabledIconColor")]
        [SerializeField] private Color _occupantStatErasureIconColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        [Header("HuanShu Status Icon")]
        [SerializeField] private bool _showHuanShuStatusIcon = true;
        [SerializeField] private Vector2 _huanShuStatusIconSize = new Vector2(22f, 22f);
        [SerializeField] private Vector2 _huanShuStatusIconOffset = new Vector2(52f, 22f);
        [SerializeField] private Color _huanShuStatusIconColor = new Color(0.45f, 0.12f, 0.72f, 0.96f);
        [SerializeField] private Color _huanShuStatusGlyphColor = new Color(0.9f, 0.72f, 1f, 1f);
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
        [Header("Card Preview Stats")]
        [SerializeField] private TMP_FontAsset _cardPreviewStatFont;
        [SerializeField] private Vector2 _cardPreviewAttackNormalizedPosition = HandCardStatOverlayLayout.DefaultAttackPosition;
        [SerializeField] private Vector2 _cardPreviewHpNormalizedPosition = HandCardStatOverlayLayout.DefaultHpPosition;
        [SerializeField] private Vector2 _cardPreviewStatTextSize = new Vector2(96f, 72f);
        [SerializeField] private int _cardPreviewStatFontSize = 44;
        [SerializeField] private Color _cardPreviewStatTextColor = Color.white;
        [Header("Card Preview Special Effect Tooltips")]
        [SerializeField] private TMP_FontAsset _cardPreviewTooltipFont;
        [SerializeField] private Vector2 _cardPreviewTooltipSize = new Vector2(340f, 116f);
        [SerializeField] private float _cardPreviewTooltipSideGap = 28f;
        [SerializeField] private float _cardPreviewTooltipVerticalGap = 12f;
        [SerializeField] private float _cardPreviewTooltipEdgePadding = 24f;
        [SerializeField] private int _cardPreviewTooltipTitleFontSize = 24;
        [SerializeField] private int _cardPreviewTooltipDescriptionFontSize = 17;
        [SerializeField] private Color _cardPreviewTooltipPanelColor = new Color(0.055f, 0.07f, 0.085f, 0.96f);
        [SerializeField] private Color _cardPreviewTooltipBorderColor = new Color(0.88f, 0.7f, 0.32f, 0.95f);
        [SerializeField] private Color _cardPreviewTooltipTitleColor = new Color(1f, 0.88f, 0.5f, 1f);
        [SerializeField] private Color _cardPreviewTooltipDescriptionColor = new Color(0.95f, 0.96f, 1f, 1f);
        [Header("Animation States")]
        [SerializeField] private string _idleStateName = "Idle";
        [SerializeField] private string _drainedStateName = "Drained";
        [SerializeField] private string _runStateName = "Run";
        [SerializeField] private string _attackStateName = "Attack";
        [SerializeField] private string _beAttackedStateName = "BeAttacked";
        [SerializeField] private string _deathStateName = "Death";

        private string _title = string.Empty;
        private string _content = "Empty";
        private bool _isOccupied;
        [NonSerialized] private string _presentedHpRuntimeId;
        [NonSerialized] private int _presentedHp;
        private BattleHighlightState _lastHighlightState = BattleHighlightState.None;
        private string _lastRenderedText = string.Empty;
        private Vector3 _baseScale = Vector3.one;
        private string _lastVisualCardId = string.Empty;
        private string _lastVisualRuntimeId = string.Empty;
        private Sprite _lastVisualSprite;
        private RuntimeAnimatorController _lastVisualController;
        private string _lastVisualControllerName = string.Empty;
        private bool _lastVisualWasDrained;
        private bool _blueDragonUsesScaledMotion;
        private bool _isVisualActive;
        private bool _isDeathPresentation;
        private DrainedStatusVfx _drainedStatusVfx;
        private ActionDebuffStatusVfx _actionDebuffStatusVfx;
        private Animator _frozenBodyAnimator;
        private RuntimeAnimatorController _frozenBodyController;
        private string _frozenBodyRuntimeId;
        private float _frozenBodyPreviousSpeed;
        private bool _isFrozenBodyPaused;
        private bool _isBindingVisual;
        private Canvas _occupantVisualCanvas;
        private SpriteAnimationSequence _activeSpriteSequence;
        private int _activeSequenceFrameIndex;
        private float _activeSequenceFrameTimer;
        private GameObject _sampleAnimationObject;
        private SpriteRenderer _sampleSpriteRenderer;
        private RectTransform _occupantStatRoot;
        private Canvas _occupantStatCanvas;
        private Image _occupantStatBackgroundImage;
        private Image _occupantStatHeartImage;
        private TMP_Text _occupantStatHpText;
        private Image _occupantStatAttackTypeImage;
        private TMP_Text _occupantStatAttackText;
        private TMP_FontAsset _occupantStatFontOverride;
        private RectTransform _huanShuStatusIconRoot;
        private Image _huanShuStatusIconBackground;
        private Image _huanShuStatusIconGlyph;
        private Image _huanShuStatusIconCore;
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
        private string _previewOccupantRuntimeId;
        private Vector2 _previewLayoutSize;
        private IReadOnlyList<OccupantSpecialEffectTooltip> _previewAbilityEntries =
            Array.Empty<OccupantSpecialEffectTooltip>();
        private IReadOnlyList<OccupantSpecialEffectTooltip> _previewAttachedEntries =
            Array.Empty<OccupantSpecialEffectTooltip>();
        private IReadOnlyList<OccupantSpecialEffectTooltip> _previewInfluenceEntries =
            Array.Empty<OccupantSpecialEffectTooltip>();
        private OccupantState _previewBoundOccupant;
        private BattleState _previewBoundBattle;
        private float _nextPreviewTooltipRefreshTime;

        public float LongPressPreviewHoldSeconds => Mathf.Max(0.05f, _longPressPreviewHoldSeconds);

        public void ConfigureOccupantStatFont(TMP_FontAsset fontAsset)
        {
            _occupantStatFontOverride = fontAsset;
            ApplyOccupantStatFont(_occupantStatHpText);
            ApplyOccupantStatFont(_occupantStatAttackText);
        }

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
            if (_occupantVisualReferenceTileSize.x <= 0f || _occupantVisualReferenceTileSize.y <= 0f)
            {
                _occupantVisualReferenceTileSize = DefaultOccupantVisualReferenceTileSize;
            }

            AutoAssignView();
            AutoAssignBootstrapper();
            AutoAssignVisualReferences();
            ApplyVisualLayout();
            ApplyStatBarLayout();
            ApplyInteractionSurfaceLayout();
        }

        public RectTransform ActiveEffectButtonAnchor
        {
            get
            {
                if (_text == null) return null;
                return _text.rectTransform.parent?.Find("TileBackground") as RectTransform ?? _text.rectTransform;
            }
        }

        private void OnEnable() => RefreshFrozenBodyPause();

        // Pause before Unity evaluates native Animator motion; SetTile/Refresh also pause immediately.
        private void Update() => RefreshFrozenBodyPause();

        private void LateUpdate()
        {
            Refresh();
            EnsureInteractionSurface();
            AdvanceSampledAnimation();
            SyncAnimatedVisualSprite();
            ApplyPulse();
            RefreshCurrentCardPreview();
            RefreshDrainedStatusVfx();
            RefreshActionDebuffStatusVfx();
        }

        private void OnDestroy()
        {
            ReleaseFrozenBodyPause();
            if (_actionDebuffStatusVfx != null)
            {
                _actionDebuffStatusVfx.Cancel();
                if (UnityEngine.Application.isPlaying) Destroy(_actionDebuffStatusVfx);
                else DestroyImmediate(_actionDebuffStatusVfx);
                _actionDebuffStatusVfx = null;
            }
            if (_drainedStatusVfx != null)
            {
                _drainedStatusVfx.Cancel();
                if (UnityEngine.Application.isPlaying) Destroy(_drainedStatusVfx);
                else DestroyImmediate(_drainedStatusVfx);
                _drainedStatusVfx = null;
            }

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

            if (_cardTargetPulse != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(_cardTargetPulse.gameObject);
                else DestroyImmediate(_cardTargetPulse.gameObject);
            }
            HideCurrentCardPreview();
        }

        public void SetTile(string title, string content, bool isOccupied)
        {
            ClearPresentedHp();
            _title = title ?? string.Empty;
            _content = content ?? string.Empty;
            _isOccupied = isOccupied;
            Refresh();
        }

        // A presentation hold never changes the occupant or survives a replacement/authoritative refresh.
        internal void SetPresentedHp(string runtimeId, int hp)
        {
            if (string.IsNullOrWhiteSpace(runtimeId) || _tileView == null ||
                !string.Equals(runtimeId, VisualRuntimeId, StringComparison.Ordinal) ||
                !string.Equals(runtimeId, _tileView.CurrentOccupantRuntimeId, StringComparison.Ordinal)) return;
            _presentedHpRuntimeId = runtimeId;
            _presentedHp = Mathf.Max(0, hp);
            RefreshOccupantStats();
        }

        private void ClearPresentedHp()
        {
            _presentedHpRuntimeId = null;
            _presentedHp = 0;
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

        public float GetResponsiveTileScale()
        {
            var tileBackgroundRect = FindTileBackgroundRect();
            return tileBackgroundRect == null
                ? 1f
                : BoardResponsiveLayoutCalculator.CalculateUniformTileScale(
                    tileBackgroundRect.rect.size,
                    _occupantVisualReferenceTileSize);
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
                var scaledOffset = localOffset * GetResponsiveTileScale();
                return tileBackgroundRect.TransformPoint(scaledOffset);
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

        public void ApplyResponsiveTileLayout(Vector2 anchoredPosition, Vector2 tileSize)
        {
            AutoAssignView();
            AutoAssignVisualReferences();

            var resolvedSize = new Vector2(
                Mathf.Max(1f, tileSize.x),
                Mathf.Max(1f, tileSize.y));
            var tileBackgroundRect = FindTileBackgroundRect();
            ApplyCenteredRectLayout(tileBackgroundRect, anchoredPosition, resolvedSize);

            if (_text != null)
            {
                ApplyCenteredRectLayout(_text.rectTransform, anchoredPosition, resolvedSize);
            }

            ApplyInteractionSurfaceLayout();
            ApplyVisualLayout();
            ApplyStatBarLayout();
            ApplyPersistentMarkLayout();
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

        public string VisualRuntimeId => _lastVisualRuntimeId;
        internal Image OccupantVisualImage => _occupantVisualImage;
        internal string VisualControllerName => _lastVisualControllerName;
        internal bool IsDeathPresentation => _isDeathPresentation;

        internal int PresentedHitsPerAttack => _tileView?.CurrentOccupant?.EffectiveHitsPerAttack ?? 1;

        internal Canvas GetVisualRootCanvas()
        {
            // Battle tiles own a child Canvas; the TileTextView itself has no Canvas ancestor.
            var canvas = _occupantVisualImage != null ? _occupantVisualImage.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null && _text != null) canvas = _text.GetComponentInParent<Canvas>(true);
            if (canvas == null) canvas = GetComponentInParent<Canvas>(true);
            return canvas == null ? null : (canvas.rootCanvas != null ? canvas.rootCanvas : canvas);
        }

        public void ResetCombatPresentation()
        {
            var hadPresentedHp = _presentedHpRuntimeId != null;
            ClearPresentedHp();
            if (hadPresentedHp) RefreshOccupantStats();
            RestoreVisualLayout();
            ApplyPassiveAnimationState(_tileView == null ? null : _tileView.CurrentOccupant);
            StopCharacterSound();
        }

        public Vector3 GetVisualNormalizedWorldPosition(Vector2 point)
        {
            if (_occupantVisualRoot == null) return GetVisualWorldPosition();
            var rect = _occupantVisualRoot.rect;
            var side = Mathf.Min(rect.width, rect.height);
            return _occupantVisualRoot.TransformPoint(rect.center + (point - Vector2.one * 0.5f) * side);
        }

        // Shared arrival/impact anchor for all character VFX. Offset is in reference-tile
        // UI units, so both board scaling and Canvas transforms preserve the lower hit point.
        public Vector3 GetVisualImpactWorldPosition()
        {
            var bodyPosition = GetVisualNormalizedWorldPosition(new Vector2(0.5f,
                ((_lastVisualCardId == BlueDragonPresentationAssets.CardId && _blueDragonUsesScaledMotion) || _lastVisualCardId == GwangmaPresentationAssets.CardId) ? 0.4f : 0.25f));
            return bodyPosition + GetTileAnchorWorldPosition(new Vector2(0f, -10f))
                - GetTileAnchorWorldPosition();
        }

        private AudioSource _characterAudio;
        private static AudioClip _commonUnitHitSound;
        private static AudioSource _commonHitSettings;
        private static bool _commonHitSettingsLoaded;
        private static AudioSource _lastCommonHitSource;
        private static int _lastCommonHitFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCommonHitPlayback()
        {
            _commonUnitHitSound = null;
            _commonHitSettings = null;
            _commonHitSettingsLoaded = false;
            _lastCommonHitSource = null;
            _lastCommonHitFrame = -1;
        }

        private void OnDisable()
        {
            ClearPresentedHp();
            StopCharacterSound();
            _drainedStatusVfx?.Cancel();
            _actionDebuffStatusVfx?.Cancel();
            ReleaseFrozenBodyPause();
            if (_cardTargetPulse != null) _cardTargetPulse.GetComponent<Image>().enabled = false;
        }

        private void StopCharacterSound()
        {
            if (_characterAudio != null) _characterAudio.Stop();
            if (_lastCommonHitSource == _characterAudio)
            {
                _lastCommonHitSource = null;
                _lastCommonHitFrame = -1;
            }
        }
        private void PlayGwangmaSound(string name, float volume)
        {
            if (_lastVisualCardId != GwangmaPresentationAssets.CardId || !UnityEngine.Application.isPlaying) return;
            PlayCharacterSound(GwangmaPresentationAssets.GetSound(name), volume);
        }

        private void PlayCharacterSound(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (_characterAudio == null)
            {
                _characterAudio = gameObject.AddComponent<AudioSource>();
                _characterAudio.playOnAwake = false;
                _characterAudio.spatialBlend = 0f;
            }
            After333SoundEffects.PlayOneShot(_characterAudio, clip, volume);
        }

        public void PlayIdleAnimation()
        {
            PlayAnimationState(_idleStateName);
        }

        public void PlayDrainedAnimation()
        {
            PlayAnimationState(_drainedStateName);
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
            PlayDamageSound();
        }

        internal void PlayDamageReactionAnimation()
        {
            // Timed multi-hit presentation has already played the shared hit cue.
            PlayAnimationState(_beAttackedStateName);
        }

        public void PlayDamageSound()
        {
            // Every damaged occupant, including the master, uses the same impact sound.
            // Death follows this reaction and must not trigger another hit sound.
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled) return;
            // AoE reactions in one frame share one audible cue instead of summing
            // identical waveforms. A hit on the next frame remains a separate cue.
            if (_lastCommonHitFrame == Time.frameCount && _lastCommonHitSource != null &&
                _lastCommonHitSource.isActiveAndEnabled) return;
            // Read the prefab as settings only; never instantiate an extra sound emitter.
            if (!_commonHitSettingsLoaded)
            {
                var settings = Resources.Load<GameObject>("Project333/Audio/Common/HitSoundSettings");
                _commonHitSettings = settings == null ? null : settings.GetComponent<AudioSource>();
                _commonHitSettingsLoaded = true;
            }
            if (_commonHitSettings == null && _commonUnitHitSound == null)
                _commonUnitHitSound = Resources.Load<AudioClip>("Project333/Audio/Common/UnitHit");
            var clip = _commonHitSettings != null ? _commonHitSettings.clip : _commonUnitHitSound;
            if (clip == null) return;
            PlayCharacterSound(clip, _commonHitSettings != null ? _commonHitSettings.volume : 0.52f);
            _lastCommonHitSource = _characterAudio;
            _lastCommonHitFrame = Time.frameCount;
        }

        public void PlayDeathAnimation()
        {
            PlayAnimationState(_deathStateName);
            PlayGwangmaSound("GwangmaDeath", 0.48f);
        }

        public float GetIdleAnimationDurationSeconds()
        {
            return GetAnimationDurationSeconds(_idleStateName);
        }

        public float GetDrainedAnimationDurationSeconds()
        {
            return GetAnimationDurationSeconds(_drainedStateName);
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

            RestoreVisualLayout();
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

        public void QueueInvinciblePopup(float delaySeconds = 0f, int stackIndex = 0)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            StartCoroutine(PlayFloatingPopupSequence(
                "무적",
                new Color(1f, 0.82f, 0.2f, 1f),
                false,
                delaySeconds,
                stackIndex));
        }

        public static Image VisibleCardPreviewArtwork => s_previewOverlayRoot != null && s_previewOverlayRoot.gameObject.activeInHierarchy ? s_previewCardImage : null;
        public static RectTransform VisibleCardPreviewTooltip(bool influence)
        {
            if (VisibleCardPreviewArtwork == null) return null;
            var panels = influence ? PreviewInfluencePanels : PreviewTooltipPanels;
            foreach (var panel in panels) if (panel.Root != null && panel.Root.gameObject.activeInHierarchy) return panel.Root;
            return null;
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
            s_previewOwner = this;
            _previewOccupantRuntimeId = _tileView.CurrentOccupantRuntimeId;
            ShowCurrentSpecialEffectTooltips();
            s_previewOverlayRoot.SetAsLastSibling();
            s_previewOverlayRoot.gameObject.SetActive(true);
            RefreshPreviewStats();
            return true;
        }

        public void HideCurrentCardPreview()
        {
            // Each tile shares one overlay. Releasing another tile must not close it.
            if (s_previewOwner != null && s_previewOwner != this)
            {
                return;
            }

            s_previewOwner = null;
            if (s_previewAttackText != null)
            {
                s_previewAttackText.text = string.Empty;
                s_previewAttackText.gameObject.SetActive(false);
            }

            if (s_previewHpText != null)
            {
                s_previewHpText.text = string.Empty;
                s_previewHpText.gameObject.SetActive(false);
            }

            if (s_previewCardImage != null)
            {
                s_previewCardImage.sprite = null;
                s_previewCardImage.enabled = false;
            }

            HideSpecialEffectTooltips();

            if (s_previewOverlayRoot != null)
            {
                s_previewOverlayRoot.gameObject.SetActive(false);
            }
        }

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            RefreshOccupantVisual();
            RefreshFrozenBodyPause();
            RefreshPersistentMark();
            RefreshOccupantStats();
            RefreshTrapStatusMarker();
            RefreshText();
            RefreshTileBackgroundHighlight();
            RefreshDrainedStatusVfx();
            RefreshActionDebuffStatusVfx();
        }

        private void RefreshDrainedStatusVfx()
        {
            var occupant = _tileView == null ? null : _tileView.CurrentOccupant;
            if (!isActiveAndEnabled)
            {
                _drainedStatusVfx?.Cancel();
                return;
            }
            if (_drainedStatusVfx == null)
            {
                if (!UnityEngine.Application.isPlaying || !_isVisualActive || _isDeathPresentation ||
                    occupant == null || !occupant.IsDrained || !occupant.IsAlive || occupant.IsSealbound ||
                    (occupant.OwnerId == PlayerId.AI && occupant.IsHiding)) return;
                _drainedStatusVfx = GetComponent<DrainedStatusVfx>();
                if (_drainedStatusVfx == null) _drainedStatusVfx = gameObject.AddComponent<DrainedStatusVfx>();
            }
            _drainedStatusVfx.Refresh(occupant, _occupantVisualRoot, _occupantVisualImage,
                _isVisualActive, _isDeathPresentation);
        }

        private void RefreshActionDebuffStatusVfx()
        {
            var occupant = _tileView == null ? null : _tileView.CurrentOccupant;
            if (!isActiveAndEnabled)
            {
                _actionDebuffStatusVfx?.Cancel();
                return;
            }
            if (_actionDebuffStatusVfx == null)
            {
                if (!UnityEngine.Application.isPlaying || !_isVisualActive || _isDeathPresentation ||
                    occupant == null || (!occupant.IsStunned && !occupant.IsFrozen) || !occupant.IsAlive ||
                    (occupant.OwnerId == PlayerId.AI && occupant.IsHiding)) return;
                _actionDebuffStatusVfx = GetComponent<ActionDebuffStatusVfx>();
                if (_actionDebuffStatusVfx == null)
                    _actionDebuffStatusVfx = gameObject.AddComponent<ActionDebuffStatusVfx>();
            }
            _actionDebuffStatusVfx.Refresh(occupant, _occupantVisualRoot, _occupantVisualImage,
                _isVisualActive, _isDeathPresentation, _lastVisualControllerName, _lastVisualCardId);
        }

        private bool ShouldPauseFrozenBody()
        {
            var occupant = _tileView == null ? null : _tileView.CurrentOccupant;
            return UnityEngine.Application.isPlaying && isActiveAndEnabled && !_isBindingVisual &&
                _isVisualActive && !_isDeathPresentation && occupant != null && occupant.IsAlive &&
                occupant.IsFrozen && !(occupant.OwnerId == PlayerId.AI && occupant.IsHiding) &&
                string.Equals(occupant.RuntimeId, _lastVisualRuntimeId, StringComparison.Ordinal) &&
                _occupantVisualRoot != null && _occupantVisualRoot.gameObject.activeInHierarchy;
        }

        private void RefreshFrozenBodyPause()
        {
            var animator = _occupantVisualAnimator;
            if (!ShouldPauseFrozenBody() || animator == null || !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null)
            {
                ReleaseFrozenBodyPause();
                return;
            }
            if (_isFrozenBodyPaused &&
                (_frozenBodyAnimator != animator || _frozenBodyController != animator.runtimeAnimatorController ||
                 !string.Equals(_frozenBodyRuntimeId, _lastVisualRuntimeId, StringComparison.Ordinal)))
                ReleaseFrozenBodyPause();
            if (!_isFrozenBodyPaused)
            {
                _frozenBodyAnimator = animator;
                _frozenBodyController = animator.runtimeAnimatorController;
                _frozenBodyRuntimeId = _lastVisualRuntimeId;
                _frozenBodyPreviousSpeed = animator.speed;
                _isFrozenBodyPaused = true;
            }
            animator.speed = 0f;
        }

        private void ReleaseFrozenBodyPause()
        {
            if (_isFrozenBodyPaused && _frozenBodyAnimator != null)
                _frozenBodyAnimator.speed = _frozenBodyPreviousSpeed;
            _isFrozenBodyPaused = false;
            _frozenBodyAnimator = null;
            _frozenBodyController = null;
            _frozenBodyRuntimeId = null;
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
                _lastVisualControllerName = boardAnimatorController == null ? string.Empty : boardAnimatorController.name;
                BindVisual(boardSprite, boardAnimatorController, occupant);
                _isVisualActive = true;
            }
            else if (_lastVisualWasDrained != occupant.IsDrained && !ShouldPauseFrozenBody())
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

        private void RefreshTrapStatusMarker()
        {
            var occupant = _tileView?.CurrentOccupant;
            var battle = _battleBootstrapper?.CurrentBattleState;
            var count = _showOccupantStats && _isOccupied && occupant != null && occupant.Kind == OccupantKind.Master &&
                        battle != null && _occupantStatRoot != null
                ? battle.GetArmedTrapCount(occupant.OwnerId) : 0;
            if (count <= 0) { _trapStatusMarker?.Present(0, null); return; }
            if (_trapStatusMarker == null)
            {
                var markerObject = new GameObject("TrapStatusMarker", typeof(RectTransform), typeof(TrapStatusMarker));
                markerObject.transform.SetParent(_occupantStatRoot, false);
                _trapStatusMarker = markerObject.GetComponent<TrapStatusMarker>();
            }
            _trapStatusMarker.Present(count, _occupantStatHpText != null ? _occupantStatHpText.font : _occupantStatFontOverride);
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

            ReleaseFrozenBodyPause();
            _actionDebuffStatusVfx?.Cancel();
            _isBindingVisual = true;
            _isDeathPresentation = false;
            StopCharacterSound();
            // Release the previous controller before setting the incoming occupant sprite.
            // Animator teardown can restore its old sprite when the new controller has no states.
            _occupantVisualAnimator.runtimeAnimatorController = null;
            EnsureOccupantVisualSpriteRenderer();
            _occupantVisualImage.sprite = boardSprite;
            _occupantVisualImage.enabled = boardSprite != null;
            if (_occupantVisualSpriteRenderer != null)
            {
                _occupantVisualSpriteRenderer.sprite = boardSprite;
            }

            _occupantVisualAnimator.runtimeAnimatorController = boardAnimatorController;
            _occupantVisualAnimator.enabled = boardAnimatorController != null;
            _occupantVisualAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _blueDragonUsesScaledMotion = false;
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
            _isBindingVisual = false;
        }

        private void ApplyPassiveAnimationState(OccupantState occupant)
        {
            // Preserve the marker of the state actually playing while frozen.
            if (ShouldPauseFrozenBody()) return;
            _lastVisualWasDrained = occupant != null && occupant.IsDrained;

            if (_lastVisualWasDrained)
            {
                PlayDrainedAnimation();
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

            var shouldUseSuppressedStatColors = occupant.IsDrained || occupant.IsErasure || occupant.IsSealbound;
            var barColor = shouldUseSuppressedStatColors
                ? _occupantStatErasureBarColor
                : _occupantStatBarColor;
            var textColor = shouldUseSuppressedStatColors
                ? _occupantStatErasureTextColor
                : _occupantStatTextColor;
            var iconColor = shouldUseSuppressedStatColors
                ? _occupantStatErasureIconColor
                : _occupantStatIconColor;
            var attackIconColor = shouldUseSuppressedStatColors
                ? _occupantStatErasureIconColor
                : GetAttackIconColor(occupant.AttackType, occupant.DamageType);

            _occupantStatHeartImage.sprite = GetHeartIconSprite();
            _occupantStatAttackTypeImage.sprite = GetAttackIconSprite(
                occupant.AttackType,
                occupant.DamageType);
            var hp = _presentedHpRuntimeId != null &&
                string.Equals(_presentedHpRuntimeId, occupant.RuntimeId, StringComparison.Ordinal) &&
                string.Equals(_presentedHpRuntimeId, VisualRuntimeId, StringComparison.Ordinal) &&
                string.Equals(_presentedHpRuntimeId, _tileView?.CurrentOccupantRuntimeId, StringComparison.Ordinal)
                ? _presentedHp : occupant.CurrentHp;
            _occupantStatHpText.text = Mathf.Max(0, hp).ToString();
            _occupantStatAttackText.text = Mathf.Max(0, occupant.Attack).ToString();
            if (_occupantStatBackgroundImage != null)
            {
                _occupantStatBackgroundImage.color = barColor;
            }

            _occupantStatHeartImage.color = iconColor;
            _occupantStatAttackTypeImage.color = attackIconColor;
            _occupantStatHpText.color = textColor;
            _occupantStatAttackText.color = textColor;
            SetHuanShuStatusIconActive(
                _showHuanShuStatusIcon && occupant.IsUnderHuanShu);
        }

        private void SetHuanShuStatusIconActive(bool isActive)
        {
            if (_huanShuStatusIconRoot != null &&
                _huanShuStatusIconRoot.gameObject.activeSelf != isActive)
            {
                _huanShuStatusIconRoot.gameObject.SetActive(isActive);
            }
        }

        private void ClearVisual()
        {
            ReleaseFrozenBodyPause();
            _isBindingVisual = false;
            _isDeathPresentation = false;
            _drainedStatusVfx?.Cancel();
            _actionDebuffStatusVfx?.Cancel();
            ClearPresentedHp();
            StopCharacterSound();
            _lastVisualCardId = string.Empty;
            _lastVisualRuntimeId = string.Empty;
            _lastVisualSprite = null;
            _lastVisualController = null;
            _lastVisualControllerName = string.Empty;
            _lastVisualWasDrained = false;
            _isVisualActive = false;
            _activeSpriteSequence = null;
            _activeSequenceFrameIndex = 0;
            _activeSequenceFrameTimer = 0f;

            if (_occupantVisualAnimator != null)
            {
                _occupantVisualAnimator.runtimeAnimatorController = null;
                _occupantVisualAnimator.enabled = false;
            }

            if (_occupantVisualSpriteRenderer != null)
            {
                _occupantVisualSpriteRenderer.sprite = null;
            }

            if (_occupantVisualImage != null)
            {
                _occupantVisualImage.sprite = null;
                _occupantVisualImage.enabled = false;
            }

            SetStatBarActive(false);
            _trapStatusMarker?.Present(0, null);
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

        private void SetBlueDragonMotionLayout(bool usesScaledMotion)
        {
            if (_blueDragonUsesScaledMotion == usesScaledMotion) return;
            var previousScale = _blueDragonUsesScaledMotion ? BlueDragonPresentationAssets.BoardScale : 1f;
            _blueDragonUsesScaledMotion = usesScaledMotion;
            if (_occupantVisualRoot == null) return;
            var nextScale = usesScaledMotion ? BlueDragonPresentationAssets.BoardScale : 1f;
            var previousHeight = _occupantVisualRoot.sizeDelta.y;
            _occupantVisualRoot.sizeDelta *= nextScale / previousScale;
            // Preserve the ground line and any current combat travel offset.
            _occupantVisualRoot.anchoredPosition += Vector2.up *
                (_occupantVisualRoot.sizeDelta.y - previousHeight) * 0.5f;
        }

        private void PlayAnimationState(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName) || _occupantVisualAnimator == null)
            {
                return;
            }

            var requestsDeath = string.Equals(stateName, _deathStateName, StringComparison.OrdinalIgnoreCase);
            if (requestsDeath)
            {
                ReleaseFrozenBodyPause();
                _actionDebuffStatusVfx?.Cancel();
            }
            else
            {
                RefreshFrozenBodyPause();
                // A hit/idle/run request must not reset the pose held inside the ice.
                if (ShouldPauseFrozenBody()) return;
            }
            _isDeathPresentation = requestsDeath;
            if (_isDeathPresentation) _drainedStatusVfx?.Cancel();

            // All motion frames and playback timing come from the card's controller.
            // Keep the existing BlueDragon artwork-size correction independent of playback.
            if (_lastVisualCardId == BlueDragonPresentationAssets.CardId)
                SetBlueDragonMotionLayout(stateName != _idleStateName && stateName != _drainedStateName);
            if (!_occupantVisualAnimator.isActiveAndEnabled || !_occupantVisualAnimator.isInitialized ||
                _occupantVisualAnimator.runtimeAnimatorController == null)
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

            if (animatorStateWasPlayed && _occupantVisualSpriteRenderer != null)
            {
                _activeSpriteSequence = null;
                _activeSequenceFrameIndex = 0;
                _activeSequenceFrameTimer = 0f;
                SyncAnimatedVisualSprite(true);
                return;
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
            if (ShouldPauseFrozenBody()) return;
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

        private void SyncAnimatedVisualSprite(bool force = false)
        {
            if (_occupantVisualImage == null || _occupantVisualSpriteRenderer == null)
            {
                return;
            }

            if (!force && _activeSpriteSequence != null)
            {
                return;
            }

            var sprite = _occupantVisualSpriteRenderer.sprite;
            if (sprite == null)
            {
                return;
            }

            if (_occupantVisualImage.sprite != sprite)
            {
                _occupantVisualImage.sprite = sprite;
            }

            _occupantVisualImage.enabled = true;
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
            _sampleAnimationObject.transform.position = new Vector3(100000f, 100000f, 0f);
            _sampleSpriteRenderer = _sampleAnimationObject.GetComponent<SpriteRenderer>();
            _sampleSpriteRenderer.enabled = true;
            _sampleSpriteRenderer.color = new Color(1f, 1f, 1f, 0f);
        }

        private void EnsureOccupantVisualSpriteRenderer()
        {
            if (_occupantVisualRoot == null)
            {
                return;
            }

            if (_occupantVisualSpriteRenderer == null)
            {
                _occupantVisualSpriteRenderer = _occupantVisualRoot.GetComponent<SpriteRenderer>();
            }

            if (_occupantVisualSpriteRenderer == null && UnityEngine.Application.isPlaying)
            {
                _occupantVisualSpriteRenderer = _occupantVisualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            ConfigureOccupantVisualSpriteRenderer();
        }

        private void ConfigureOccupantVisualSpriteRenderer()
        {
            if (_occupantVisualSpriteRenderer == null)
            {
                return;
            }

            _occupantVisualSpriteRenderer.enabled = true;
            _occupantVisualSpriteRenderer.color = new Color(1f, 1f, 1f, 0f);
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
            if (s_previewOverlayRoot == null)
            {
                s_previewBackdropImage = null;
                s_previewCardImage = null;
                s_previewAttackText = null;
                s_previewHpText = null;
                s_previewTooltipRoot = null;
                PreviewTooltipPanels.Clear();
                PreviewAttachedPanels.Clear();
                PreviewInfluencePanels.Clear();
            }
            else if (s_previewTooltipRoot == null)
            {
                PreviewTooltipPanels.Clear();
                PreviewAttachedPanels.Clear();
                PreviewInfluencePanels.Clear();
            }

            if (s_previewOverlayRoot != null &&
                s_previewBackdropImage != null &&
                s_previewCardImage != null &&
                s_previewTooltipRoot != null)
            {
                EnsurePreviewStatTexts();
                ApplyPreviewOverlayLayout();
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
                s_previewTooltipRoot = existingOverlayRoot.Find(AutoPreviewTooltipRootName) as RectTransform;
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

            }

            if (s_previewBackdropImage == null)
            {
                var backdropObject = new GameObject(AutoPreviewBackdropName, typeof(RectTransform), typeof(Image));
                backdropObject.transform.SetParent(s_previewOverlayRoot, false);
                s_previewBackdropImage = backdropObject.GetComponent<Image>();
            }

            if (s_previewCardImage == null)
            {
                var previewImageObject = new GameObject(AutoPreviewImageName, typeof(RectTransform), typeof(Image));
                previewImageObject.transform.SetParent(s_previewOverlayRoot, false);
                s_previewCardImage = previewImageObject.GetComponent<Image>();
            }

            if (s_previewTooltipRoot == null)
            {
                var tooltipRootObject = new GameObject(AutoPreviewTooltipRootName, typeof(RectTransform));
                tooltipRootObject.transform.SetParent(s_previewOverlayRoot, false);
                s_previewTooltipRoot = tooltipRootObject.GetComponent<RectTransform>();
            }

            if (s_previewOverlayRoot == null ||
                s_previewBackdropImage == null ||
                s_previewCardImage == null ||
                s_previewTooltipRoot == null)
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

            s_previewTooltipRoot.anchorMin = Vector2.zero;
            s_previewTooltipRoot.anchorMax = Vector2.one;
            s_previewTooltipRoot.pivot = new Vector2(0.5f, 0.5f);
            s_previewTooltipRoot.offsetMin = Vector2.zero;
            s_previewTooltipRoot.offsetMax = Vector2.zero;
            s_previewTooltipRoot.localScale = Vector3.one;
            s_previewTooltipRoot.SetAsLastSibling();

            EnsurePreviewStatTexts();
            ApplyPreviewOverlayLayout();
            s_previewOverlayRoot.gameObject.SetActive(false);
        }

        [ContextMenu("Ensure Editable Card Preview")]
        public void EnsureEditableCardPreview()
        {
            AutoAssignView();
            EnsurePreviewOverlay();
        }

        private void EnsurePreviewStatTexts()
        {
            if (s_previewCardImage == null)
            {
                return;
            }

            s_previewAttackText = FindOrCreatePreviewStatText("AttackValueText");
            s_previewHpText = FindOrCreatePreviewStatText("HpValueText");
        }

        private TMP_Text FindOrCreatePreviewStatText(string objectName)
        {
            var existing = s_previewCardImage.transform.Find(objectName)?.GetComponent<TMP_Text>();
            if (existing != null)
            {
                return existing;
            }

            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(s_previewCardImage.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void RefreshCurrentCardPreview()
        {
            if (s_previewOwner != this || s_previewOverlayRoot == null ||
                !s_previewOverlayRoot.gameObject.activeSelf)
            {
                return;
            }

            if (_tileView == null || _tileView.CurrentOccupant == null ||
                _tileView.CurrentOccupantRuntimeId != _previewOccupantRuntimeId)
            {
                HideCurrentCardPreview();
                return;
            }

            var battle = _battleBootstrapper == null ? null : _battleBootstrapper.CurrentBattleState;
            if (_previewBoundOccupant != _tileView.CurrentOccupant || _previewBoundBattle != battle ||
                !UnityEngine.Application.isPlaying || Time.unscaledTime >= _nextPreviewTooltipRefreshTime)
            {
                ShowCurrentSpecialEffectTooltips(false);
            }
            if (_previewLayoutSize != s_previewOverlayRoot.rect.size)
            {
                ApplyPreviewOverlayLayout();
            }

            RefreshPreviewStats();
        }

        private void RefreshPreviewStats()
        {
            if (s_previewCardImage == null || s_previewCardImage.sprite == null ||
                s_previewAttackText == null || s_previewHpText == null)
            {
                return;
            }

            var occupant = _tileView == null ? null : _tileView.CurrentOccupant;
            s_previewAttackText.gameObject.SetActive(occupant != null);
            s_previewHpText.gameObject.SetActive(occupant != null);
            if (occupant == null)
            {
                return;
            }

            s_previewAttackText.text = Mathf.Max(0, occupant.Attack).ToString();
            s_previewHpText.text = Mathf.Max(0, occupant.CurrentHp).ToString();
            var renderedRect = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(s_previewCardImage);
            ApplyPreviewStatLayout(s_previewAttackText, renderedRect, _cardPreviewAttackNormalizedPosition);
            ApplyPreviewStatLayout(s_previewHpText, renderedRect, _cardPreviewHpNormalizedPosition);
        }

        private void ApplyPreviewStatLayout(TMP_Text text, Rect renderedRect, Vector2 normalizedPosition)
        {
            var rect = text.rectTransform;
            HandCardStatOverlayLayout.PositionStatText(rect, s_previewCardImage.rectTransform,
                renderedRect, normalizedPosition);
            var referenceHeight = Mathf.Max(1f, _cardPreviewSize.y);
            rect.sizeDelta = _cardPreviewStatTextSize * (renderedRect.height / referenceHeight);
            rect.localScale = Vector3.one;
            text.fontSize = HandCardStatOverlayLayout.CalculateScaledFontSize(
                _cardPreviewStatFontSize, renderedRect.height, referenceHeight);
            text.color = _cardPreviewStatTextColor;
            var font = _cardPreviewStatFont != null ? _cardPreviewStatFont : _occupantStatFontOverride;
            if (font == null && _text != null)
            {
                font = _text.font;
            }

            if (font != null && text.font != font)
            {
                text.font = font;
            }
        }

        private void ShowCurrentSpecialEffectTooltips(bool force = true)
        {
            var occupant = _tileView == null ? null : _tileView.CurrentOccupant;
            if (occupant == null || s_previewTooltipRoot == null)
            {
                HideSpecialEffectTooltips();
                ApplyPreviewOverlayLayout();
                return;
            }

            CardDefinition definition = null;
            AutoAssignBootstrapper();
            _battleBootstrapper?.TryGetCardDefinition(occupant.CardId, out definition);
            var battle = _battleBootstrapper == null ? null : _battleBootstrapper.CurrentBattleState;
            var abilities = OccupantSpecialEffectTooltipCatalog.Build(definition, occupant);
            var attached = OccupantStatusTooltipCatalog.BuildAttached(definition, occupant, battle);
            var influences = OccupantStatusTooltipCatalog.BuildInfluences(definition, occupant, battle);
            _previewBoundOccupant = occupant;
            _previewBoundBattle = battle;
            _nextPreviewTooltipRefreshTime = Time.unscaledTime + 0.15f;
            if (!force && SameTooltipEntries(_previewAbilityEntries, abilities) &&
                SameTooltipEntries(_previewAttachedEntries, attached) &&
                SameTooltipEntries(_previewInfluenceEntries, influences))
            {
                return;
            }

            _previewAbilityEntries = abilities;
            _previewAttachedEntries = attached;
            _previewInfluenceEntries = influences;
            BindPreviewTooltipPanels(PreviewTooltipPanels, AutoPreviewTooltipPanelPrefix, abilities);
            BindPreviewTooltipPanels(PreviewAttachedPanels, "AttachedEffectTooltip", attached);
            BindPreviewTooltipPanels(PreviewInfluencePanels, "InfluenceTooltip", influences);
            s_previewTooltipRoot.gameObject.SetActive(abilities.Count + attached.Count + influences.Count > 0);
            ApplyPreviewOverlayLayout();
        }

        private static bool SameTooltipEntries(
            IReadOnlyList<OccupantSpecialEffectTooltip> previous,
            IReadOnlyList<OccupantSpecialEffectTooltip> current)
        {
            if (previous.Count != current.Count) return false;
            for (var index = 0; index < previous.Count; index++)
            {
                if (previous[index].Title != current[index].Title ||
                    previous[index].Description != current[index].Description ||
                    previous[index].Kind != current[index].Kind) return false;
            }
            return true;
        }

        private void BindPreviewTooltipPanels(List<PreviewTooltipPanel> panels, string prefix,
            IReadOnlyList<OccupantSpecialEffectTooltip> entries)
        {
            while (panels.Count < entries.Count)
                panels.Add(FindOrCreatePreviewTooltipPanel(prefix, panels.Count));
            var font = ResolvePreviewTooltipFont();
            for (var index = 0; index < panels.Count; index++)
            {
                var panel = panels[index];
                panel.Root.gameObject.SetActive(index < entries.Count);
                if (index >= entries.Count) continue;
                var entry = entries[index];
                var color = ResolvePreviewTooltipKindColor(entry.Kind);
                panel.Background.color = _cardPreviewTooltipPanelColor;
                panel.Background.raycastTarget = false;
                panel.Outline.effectColor = entry.Kind == OccupantTooltipKind.Ability
                    ? _cardPreviewTooltipBorderColor : color;
                panel.Outline.effectDistance = new Vector2(2f, -2f);
                panel.Title.text = entry.Title;
                panel.Title.color = color;
                panel.Title.fontStyle = FontStyles.Bold;
                panel.Title.enableAutoSizing = false;
                panel.Title.textWrappingMode = TextWrappingModes.Normal;
                panel.Title.overflowMode = TextOverflowModes.Overflow;
                panel.Title.raycastTarget = false;
                panel.Description.text = entry.Description;
                panel.Description.gameObject.SetActive(!string.IsNullOrWhiteSpace(entry.Description));
                panel.Description.color = _cardPreviewTooltipDescriptionColor;
                panel.Description.fontStyle = FontStyles.Normal;
                panel.Description.enableAutoSizing = false;
                panel.Description.textWrappingMode = TextWrappingModes.Normal;
                panel.Description.overflowMode = TextOverflowModes.Overflow;
                panel.Description.raycastTarget = false;
                if (font != null)
                {
                    panel.Title.font = font;
                    panel.Description.font = font;
                }
            }
        }

        private Color ResolvePreviewTooltipKindColor(OccupantTooltipKind kind)
        {
            switch (kind)
            {
                case OccupantTooltipKind.Buff: return new Color(0.58f, 1f, 0.66f, 1f);
                case OccupantTooltipKind.Debuff: return new Color(1f, 0.62f, 0.59f, 1f);
                case OccupantTooltipKind.State: return new Color(0.60f, 0.82f, 1f, 1f);
                case OccupantTooltipKind.Influence: return new Color(1f, 0.88f, 0.50f, 1f);
                default: return _cardPreviewTooltipTitleColor;
            }
        }

        private void HideSpecialEffectTooltips()
        {
            HideTooltipPanels(PreviewTooltipPanels);
            HideTooltipPanels(PreviewAttachedPanels);
            HideTooltipPanels(PreviewInfluencePanels);
            _previewAbilityEntries = Array.Empty<OccupantSpecialEffectTooltip>();
            _previewAttachedEntries = Array.Empty<OccupantSpecialEffectTooltip>();
            _previewInfluenceEntries = Array.Empty<OccupantSpecialEffectTooltip>();
            _previewBoundOccupant = null;
            _previewBoundBattle = null;
            if (s_previewTooltipRoot != null) s_previewTooltipRoot.gameObject.SetActive(false);
        }

        private static void HideTooltipPanels(List<PreviewTooltipPanel> panels)
        {
            foreach (var panel in panels)
                if (panel?.Root != null) panel.Root.gameObject.SetActive(false);
        }

        private PreviewTooltipPanel FindOrCreatePreviewTooltipPanel(string prefix, int index)
        {
            var panelName = $"{prefix}_{index:00}";
            var existingRoot = s_previewTooltipRoot.Find(panelName) as RectTransform;
            if (existingRoot != null)
            {
                var existingBackground = existingRoot.GetComponent<UnityEngine.UI.Image>();
                var existingOutline = existingRoot.GetComponent<UnityEngine.UI.Outline>();
                var existingTitle = existingRoot.Find(AutoPreviewTooltipTitleName)?.GetComponent<TMP_Text>();
                var existingDescription = existingRoot.Find(AutoPreviewTooltipDescriptionName)?.GetComponent<TMP_Text>();
                if (existingBackground != null && existingOutline != null &&
                    existingTitle != null && existingDescription != null)
                    return new PreviewTooltipPanel(existingRoot, existingBackground,
                        existingOutline, existingTitle, existingDescription);
            }

            var panelObject = new GameObject(panelName, typeof(RectTransform),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
            panelObject.transform.SetParent(s_previewTooltipRoot, false);
            var panelRoot = panelObject.GetComponent<RectTransform>();
            var background = panelObject.GetComponent<UnityEngine.UI.Image>();
            var outline = panelObject.GetComponent<UnityEngine.UI.Outline>();
            background.raycastTarget = false;
            TMP_Text CreateText(string name, TextAlignmentOptions alignment)
            {
                var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(panelRoot, false);
                var text = textObject.GetComponent<TextMeshProUGUI>();
                text.alignment = alignment;
                text.enableAutoSizing = false;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;
                return text;
            }
            return new PreviewTooltipPanel(panelRoot, background, outline,
                CreateText(AutoPreviewTooltipTitleName, TextAlignmentOptions.TopLeft),
                CreateText(AutoPreviewTooltipDescriptionName, TextAlignmentOptions.TopLeft));
        }

        private void ApplyPreviewOverlayLayout()
        {
            if (s_previewOverlayRoot == null || s_previewCardImage == null) return;
            Canvas.ForceUpdateCanvases();
            _previewLayoutSize = s_previewOverlayRoot.rect.size;
            var overlaySize = _previewLayoutSize;
            if (overlaySize.x <= 1f || overlaySize.y <= 1f)
                overlaySize = new Vector2(1920f, 1080f);
            var layout = CardPreviewTooltipLayout.Calculate(overlaySize.x, overlaySize.y,
                _cardPreviewSize.x, _cardPreviewSize.y, _previewAbilityEntries.Count,
                _previewAttachedEntries.Count, _previewInfluenceEntries.Count,
                MeasurePreviewTooltip, _cardPreviewTooltipSize.x, _cardPreviewTooltipSize.y,
                _cardPreviewTooltipEdgePadding, _cardPreviewTooltipSideGap, _cardPreviewTooltipVerticalGap);
            SetPreviewRect(s_previewCardImage.rectTransform, layout.Card);
            ApplyPreviewTooltipGroupLayout(PreviewTooltipPanels, layout.Abilities, layout.TextScale);
            ApplyPreviewTooltipGroupLayout(PreviewAttachedPanels, layout.Attached, layout.TextScale);
            ApplyPreviewTooltipGroupLayout(PreviewInfluencePanels, layout.Influences, layout.TextScale);
        }

        private float MeasurePreviewTooltip(CardPreviewTooltipZone zone, int index, float width, float scale)
        {
            var panel = zone == CardPreviewTooltipZone.Ability ? PreviewTooltipPanels[index] :
                zone == CardPreviewTooltipZone.Attached ? PreviewAttachedPanels[index] : PreviewInfluencePanels[index];
            ApplyPreviewTooltipFontSize(panel, scale);
            var textWidth = Mathf.Max(1f, width - 24f * scale);
            var titleHeight = panel.Title.GetPreferredValues(panel.Title.text, textWidth, Mathf.Infinity).y;
            if (!panel.Description.gameObject.activeSelf)
                return titleHeight + 16f * scale;
            var bodyHeight = panel.Description.GetPreferredValues(panel.Description.text, textWidth, Mathf.Infinity).y;
            return Mathf.Max(_cardPreviewTooltipSize.y * scale, titleHeight + bodyHeight + 24f * scale);
        }

        private void ApplyPreviewTooltipGroupLayout(List<PreviewTooltipPanel> panels,
            IReadOnlyList<CardPreviewTooltipRect> boxes, float scale)
        {
            for (var index = 0; index < boxes.Count; index++)
            {
                var panel = panels[index];
                var box = boxes[index];
                SetPreviewRect(panel.Root, box);
                ApplyPreviewTooltipFontSize(panel, scale);
                var horizontalPadding = 12f * scale;
                var verticalPadding = 8f * scale;
                var titleHeight = panel.Title.GetPreferredValues(panel.Title.text,
                    Mathf.Max(1f, box.Width - 2f * horizontalPadding), Mathf.Infinity).y;
                if (!panel.Description.gameObject.activeSelf)
                    verticalPadding = Mathf.Max(verticalPadding, (box.Height - titleHeight) * 0.5f);
                var titleRect = panel.Title.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = Vector2.one;
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.offsetMin = new Vector2(horizontalPadding, -verticalPadding - titleHeight);
                titleRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
                if (!panel.Description.gameObject.activeSelf) continue;
                var descriptionRect = panel.Description.rectTransform;
                descriptionRect.anchorMin = Vector2.zero;
                descriptionRect.anchorMax = Vector2.one;
                descriptionRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
                descriptionRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding - titleHeight - 6f * scale);
            }
        }

        private void ApplyPreviewTooltipFontSize(PreviewTooltipPanel panel, float scale)
        {
            panel.Title.fontSize = Mathf.Max(18f, _cardPreviewTooltipTitleFontSize) * scale;
            panel.Description.fontSize = Mathf.Max(14f, _cardPreviewTooltipDescriptionFontSize) * scale;
        }

        private static void SetPreviewRect(RectTransform rect, CardPreviewTooltipRect box)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(box.X, box.Y);
            rect.sizeDelta = new Vector2(box.Width, box.Height);
            rect.localScale = Vector3.one;
        }

        private TMP_FontAsset ResolvePreviewTooltipFont()
        {
            if (_cardPreviewTooltipFont != null)
            {
                return _cardPreviewTooltipFont;
            }

            var runtimeKoreanFont = CombatLogTextView.ResolveRuntimeKoreanFontAsset();
            return runtimeKoreanFont != null
                ? runtimeKoreanFont
                : _text == null
                    ? null
                    : _text.font;
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
                string.Equals(stateName, _drainedStateName, StringComparison.OrdinalIgnoreCase) ||
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

            if (_occupantVisualRoot != null && _occupantVisualSpriteRenderer == null)
            {
                _occupantVisualSpriteRenderer = _occupantVisualRoot.GetComponent<SpriteRenderer>();
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
                    var huanShuIcon = statRectTransform.Find(AutoHuanShuIconName) as RectTransform;
                    if (huanShuIcon != null)
                    {
                        _huanShuStatusIconRoot = huanShuIcon;
                        _huanShuStatusIconBackground = huanShuIcon.GetComponent<Image>();
                        _huanShuStatusIconGlyph = huanShuIcon.Find("Glyph")?.GetComponent<Image>();
                        _huanShuStatusIconCore = huanShuIcon.Find("Core")?.GetComponent<Image>();
                    }
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

                EnsureOccupantVisualSpriteRenderer();
                return;
            }

            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            var visualObject = new GameObject(
                AutoVisualName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(Image),
                typeof(Animator),
                typeof(SpriteRenderer));
            visualObject.transform.SetParent(visualParent, false);

            _occupantVisualRoot = visualObject.GetComponent<RectTransform>();
            _occupantVisualCanvas = visualObject.GetComponent<Canvas>();
            _occupantVisualImage = visualObject.GetComponent<Image>();
            _occupantVisualAnimator = visualObject.GetComponent<Animator>();
            _occupantVisualSpriteRenderer = visualObject.GetComponent<SpriteRenderer>();
            ConfigureOccupantVisualSpriteRenderer();
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

        private static void ApplyCenteredRectLayout(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
            rectTransform.localScale = Vector3.one;
        }

        private Image FindTileBackgroundImage()
        {
            var tileBackgroundRect = FindTileBackgroundRect();
            return tileBackgroundRect == null ? null : tileBackgroundRect.GetComponent<Image>();
        }

        private void EnsureRuntimeStatObjects()
        {
            if (!UnityEngine.Application.isPlaying || _occupantVisualRoot == null)
            {
                return;
            }

            if (_occupantStatRoot == null)
            {
                var statBarObject = new GameObject(AutoStatBarName, typeof(RectTransform), typeof(Image));
                statBarObject.transform.SetParent(_occupantVisualRoot, false);

                _occupantStatRoot = statBarObject.GetComponent<RectTransform>();
                _occupantStatBackgroundImage = statBarObject.GetComponent<Image>();
                _occupantStatBackgroundImage.raycastTarget = false;

                _occupantStatHeartImage = CreateStatImage("HeartIcon", _occupantStatRoot);
                _occupantStatHpText = CreateStatText("HpText", _occupantStatRoot, TextAlignmentOptions.Left);
                _occupantStatAttackTypeImage = CreateStatImage("AttackTypeIcon", _occupantStatRoot);
                _occupantStatAttackText = CreateStatText("AttackText", _occupantStatRoot, TextAlignmentOptions.Left);
            }

            if (_occupantStatCanvas == null)
            {
                _occupantStatCanvas = _occupantStatRoot.GetComponent<Canvas>();

                if (_occupantStatCanvas == null)
                {
                    _occupantStatCanvas =
                        _occupantStatRoot.gameObject.AddComponent<Canvas>();
                }
            }

            _occupantStatCanvas.overrideSorting = true;
            _occupantStatCanvas.sortingLayerID =
                _occupantVisualCanvas != null
                    ? _occupantVisualCanvas.sortingLayerID
                    : 0;
            _occupantStatCanvas.sortingOrder = 4950;

            EnsureRuntimeHuanShuStatusIcon();
            ApplyStatBarLayout();
        }

        private void EnsureRuntimeHuanShuStatusIcon()
        {
            if (_huanShuStatusIconRoot != null || _occupantStatRoot == null)
            {
                return;
            }

            var iconObject = new GameObject(AutoHuanShuIconName, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(_occupantStatRoot, false);
            _huanShuStatusIconRoot = iconObject.GetComponent<RectTransform>();
            _huanShuStatusIconBackground = iconObject.GetComponent<Image>();
            _huanShuStatusIconBackground.raycastTarget = false;

            _huanShuStatusIconGlyph = CreateStatImage("Glyph", _huanShuStatusIconRoot);
            _huanShuStatusIconCore = CreateStatImage("Core", _huanShuStatusIconRoot);
            SetHuanShuStatusIconActive(false);
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
            var popupColor = isHealing ? _healingPopupColor : _damagePopupColor;
            yield return PlayFloatingPopupSequence(
                amount.ToString(),
                popupColor,
                isHealing,
                delaySeconds,
                stackIndex);
        }

        private IEnumerator PlayFloatingPopupSequence(
            string popupValue,
            Color popupColor,
            bool isHealing,
            float delaySeconds,
            int stackIndex)
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
            popupText.text = popupValue;
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
            popupText.textWrappingMode = TextWrappingModes.NoWrap;
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

            var responsiveTileScale = GetResponsiveTileScale();
            var resolvedMarkOffset = _persistentMarkOffset * responsiveTileScale;
            if (_occupantVisualRoot != null)
            {
                _persistentMarkRoot.anchorMin = _occupantVisualRoot.anchorMin;
                _persistentMarkRoot.anchorMax = _occupantVisualRoot.anchorMax;
                _persistentMarkRoot.pivot = _occupantVisualRoot.pivot;
                _persistentMarkRoot.anchoredPosition = _occupantVisualRoot.anchoredPosition + resolvedMarkOffset;
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
                    _persistentMarkRoot.anchoredPosition = tileBackgroundRect.anchoredPosition + resolvedMarkOffset;
                    _persistentMarkRoot.localRotation = tileBackgroundRect.localRotation;
                }
                else
                {
                    _persistentMarkRoot.anchorMin = new Vector2(0.5f, 0.5f);
                    _persistentMarkRoot.anchorMax = new Vector2(0.5f, 0.5f);
                    _persistentMarkRoot.pivot = new Vector2(0.5f, 0.5f);
                    _persistentMarkRoot.anchoredPosition = resolvedMarkOffset;
                    _persistentMarkRoot.localRotation = Quaternion.identity;
                }
            }

            _persistentMarkRoot.sizeDelta = _persistentMarkSize * responsiveTileScale;
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
            if (_occupantStatFontOverride != null)
            {
                runtimeText.font = _occupantStatFontOverride;
            }
            else if (_text != null && _text.font != null)
            {
                runtimeText.font = _text.font;
            }

            runtimeText.fontSize = _occupantStatFontSize;
            runtimeText.alignment = alignment;
            runtimeText.textWrappingMode = TextWrappingModes.NoWrap;
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
            var tileBackgroundRect = FindTileBackgroundRect();
            var responsiveVisualScale = GetResponsiveTileScale();
            var referenceVisualOffset = _occupantVisualFineOffset + DefaultOccupantVisualLiftOffset;
            var resolvedVisualOffset = tileBackgroundRect == null
                ? referenceVisualOffset
                : BoardResponsiveLayoutCalculator.ScaleTileRelativeOffset(
                    referenceVisualOffset,
                    tileBackgroundRect.rect.size,
                    _occupantVisualReferenceTileSize);
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

            _occupantVisualRoot.sizeDelta = _occupantVisualSize *
                                            Mathf.Max(0.1f, _occupantVisualScaleMultiplier) *
                                            responsiveVisualScale;
            var atlasScale = _lastVisualCardId == GwangmaPresentationAssets.CardId
                ? GwangmaPresentationAssets.BoardScale
                : _lastVisualCardId == BlueDragonPresentationAssets.CardId && _blueDragonUsesScaledMotion
                    ? BlueDragonPresentationAssets.BoardScale : 1f;
            if (atlasScale != 1f)
            {
                // New art fills its cells; keep its ground line at the existing tile lift.
                var originalHeight = _occupantVisualRoot.sizeDelta.y;
                _occupantVisualRoot.sizeDelta *= atlasScale;
                _occupantVisualRoot.anchoredPosition -= Vector2.up * originalHeight * (1f - atlasScale) * 0.5f;
            }
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
            var responsiveTileScale = GetResponsiveTileScale();
            var resolvedEffectOffset = _spellEffectOffset * responsiveTileScale;

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
                    _spellEffectRoot.anchoredPosition = localPoint + resolvedEffectOffset;
                }
                else
                {
                    _spellEffectRoot.anchoredPosition = resolvedEffectOffset;
                }
            }
            else
            {
                _spellEffectRoot.anchoredPosition = resolvedEffectOffset;
            }

            var resolvedEffectSize = size == Vector2.zero ? _spellEffectSize : size;
            _spellEffectRoot.sizeDelta = resolvedEffectSize * responsiveTileScale;
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
            var responsiveTileScale = GetResponsiveTileScale();
            _occupantStatRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _occupantStatRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _occupantStatRoot.pivot = new Vector2(0.5f, 0.5f);
            _occupantStatRoot.anchoredPosition = new Vector2(
                _occupantStatBarOffset.x * responsiveTileScale,
                _occupantStatBarOffset.y *
                Mathf.Max(1f, _occupantVisualScaleMultiplier) *
                responsiveTileScale);
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
            ApplyOccupantStatFont(_occupantStatHpText);
            ApplyOccupantStatFont(_occupantStatAttackText);
            ApplyHuanShuStatusIconLayout();
        }

        private void ApplyOccupantStatFont(TMP_Text textComponent)
        {
            if (textComponent != null && _occupantStatFontOverride != null)
            {
                textComponent.font = _occupantStatFontOverride;
            }
        }

        private void ApplyHuanShuStatusIconLayout()
        {
            if (_huanShuStatusIconRoot == null)
            {
                return;
            }

            _huanShuStatusIconRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _huanShuStatusIconRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _huanShuStatusIconRoot.pivot = new Vector2(0.5f, 0.5f);
            _huanShuStatusIconRoot.anchoredPosition = _huanShuStatusIconOffset;
            _huanShuStatusIconRoot.sizeDelta = _huanShuStatusIconSize;
            _huanShuStatusIconRoot.localScale = Vector3.one;
            _huanShuStatusIconRoot.localRotation = Quaternion.identity;

            if (_huanShuStatusIconBackground != null)
            {
                _huanShuStatusIconBackground.color = _huanShuStatusIconColor;
                _huanShuStatusIconBackground.raycastTarget = false;
            }

            ApplyHuanShuGlyphLayout(
                _huanShuStatusIconGlyph,
                0.58f,
                45f,
                _huanShuStatusGlyphColor);
            ApplyHuanShuGlyphLayout(
                _huanShuStatusIconCore,
                0.22f,
                0f,
                _huanShuStatusIconColor);
        }

        private static void ApplyHuanShuGlyphLayout(
            Image image,
            float sizeRatio,
            float rotationDegrees,
            Color color)
        {
            if (image == null)
            {
                return;
            }

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = new Vector3(sizeRatio, sizeRatio, 1f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            image.color = color;
            image.raycastTarget = false;
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

            var highlight = _tileView != null ? _tileView.HighlightState : _lastHighlightState;
            var showTargetPulse = highlight == BattleHighlightState.Playable ||
                                  highlight == BattleHighlightState.SpellTarget;
            if (showTargetPulse && _cardTargetPulse == null)
                _cardTargetPulse = TileTargetPulse.Create(tileBackgroundImage);
            if (_cardTargetPulse != null)
                _cardTargetPulse.Refresh(tileBackgroundImage, showTargetPulse, Time.unscaledTime);

            tileBackgroundImage.color = highlight switch
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
                        "...XXX...",
                        "...XXX...",
                        "...XXX...",
                        "...XXX...",
                        "XXXXXXXXX", 
                        "...XXX...",
                    },
                    Color.white);
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
                        "..X......",
                        "..XXX....",
                        "..X..XX..",
                        "..X....XX",
                        "XXXXXXXXX",
                        "..X..XX..",
                        "..XXX....",
                        "..X......",   
                    },
                    Color.white);
            }

            return s_bowIconSprite;
        }

        private static Sprite GetMagicStaffIconSprite()
        {
            if (s_magicStaffIconSprite == null)
            {
                s_magicStaffIconSprite = CreateIconSprite(
                    "TileMagicStaffIcon",
                    new[]
                    {
                        "...XXX...",
                        "..XXXXX..",
                        "...XXX...",
                        "....X....",
                        "...XXX...",
                        "....X....",
                        "....X....",
                        "...XXX...",
                    },
                    Color.white);
            }

            return s_magicStaffIconSprite;
        }

        private static Sprite GetAttackIconSprite(AttackType attackType, DamageType damageType)
        {
            if (attackType == AttackType.Ranged && damageType == DamageType.Magic)
            {
                return GetMagicStaffIconSprite();
            }

            return attackType == AttackType.Melee
                ? GetSwordIconSprite()
                : GetBowIconSprite();
        }

        private static Color GetAttackIconColor(AttackType attackType, DamageType damageType)
        {
            if (damageType == DamageType.Magic)
            {
                return MagicAttackIconColor;
            }

            if (damageType == DamageType.Physical)
            {
                return attackType == AttackType.Melee
                    ? PhysicalMeleeAttackIconColor
                    : PhysicalRangedAttackIconColor;
            }

            return NeutralAttackIconColor;
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

        private sealed class PreviewTooltipPanel
        {
            public PreviewTooltipPanel(
                RectTransform root,
                Image background,
                Outline outline,
                TMP_Text title,
                TMP_Text description)
            {
                Root = root;
                Background = background;
                Outline = outline;
                Title = title;
                Description = description;
            }

            public RectTransform Root { get; }
            public Image Background { get; }
            public Outline Outline { get; }
            public TMP_Text Title { get; }
            public TMP_Text Description { get; }
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
