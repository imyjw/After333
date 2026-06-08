using TMPro;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Cards;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed class HandCardTextView : MonoBehaviour
    {
        [SerializeField] private HandCardView _handCardView;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Image _artworkImage;
        [SerializeField] private string _emptyLabel = "Empty";
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _selectedColor = new Color(0.55f, 1f, 1f);
        [SerializeField] private Color _playableColor = new Color(0.7f, 1f, 0.7f, 1f);
        [SerializeField] private Color _artworkNormalColor = Color.white;
        [SerializeField] private Color _artworkSelectedColor = new Color(0.82f, 1f, 1f, 1f);
        [SerializeField] private Color _artworkPlayableColor = new Color(0.92f, 1f, 0.92f, 1f);
        [SerializeField] private bool _hideTextWhenArtworkVisible = true;
        [SerializeField] private float _artworkPadding = 6f;
        [SerializeField] private float _pulseScale = 1.05f;
        [SerializeField] private float _pulseSpeed = 6f;

        [Header("Runtime Hand Card")]
        [SerializeField] private float _layoutLerpSpeed = 18f;
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color _selectedShadowColor = new Color(0.24f, 0.71f, 1f, 0.78f);
        [SerializeField] private Color _playableShadowColor = new Color(0.32f, 1f, 0.32f, 0.85f);
        [SerializeField] private Vector2 _shadowDistance = new Vector2(0f, -18f);
        [SerializeField] private Vector2 _selectedShadowDistance = new Vector2(0f, -26f);
        [SerializeField] private Vector2 _playableShadowDistance = new Vector2(0f, 0f);
        [SerializeField] private Color _playableOutlineColor = new Color(0.34f, 1f, 0.34f, 0.92f);
        [SerializeField] private Vector2 _playableOutlineDistance = new Vector2(4f, 4f);
        [SerializeField] private Color _playableGlowColor = new Color(0.18f, 1f, 0.18f, 0.34f);
        [SerializeField] private Vector2 _playableGlowPadding = new Vector2(16f, 20f);
        [SerializeField] private int _runtimeCardSortingBaseOrder = 120;
        [SerializeField] private int _runtimeSelectedCardSortingBaseOrder = 4200;
        [SerializeField] private int _runtimeDraggedCardSortingBaseOrder = 4600;
        [SerializeField] private float _runtimeDragAbsoluteScale = 0.4f;

        private static readonly Dictionary<string, Sprite> OpaqueRuntimeSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static Sprite s_runtimeWhiteSprite;

        private string _label = "Empty";
        private bool _hasCard;
        private string _lastRenderedText = string.Empty;
        private BattleHighlightState _lastHighlightState = BattleHighlightState.None;
        private Vector3 _textBaseScale = Vector3.one;
        private Vector3 _artworkBaseScale = Vector3.one;
        private string _lastArtworkCardId = string.Empty;

        private Canvas _legacyCanvas;
        private GraphicRaycaster _legacyRaycaster;

        private RectTransform _runtimeHandRoot;
        private RectTransform _runtimeCardRect;
        private Canvas _runtimeCardCanvas;
        private CanvasGroup _runtimeCanvasGroup;
        private Image _runtimeHitboxImage;
        private Image _runtimePlayableGlowImage;
        private Image _runtimeCardImage;
        private Outline _runtimeCardOutline;
        private Shadow _runtimeShadow;
        private Button _runtimeButton;
        private HandCardRuntimeDragRelay _runtimeDragRelay;
        private Text _runtimeFallbackText;

        private Vector2 _targetRuntimeAnchoredPosition;
        private float _targetRuntimeRotationDegrees;
        private Vector2 _targetRuntimeSize = new Vector2(270f, 378f);
        private float _targetRuntimeScale = 1f;
        private int _targetRuntimeSortingOrder;
        private bool _targetRuntimeVisible;
        private bool _runtimeLayoutInitialized;
        private bool _isRuntimeDragActive;
        private Vector2 _runtimeDragAnchoredPosition;
        private bool _suppressNextRuntimeClick;

        private bool HasRuntimeVisual => _runtimeCardRect != null;

        private bool IsArtworkVisible => _artworkImage != null &&
                                         _artworkImage.enabled &&
                                         _artworkImage.sprite != null;

        private void Awake()
        {
            AutoAssignView();
            CacheLegacyCanvas();
            EnsureArtworkImage();
            CacheBaseScale();
        }

        private void OnValidate()
        {
            AutoAssignView();
            CacheBaseScale();
        }

        private void LateUpdate()
        {
            Refresh();

            if (HasRuntimeVisual)
            {
                ApplyRuntimeAnimation();
                return;
            }

            ApplyPulse();
        }

        private void OnDestroy()
        {
            if (_runtimeCardRect != null)
            {
                Destroy(_runtimeCardRect.gameObject);
            }
        }

        public void SetHandCard(string label, bool hasCard)
        {
            _label = label ?? string.Empty;
            _hasCard = hasCard;
            Refresh();
        }

        public void BindRuntimeHandRoot(RectTransform runtimeHandRoot)
        {
            AutoAssignView();
            SyncFromHandCardView();

            if (_runtimeHandRoot == runtimeHandRoot && HasRuntimeVisual)
            {
                return;
            }

            _runtimeHandRoot = runtimeHandRoot;
            EnsureRuntimeVisual();
            Refresh();
        }

        public void ApplyRuntimeLayout(
            Vector2 anchoredPosition,
            float rotationDegrees,
            Vector2 size,
            float scale,
            int siblingIndex,
            bool visible)
        {
            AutoAssignView();
            SyncFromHandCardView();
            EnsureRuntimeVisual();
            if (!HasRuntimeVisual)
            {
                return;
            }

            _targetRuntimeAnchoredPosition = anchoredPosition;
            _targetRuntimeRotationDegrees = rotationDegrees;
            _targetRuntimeSize = size;
            _targetRuntimeScale = scale;
            _targetRuntimeVisible = visible;
            _targetRuntimeSortingOrder = ResolveRuntimeSortingOrder(siblingIndex);

            if (!_runtimeLayoutInitialized || !UnityEngine.Application.isPlaying)
            {
                _runtimeLayoutInitialized = true;
                _runtimeCardRect.anchoredPosition = anchoredPosition;
                _runtimeCardRect.sizeDelta = size;
                _runtimeCardRect.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
                _runtimeCardRect.localScale = Vector3.one * scale;
            }

            if (siblingIndex >= 0 && _runtimeCardRect.parent != null)
            {
                _runtimeCardRect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, _runtimeCardRect.parent.childCount - 1));
            }

            ApplyRuntimeSortingOrder();

            var shouldShowRuntimeCard = visible && _hasCard;
            if (_runtimeCardRect.gameObject.activeSelf != shouldShowRuntimeCard)
            {
                _runtimeCardRect.gameObject.SetActive(shouldShowRuntimeCard);
            }

            _runtimeCanvasGroup.alpha = shouldShowRuntimeCard ? 1f : 0f;
            _runtimeCanvasGroup.blocksRaycasts = shouldShowRuntimeCard;
            _runtimeCanvasGroup.interactable = shouldShowRuntimeCard;
        }

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            SyncFromHandCardView();

            if (HasRuntimeVisual)
            {
                RefreshRuntimeVisual();
                SetLegacyCanvasVisible(false);
                return;
            }

            RefreshLegacyVisual();
        }

        private void RefreshLegacyVisual()
        {
            RefreshArtwork();

            if (_text == null)
            {
                return;
            }

            var highlightState = _handCardView == null ? BattleHighlightState.None : _handCardView.HighlightState;
            _lastHighlightState = highlightState;

            var shouldHideText = _hideTextWhenArtworkVisible && IsArtworkVisible;
            if (shouldHideText)
            {
                if (_text.enabled)
                {
                    _text.enabled = false;
                }

                _text.rectTransform.localScale = _textBaseScale;
                return;
            }

            if (!_text.enabled)
            {
                _text.enabled = true;
            }

            var resolvedLabel = _hasCard
                ? _label
                : _emptyLabel;

            var highlightLabel = BattleUiFormatter.FormatHighlightLabel(highlightState);
            var renderedText = BattleRichTextStyler.StyleHandCard(resolvedLabel, highlightLabel, _hasCard, _emptyLabel);

            if (_lastRenderedText != renderedText)
            {
                _text.text = renderedText;
                _lastRenderedText = renderedText;
            }

            _text.color = ResolveRuntimeTextColor(highlightState);
        }

        private void RefreshRuntimeVisual()
        {
            AutoAssignView();
            EnsureRuntimeVisual();
            if (!HasRuntimeVisual)
            {
                return;
            }

            var highlightState = _handCardView == null ? BattleHighlightState.None : _handCardView.HighlightState;
            _lastHighlightState = highlightState;

            var cardId = _handCardView == null ? string.Empty : _handCardView.CardId;
            var hasCard = _handCardView != null && _handCardView.HasCard;

            if (!hasCard)
            {
                _lastArtworkCardId = string.Empty;
                _runtimeCardImage.sprite = null;
                _runtimeCardImage.enabled = false;
                if (_runtimePlayableGlowImage != null)
                {
                    _runtimePlayableGlowImage.enabled = false;
                    _runtimePlayableGlowImage.color = Color.clear;
                }
                _runtimeFallbackText.enabled = false;
                _runtimeCanvasGroup.alpha = 0f;
                _runtimeCanvasGroup.blocksRaycasts = false;
                _runtimeCanvasGroup.interactable = false;
                if (_runtimeCardRect != null && _runtimeCardRect.gameObject.activeSelf)
                {
                    _runtimeCardRect.gameObject.SetActive(false);
                }
                return;
            }

            if (TryGetOpaqueRuntimeArtwork(cardId, out var artworkSprite))
            {
                if (!string.Equals(_lastArtworkCardId, cardId) || _runtimeCardImage.sprite != artworkSprite)
                {
                    _runtimeCardImage.sprite = artworkSprite;
                    _lastArtworkCardId = cardId;
                }

                _runtimeCardImage.enabled = true;
                _runtimeCardImage.color = ForceOpaque(ResolveRuntimeArtworkColor(highlightState));
                _runtimeFallbackText.enabled = false;
            }
            else
            {
                _lastArtworkCardId = string.Empty;
                _runtimeCardImage.sprite = null;
                _runtimeCardImage.enabled = false;
                _runtimeFallbackText.text = ResolveRuntimeFallbackLabel(cardId);
                _runtimeFallbackText.enabled = true;
                _runtimeFallbackText.color = ForceOpaque(ResolveRuntimeTextColor(highlightState));
            }

            if (_runtimeCardOutline != null)
            {
                var isPlayable = highlightState == BattleHighlightState.Playable;
                _runtimeCardOutline.enabled = isPlayable;
                _runtimeCardOutline.effectColor = _playableOutlineColor;
                _runtimeCardOutline.effectDistance = _playableOutlineDistance;
            }

            if (_runtimePlayableGlowImage != null)
            {
                var isPlayable = highlightState == BattleHighlightState.Playable;
                _runtimePlayableGlowImage.enabled = isPlayable;
                _runtimePlayableGlowImage.color = isPlayable ? _playableGlowColor : Color.clear;
            }

            _runtimeShadow.effectColor = ResolveRuntimeShadowColor(highlightState);
            _runtimeShadow.effectDistance = ResolveRuntimeShadowDistance(highlightState);
        }

        private void AutoAssignView()
        {
            if (_handCardView == null)
            {
                _handCardView = GetComponent<HandCardView>();
            }
        }

        private void SyncFromHandCardView()
        {
            if (_handCardView == null)
            {
                return;
            }

            _hasCard = _handCardView.HasCard;
            _label = string.IsNullOrWhiteSpace(_handCardView.Label)
                ? _label
                : _handCardView.Label;
        }

        private void CacheLegacyCanvas()
        {
            if (_legacyCanvas == null)
            {
                _legacyCanvas = _text != null ? _text.canvas : GetComponentInChildren<Canvas>(true);
            }

            if (_legacyCanvas != null && _legacyRaycaster == null)
            {
                _legacyRaycaster = _legacyCanvas.GetComponent<GraphicRaycaster>();
            }
        }

        private void SetLegacyCanvasVisible(bool visible)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            CacheLegacyCanvas();

            if (_legacyCanvas != null)
            {
                _legacyCanvas.enabled = visible;
            }

            if (_legacyRaycaster != null)
            {
                _legacyRaycaster.enabled = visible;
            }

            if (_text != null)
            {
                _text.enabled = visible;
            }

            if (_artworkImage != null)
            {
                _artworkImage.enabled = visible && IsValidArtworkSprite(_artworkImage.sprite);
            }
        }

        private void EnsureRuntimeVisual()
        {
            AutoAssignView();

            if (_runtimeHandRoot == null)
            {
                return;
            }

            if (HasRuntimeVisual)
            {
                if (_runtimeCardRect.parent != _runtimeHandRoot)
                {
                    _runtimeCardRect.SetParent(_runtimeHandRoot, false);
                }

                return;
            }

            var runtimeCardObject = new GameObject(
                $"{name}_RuntimeCardVisual",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup),
                typeof(GraphicRaycaster),
                typeof(Image),
                typeof(Shadow),
                typeof(Button));

            _runtimeCardRect = runtimeCardObject.GetComponent<RectTransform>();
            _runtimeCardRect.SetParent(_runtimeHandRoot, false);
            _runtimeCardRect.anchorMin = new Vector2(0.5f, 0f);
            _runtimeCardRect.anchorMax = new Vector2(0.5f, 0f);
            _runtimeCardRect.pivot = new Vector2(0.5f, 0.08f);
            _runtimeCardRect.anchoredPosition = Vector2.zero;
            _runtimeCardRect.gameObject.SetActive(false);

            _runtimeCanvasGroup = runtimeCardObject.GetComponent<CanvasGroup>();
            _runtimeCanvasGroup.alpha = 0f;
            _runtimeCanvasGroup.blocksRaycasts = false;
            _runtimeCanvasGroup.interactable = false;

            _runtimeCardCanvas = runtimeCardObject.GetComponent<Canvas>();
            _runtimeCardCanvas.overrideSorting = true;
            _runtimeCardCanvas.sortingOrder = _runtimeCardSortingBaseOrder;

            _runtimeHitboxImage = runtimeCardObject.GetComponent<Image>();
            _runtimeHitboxImage.color = new Color(1f, 1f, 1f, 0.001f);
            _runtimeHitboxImage.raycastTarget = true;
            _runtimeHitboxImage.sprite = null;
            _runtimeHitboxImage.type = Image.Type.Simple;

            _runtimeShadow = runtimeCardObject.GetComponent<Shadow>();
            _runtimeShadow.useGraphicAlpha = true;
            _runtimeShadow.effectColor = _shadowColor;
            _runtimeShadow.effectDistance = _shadowDistance;

            _runtimeButton = runtimeCardObject.GetComponent<Button>();
            _runtimeButton.transition = Selectable.Transition.None;
            _runtimeButton.targetGraphic = _runtimeHitboxImage;
            _runtimeButton.onClick.AddListener(NotifyRuntimeCardClicked);

            _runtimeDragRelay = runtimeCardObject.AddComponent<HandCardRuntimeDragRelay>();
            _runtimeDragRelay.BeginDrag = HandleRuntimeBeginDrag;
            _runtimeDragRelay.Drag = HandleRuntimeDrag;
            _runtimeDragRelay.EndDrag = HandleRuntimeEndDrag;

            var glowObject = new GameObject("PlayableGlow", typeof(RectTransform), typeof(Image));
            var glowRect = glowObject.GetComponent<RectTransform>();
            glowRect.SetParent(_runtimeCardRect, false);
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-_playableGlowPadding.x, -_playableGlowPadding.y);
            glowRect.offsetMax = new Vector2(_playableGlowPadding.x, _playableGlowPadding.y);
            glowRect.SetAsFirstSibling();

            _runtimePlayableGlowImage = glowObject.GetComponent<Image>();
            _runtimePlayableGlowImage.sprite = GetRuntimeWhiteSprite();
            _runtimePlayableGlowImage.type = Image.Type.Simple;
            _runtimePlayableGlowImage.raycastTarget = false;
            _runtimePlayableGlowImage.color = Color.clear;
            _runtimePlayableGlowImage.enabled = false;

            var artworkObject = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            var artworkRect = artworkObject.GetComponent<RectTransform>();
            artworkRect.SetParent(_runtimeCardRect, false);
            artworkRect.anchorMin = Vector2.zero;
            artworkRect.anchorMax = Vector2.one;
            artworkRect.offsetMin = Vector2.zero;
            artworkRect.offsetMax = Vector2.zero;

            _runtimeCardImage = artworkObject.GetComponent<Image>();
            _runtimeCardImage.preserveAspect = true;
            _runtimeCardImage.raycastTarget = false;
            _runtimeCardImage.color = ForceOpaque(_artworkNormalColor);

            _runtimeCardOutline = artworkObject.AddComponent<Outline>();
            _runtimeCardOutline.effectColor = _playableOutlineColor;
            _runtimeCardOutline.effectDistance = _playableOutlineDistance;
            _runtimeCardOutline.useGraphicAlpha = true;
            _runtimeCardOutline.enabled = false;

            var fallbackTextObject = new GameObject("FallbackText", typeof(RectTransform), typeof(Text));
            var fallbackRect = fallbackTextObject.GetComponent<RectTransform>();
            fallbackRect.SetParent(_runtimeCardRect, false);
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = new Vector2(18f, 24f);
            fallbackRect.offsetMax = new Vector2(-18f, -24f);

            _runtimeFallbackText = fallbackTextObject.GetComponent<Text>();
            _runtimeFallbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _runtimeFallbackText.alignment = TextAnchor.MiddleCenter;
            _runtimeFallbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _runtimeFallbackText.verticalOverflow = VerticalWrapMode.Truncate;
            _runtimeFallbackText.fontSize = 28;
            _runtimeFallbackText.supportRichText = false;
            _runtimeFallbackText.color = _normalColor;
            _runtimeFallbackText.raycastTarget = false;
            _runtimeFallbackText.enabled = false;
        }

        private void EnsureArtworkImage()
        {
            if (_artworkImage != null)
            {
                ConfigureArtworkImage(_artworkImage);
                return;
            }

            var existingTransform = transform.Find("CardArtworkImage");
            if (existingTransform != null)
            {
                _artworkImage = existingTransform.GetComponent<Image>();
                ConfigureArtworkImage(_artworkImage);
                return;
            }

            if (!TryGetComponent<RectTransform>(out var rootRectTransform))
            {
                return;
            }

            var artworkObject = new GameObject("CardArtworkImage", typeof(RectTransform), typeof(Image));
            var artworkRectTransform = artworkObject.GetComponent<RectTransform>();
            artworkRectTransform.SetParent(rootRectTransform, false);
            artworkRectTransform.anchorMin = Vector2.zero;
            artworkRectTransform.anchorMax = Vector2.one;
            artworkRectTransform.pivot = new Vector2(0.5f, 0.5f);
            artworkRectTransform.offsetMin = new Vector2(_artworkPadding, _artworkPadding);
            artworkRectTransform.offsetMax = new Vector2(-_artworkPadding, -_artworkPadding);
            artworkRectTransform.SetAsFirstSibling();

            _artworkImage = artworkObject.GetComponent<Image>();
            ConfigureArtworkImage(_artworkImage);
        }

        private void ConfigureArtworkImage(Image artworkImage)
        {
            if (artworkImage == null)
            {
                return;
            }

            artworkImage.enabled = IsValidArtworkSprite(artworkImage.sprite);
            artworkImage.raycastTarget = false;
            artworkImage.preserveAspect = true;

            if (artworkImage.rectTransform != null)
            {
                artworkImage.rectTransform.offsetMin = new Vector2(_artworkPadding, _artworkPadding);
                artworkImage.rectTransform.offsetMax = new Vector2(-_artworkPadding, -_artworkPadding);
            }
        }

        private void CacheBaseScale()
        {
            if (_text != null)
            {
                _textBaseScale = _text.rectTransform.localScale;
            }

            if (_artworkImage != null)
            {
                _artworkBaseScale = _artworkImage.rectTransform.localScale;
            }
        }

        private void RefreshArtwork()
        {
            EnsureArtworkImage();

            if (_artworkImage == null)
            {
                return;
            }

            var cardId = _handCardView == null ? string.Empty : _handCardView.CardId;
            var hasCard = _handCardView != null && _handCardView.HasCard;

            if (!hasCard || !CardArtworkLibrary.TryGetArtwork(cardId, out var artworkSprite))
            {
                _lastArtworkCardId = string.Empty;
                _artworkImage.sprite = null;
                _artworkImage.enabled = false;
                _artworkImage.rectTransform.localScale = _artworkBaseScale;
                return;
            }

            if (!string.Equals(_lastArtworkCardId, cardId) || _artworkImage.sprite != artworkSprite)
            {
                _artworkImage.sprite = artworkSprite;
                _lastArtworkCardId = cardId;
            }

            var highlightState = _handCardView == null ? BattleHighlightState.None : _handCardView.HighlightState;
            _artworkImage.color = highlightState == BattleHighlightState.Selected
                ? _artworkSelectedColor
                : highlightState == BattleHighlightState.Playable
                    ? _artworkPlayableColor
                    : _artworkNormalColor;
            _artworkImage.enabled = true;
        }

        private Color ResolveRuntimeArtworkColor(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _artworkSelectedColor,
                BattleHighlightState.Playable => _artworkPlayableColor,
                _ => _artworkNormalColor,
            };
        }

        private Color ResolveRuntimeTextColor(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _selectedColor,
                BattleHighlightState.Playable => _playableColor,
                _ => _normalColor,
            };
        }

        private Color ResolveRuntimeShadowColor(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _selectedShadowColor,
                BattleHighlightState.Playable => _playableShadowColor,
                _ => _shadowColor,
            };
        }

        private Vector2 ResolveRuntimeShadowDistance(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _selectedShadowDistance,
                BattleHighlightState.Playable => _playableShadowDistance,
                _ => _shadowDistance,
            };
        }

        private void ApplyRuntimeAnimation()
        {
            if (!HasRuntimeVisual || !_runtimeCardRect.gameObject.activeSelf)
            {
                return;
            }

            if (_isRuntimeDragActive)
            {
                _runtimeCardRect.anchoredPosition = _runtimeDragAnchoredPosition;
                _runtimeCardRect.localRotation = Quaternion.identity;
                _runtimeCardRect.localScale = Vector3.one * _runtimeDragAbsoluteScale;
                _runtimeCanvasGroup.alpha = 1f;
                _runtimeCanvasGroup.blocksRaycasts = false;
                _runtimeCanvasGroup.interactable = false;
                _targetRuntimeSortingOrder = _runtimeDraggedCardSortingBaseOrder + Mathf.Max(0, _handCardView?.SlotIndex ?? 0);
                ApplyRuntimeSortingOrder();
                return;
            }

            var lerpT = Mathf.Clamp01(Time.unscaledDeltaTime * _layoutLerpSpeed);

            _runtimeCardRect.sizeDelta = Vector2.Lerp(_runtimeCardRect.sizeDelta, _targetRuntimeSize, lerpT);
            _runtimeCardRect.anchoredPosition = Vector2.Lerp(_runtimeCardRect.anchoredPosition, _targetRuntimeAnchoredPosition, lerpT);
            _runtimeCardRect.localRotation = Quaternion.Slerp(
                _runtimeCardRect.localRotation,
                Quaternion.Euler(0f, 0f, _targetRuntimeRotationDegrees),
                lerpT);
            _runtimeCardRect.localScale = Vector3.Lerp(
                _runtimeCardRect.localScale,
                Vector3.one * _targetRuntimeScale,
                lerpT);

            ApplyRuntimeSortingOrder();
            var targetAlpha = _targetRuntimeVisible && _hasCard ? 1f : 0f;
            _runtimeCanvasGroup.alpha = targetAlpha;
            _runtimeCanvasGroup.blocksRaycasts = targetAlpha > 0.95f;
            _runtimeCanvasGroup.interactable = targetAlpha > 0.95f;
        }

        private int ResolveRuntimeSortingOrder(int siblingIndex)
        {
            var normalizedSiblingIndex = Mathf.Max(0, siblingIndex);
            var highlightState = _handCardView == null ? BattleHighlightState.None : _handCardView.HighlightState;
            var baseOrder = highlightState == BattleHighlightState.Selected
                ? _runtimeSelectedCardSortingBaseOrder
                : _runtimeCardSortingBaseOrder;
            return baseOrder + normalizedSiblingIndex;
        }

        private void ApplyRuntimeSortingOrder()
        {
            if (_runtimeCardCanvas == null)
            {
                _runtimeCardCanvas = _runtimeCardRect == null ? null : _runtimeCardRect.GetComponent<Canvas>();
            }

            if (_runtimeCardCanvas == null)
            {
                return;
            }

            _runtimeCardCanvas.overrideSorting = true;
            _runtimeCardCanvas.sortingOrder = _targetRuntimeSortingOrder;
        }

        private void ApplyPulse()
        {
            var shouldPulse = _lastHighlightState == BattleHighlightState.Selected;

            if (_text != null)
            {
                var textTargetScale = _textBaseScale;
                if (_text.enabled && shouldPulse && !IsArtworkVisible)
                {
                    var pulse = 1f + (Mathf.Sin(Time.unscaledTime * _pulseSpeed) * (_pulseScale - 1f));
                    textTargetScale = _textBaseScale * pulse;
                }

                _text.rectTransform.localScale = Vector3.Lerp(
                    _text.rectTransform.localScale,
                    textTargetScale,
                    Time.unscaledDeltaTime * 14f);
            }

            if (_artworkImage == null)
            {
                return;
            }

            var artworkTargetScale = _artworkBaseScale;
            if (IsArtworkVisible && shouldPulse)
            {
                var pulse = 1f + (Mathf.Sin(Time.unscaledTime * _pulseSpeed) * (_pulseScale - 1f));
                artworkTargetScale = _artworkBaseScale * pulse;
            }

            _artworkImage.rectTransform.localScale = Vector3.Lerp(
                _artworkImage.rectTransform.localScale,
                artworkTargetScale,
                Time.unscaledDeltaTime * 14f);
        }

        private void NotifyRuntimeCardClicked()
        {
            if (_suppressNextRuntimeClick)
            {
                _suppressNextRuntimeClick = false;
                return;
            }

            _handCardView?.NotifyClicked();
        }

        private void HandleRuntimeBeginDrag(PointerEventData eventData)
        {
            if (_handCardView == null || !_handCardView.HasCard)
            {
                return;
            }

            _isRuntimeDragActive = true;
            UpdateRuntimeDragPosition(eventData.position);
            _handCardView.NotifyDragStarted(eventData.position);
        }

        private void HandleRuntimeDrag(PointerEventData eventData)
        {
            if (!_isRuntimeDragActive || _handCardView == null)
            {
                return;
            }

            UpdateRuntimeDragPosition(eventData.position);
            _handCardView.NotifyDragged(eventData.position);
        }

        private void HandleRuntimeEndDrag(PointerEventData eventData)
        {
            _suppressNextRuntimeClick = true;

            if (_handCardView != null)
            {
                _handCardView.NotifyDragEnded(eventData.position);
            }

            _isRuntimeDragActive = false;
        }

        private void UpdateRuntimeDragPosition(Vector2 screenPosition)
        {
            if (_runtimeHandRoot == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_runtimeHandRoot, screenPosition, null, out var localPoint))
            {
                return;
            }

            _runtimeDragAnchoredPosition = localPoint;
        }

        private string ResolveRuntimeFallbackLabel(string cardId)
        {
            if (!string.IsNullOrWhiteSpace(cardId))
            {
                return cardId;
            }

            if (_hasCard && !string.IsNullOrWhiteSpace(_label))
            {
                return _label;
            }

            return _emptyLabel;
        }

        private static bool IsValidArtworkSprite(Sprite sprite)
        {
            return sprite != null && sprite.texture != null;
        }

        private static Color ForceOpaque(Color color)
        {
            color.a = 1f;
            return color;
        }

        private static bool TryGetOpaqueRuntimeArtwork(string cardId, out Sprite artworkSprite)
        {
            artworkSprite = null;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            if (OpaqueRuntimeSpriteCache.TryGetValue(cardId, out artworkSprite))
            {
                return artworkSprite != null;
            }

            if (!CardArtworkLibrary.TryGetArtwork(cardId, out var sourceSprite) || sourceSprite == null)
            {
                return false;
            }

            artworkSprite = CreateOpaqueRuntimeSprite(cardId, sourceSprite);
            if (artworkSprite == null)
            {
                return false;
            }

            OpaqueRuntimeSpriteCache[cardId] = artworkSprite;
            return true;
        }

        private static Sprite CreateOpaqueRuntimeSprite(string cardId, Sprite sourceSprite)
        {
            var sourceTexture = sourceSprite.texture;
            if (sourceTexture == null)
            {
                return null;
            }

            var copiedTexture = CopyTextureReadable(sourceTexture);
            if (copiedTexture == null)
            {
                return sourceSprite;
            }

            copiedTexture.filterMode = sourceTexture.filterMode;
            copiedTexture.wrapMode = TextureWrapMode.Clamp;

            var pixels = copiedTexture.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0)
                {
                    pixels[i].a = byte.MaxValue;
                }
            }

            copiedTexture.SetPixels32(pixels);
            copiedTexture.Apply(false, false);

            var rect = sourceSprite.rect;
            var pivot = new Vector2(
                sourceSprite.pivot.x / Mathf.Max(1f, rect.width),
                sourceSprite.pivot.y / Mathf.Max(1f, rect.height));

            var sprite = Sprite.Create(copiedTexture, rect, pivot, sourceSprite.pixelsPerUnit);
            sprite.name = $"{cardId}_OpaqueRuntime";
            return sprite;
        }

        private static Texture2D CopyTextureReadable(Texture sourceTexture)
        {
            var width = sourceTexture.width;
            var height = sourceTexture.height;

            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;

            try
            {
                Graphics.Blit(sourceTexture, renderTexture);
                RenderTexture.active = renderTexture;

                var copiedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                copiedTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                copiedTexture.Apply(false, false);
                return copiedTexture;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static Sprite GetRuntimeWhiteSprite()
        {
            if (s_runtimeWhiteSprite != null)
            {
                return s_runtimeWhiteSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "HandRuntimeWhite"
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            s_runtimeWhiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            s_runtimeWhiteSprite.name = "HandRuntimeWhite";
            return s_runtimeWhiteSprite;
        }
    }
}
