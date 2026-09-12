using TMPro;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;
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
        [SerializeField] private Color _mulliganSelectedColor = new Color(1f, 0.7f, 0.62f, 1f);
        [SerializeField] private Color _playableColor = new Color(0.7f, 1f, 0.7f, 1f);
        [SerializeField] private Color _artworkNormalColor = Color.white;
        [SerializeField] private Color _artworkSelectedColor = new Color(0.82f, 1f, 1f, 1f);
        [SerializeField] private Color _artworkMulliganSelectedColor = new Color(1f, 0.72f, 0.68f, 1f);
        [SerializeField] private Color _artworkPlayableColor = new Color(0.92f, 1f, 0.92f, 1f);
        [SerializeField] private bool _hideTextWhenArtworkVisible = true;
        [SerializeField] private float _artworkPadding = 6f;
        [SerializeField] private float _pulseScale = 1.05f;
        [SerializeField] private float _pulseSpeed = 6f;

        [Header("Runtime Hand Card")]
        [SerializeField] private float _layoutLerpSpeed = 18f;
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color _selectedShadowColor = new Color(0.24f, 0.71f, 1f, 0.78f);
        [SerializeField] private Color _mulliganSelectedShadowColor = new Color(1f, 0.18f, 0.08f, 0.9f);
        [SerializeField] private Color _playableShadowColor = new Color(0.32f, 1f, 0.32f, 0.85f);
        [SerializeField] private Vector2 _shadowDistance = new Vector2(0f, -18f);
        [SerializeField] private Vector2 _selectedShadowDistance = new Vector2(0f, -26f);
        [SerializeField] private Vector2 _playableShadowDistance = new Vector2(0f, 0f);
        [SerializeField] private Color _playableOutlineColor = new Color(0.34f, 1f, 0.34f, 0.92f);
        [SerializeField] private Color _mulliganOutlineColor = new Color(1f, 0.18f, 0.08f, 0.96f);
        [SerializeField] private Vector2 _playableOutlineDistance = new Vector2(4f, 4f);
        [SerializeField] private Color _playableGlowColor = new Color(0.18f, 1f, 0.18f, 0.34f);
        [SerializeField] private Color _mulliganGlowColor = new Color(1f, 0.12f, 0.04f, 0.38f);
        [SerializeField] private Vector2 _playableGlowPadding = new Vector2(16f, 20f);
        [SerializeField] private int _runtimeCardSortingBaseOrder = 1200;
        [SerializeField] private int _runtimeSelectedCardSortingBaseOrder = 4200;
        [SerializeField] private int _runtimeDraggedCardSortingBaseOrder = 4600;
        [SerializeField] private float _runtimeDragAbsoluteScale = 0.4f;
        [SerializeField] private bool _showRuntimeStatOverlay = true;
        [SerializeField] private RectTransform _runtimeStatOverlayTemplate;
        [SerializeField] private Text _runtimeStatAttackTemplateText;
        [SerializeField] private Text _runtimeStatHpTemplateText;
        [SerializeField] private Color _runtimeStatOverlayBackgroundColor = new Color(0.03f, 0.025f, 0.02f, 0.92f);
        [SerializeField] private Color _runtimeStatOverlayTextColor = new Color(1f, 0.9f, 0.58f, 1f);
        [SerializeField] private int _runtimeStatOverlayFontSize = 34;

        [Header("Runtime Hand Card Stat Layout")]
        [SerializeField] private Vector2 _runtimeStatAttackNormalizedPosition = HandCardStatOverlayLayout.DefaultAttackPosition;
        [SerializeField] private Vector2 _runtimeStatHpNormalizedPosition = HandCardStatOverlayLayout.DefaultHpPosition;
        [SerializeField, Min(1f)] private float _runtimeStatReferenceCardHeight = 250f;

        private const string CardDefinitionJsonResourcePath = "Project333/Data/cards";
        private const int MinimumRuntimeCardSortingBaseOrder = 1200;
        private static readonly Dictionary<string, Sprite> OpaqueRuntimeSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static ICardDefinitionProvider s_runtimeCardDefinitionProvider;
        private static bool s_runtimeCardDefinitionProviderLoadAttempted;
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

        [SerializeField, HideInInspector] private RectTransform _runtimeHandRoot;
        [SerializeField, HideInInspector] private RectTransform _runtimeCardRect;
        [SerializeField, HideInInspector] private Canvas _runtimeCardCanvas;
        [SerializeField, HideInInspector] private CanvasGroup _runtimeCanvasGroup;
        [SerializeField, HideInInspector] private Image _runtimeHitboxImage;
        [SerializeField, HideInInspector] private Image _runtimePlayableGlowImage;
        [SerializeField, HideInInspector] private Image _runtimeCardImage;
        private HandCardEdgeGlow _runtimeEdgeGlow;
        [SerializeField, HideInInspector] private RectTransform _runtimeStatOverlayRect;
        [SerializeField, HideInInspector] private RectTransform _runtimeStatAttackTextRect;
        [SerializeField, HideInInspector] private RectTransform _runtimeStatHpTextRect;
        [SerializeField, HideInInspector] private Image _runtimeStatOverlayBackgroundImage;
        [SerializeField, HideInInspector] private Outline _runtimeCardOutline;
        [SerializeField, HideInInspector] private Shadow _runtimeShadow;
        [SerializeField, HideInInspector] private Button _runtimeButton;
        [SerializeField, HideInInspector] private HandCardRuntimeDragRelay _runtimeDragRelay;
        [SerializeField, HideInInspector] private Text _runtimeFallbackText;
        [SerializeField, HideInInspector] private Text _runtimeStatAttackText;
        [SerializeField, HideInInspector] private Text _runtimeStatHpText;
        private bool _createdRuntimeVisualAtRuntime;

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
#if UNITY_EDITOR
        private bool _editorStatOverlayTemplateEnsureQueued;
#endif

        private bool HasRuntimeVisual => _runtimeCardRect != null;

        private bool IsArtworkVisible => _artworkImage != null &&
                                         _artworkImage.enabled &&
                                         _artworkImage.sprite != null;

        private void Awake()
        {
            AutoAssignView();
            AutoAssignRuntimeStatOverlayTemplate();
            CacheLegacyCanvas();
            EnsureArtworkImage();
            CacheBaseScale();
        }

        private void OnValidate()
        {
            AutoAssignView();
            AutoAssignRuntimeStatOverlayTemplate();
            CacheBaseScale();
#if UNITY_EDITOR
            ScheduleEnsureEditableRuntimeStatOverlayTemplate();
#endif
        }

        private void LateUpdate()
        {
            Refresh();

            if (HasRuntimeVisual)
            {
                ApplyRuntimeAnimation();
                RefreshRuntimeEdgeGlow();
                return;
            }

            ApplyPulse();
        }

        private void OnDestroy()
        {
            if (_createdRuntimeVisualAtRuntime && _runtimeCardRect != null)
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

        [ContextMenu("Ensure Editable Runtime Stat Overlay Template")]
        public void EnsureEditableRuntimeStatOverlayTemplate()
        {
            if (transform.Find("RuntimeStatOverlayTemplate") is RectTransform existingTemplate)
            {
                _runtimeStatOverlayTemplate = existingTemplate;
                ConvertLegacyRuntimeStatOverlayTemplate(existingTemplate);
                EnsureEditableRuntimeStatNumberTemplates(existingTemplate);
                return;
            }

            _runtimeStatOverlayTemplate = null;

            var templateObject = new GameObject("RuntimeStatOverlayTemplate", typeof(RectTransform), typeof(Image));
            var templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.SetParent(transform, false);
            ApplyDefaultRuntimeStatOverlayRect(templateRect);

            var templateImage = templateObject.GetComponent<Image>();
            templateImage.sprite = GetRuntimeWhiteSprite();
            templateImage.type = Image.Type.Simple;
            templateImage.raycastTarget = false;
            templateImage.color = Color.clear;

            _runtimeStatOverlayTemplate = templateRect;
            EnsureEditableRuntimeStatNumberTemplates(templateRect);

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(templateObject, "Create Runtime Stat Overlay Template");
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif

            Debug.Log($"Created RuntimeStatOverlayTemplate under {name}.", this);
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
                if (_runtimeEdgeGlow != null)
                {
                    _runtimeEdgeGlow.Present(_runtimeCardImage, false);
                }
                _runtimeFallbackText.enabled = false;
                SetRuntimeStatOverlayVisible(false);
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

            RefreshRuntimeStatOverlay(cardId);

            if (_runtimeCardOutline != null)
            {
                var isPlayable = highlightState == BattleHighlightState.Playable;
                var isMulliganSelected = highlightState == BattleHighlightState.MulliganSelected;
                _runtimeCardOutline.enabled = isMulliganSelected;
                _runtimeCardOutline.effectColor = isMulliganSelected
                    ? _mulliganOutlineColor
                    : _playableOutlineColor;
                _runtimeCardOutline.effectDistance = _playableOutlineDistance;
            }

            if (_runtimePlayableGlowImage != null)
            {
                var isPlayable = highlightState == BattleHighlightState.Playable;
                var isMulliganSelected = highlightState == BattleHighlightState.MulliganSelected;
                _runtimePlayableGlowImage.enabled = isMulliganSelected;
                _runtimePlayableGlowImage.color = isMulliganSelected
                    ? _mulliganGlowColor
                    : isPlayable
                        ? _playableGlowColor
                        : Color.clear;
            }

            RefreshRuntimeEdgeGlow();

            _runtimeShadow.effectColor = ResolveRuntimeShadowColor(highlightState);
            _runtimeShadow.effectDistance = ResolveRuntimeShadowDistance(highlightState);
        }

        private void RefreshRuntimeEdgeGlow()
        {
            if (_runtimeCardImage == null)
                return;

            var visible = _handCardView != null && _handCardView.HasCard &&
                          _handCardView.IsPlayable;
            if (_runtimeEdgeGlow == null && visible)
            {
                var existing = _runtimeCardImage.transform.Find("PlayableEdgeGlow");
                var glow = existing != null ? existing.gameObject :
                    new GameObject("PlayableEdgeGlow", typeof(RectTransform), typeof(Image));
                glow.transform.SetParent(_runtimeCardImage.transform, false);
                _runtimeEdgeGlow = new HandCardEdgeGlow(glow.GetComponent<Image>());
            }
            if (_runtimeEdgeGlow != null)
                _runtimeEdgeGlow.Present(_runtimeCardImage, visible);
        }

        private void AutoAssignView()
        {
            if (_handCardView == null)
            {
                _handCardView = GetComponent<HandCardView>();
            }
        }

        private void AutoAssignRuntimeStatOverlayTemplate()
        {
            if (_runtimeStatOverlayTemplate == null &&
                transform.Find("RuntimeStatOverlayTemplate") is RectTransform templateRect)
            {
                _runtimeStatOverlayTemplate = templateRect;
            }

            if (_runtimeStatOverlayTemplate != null)
            {
                if (_runtimeStatAttackTemplateText == null &&
                    _runtimeStatOverlayTemplate.Find("AttackValueText") is RectTransform attackTextRect)
                {
                    _runtimeStatAttackTemplateText = attackTextRect.GetComponent<Text>();
                }

                if (_runtimeStatHpTemplateText == null &&
                    _runtimeStatOverlayTemplate.Find("HpValueText") is RectTransform hpTextRect)
                {
                    _runtimeStatHpTemplateText = hpTextRect.GetComponent<Text>();
                }
            }
        }

#if UNITY_EDITOR
        private void ScheduleEnsureEditableRuntimeStatOverlayTemplate()
        {
            if (UnityEngine.Application.isPlaying || _editorStatOverlayTemplateEnsureQueued)
            {
                return;
            }

            _editorStatOverlayTemplateEnsureQueued = true;
            UnityEditor.EditorApplication.delayCall += EnsureEditableRuntimeStatOverlayTemplateDelayed;
        }

        private void EnsureEditableRuntimeStatOverlayTemplateDelayed()
        {
            _editorStatOverlayTemplateEnsureQueued = false;

            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            EnsureEditableRuntimeStatOverlayTemplate();
        }
#endif

        private void ApplyRuntimeStatOverlayTemplate(
            RectTransform runtimeOverlayRect,
            Image runtimeOverlayImage,
            RectTransform runtimeAttackTextRect,
            Text runtimeAttackText,
            RectTransform runtimeHpTextRect,
            Text runtimeHpText)
        {
            AutoAssignRuntimeStatOverlayTemplate();

            if (_runtimeStatOverlayTemplate == null)
            {
                ApplyDefaultRuntimeStatOverlayRect(runtimeOverlayRect);
                if (runtimeOverlayImage != null)
                {
                    runtimeOverlayImage.sprite = GetRuntimeWhiteSprite();
                    runtimeOverlayImage.type = Image.Type.Simple;
                    runtimeOverlayImage.color = Color.clear;
                }

                ApplyDefaultRuntimeStatNumberRect(runtimeAttackTextRect, isHp: false);
                ApplyDefaultRuntimeStatNumberStyle(runtimeAttackText);
                ApplyDefaultRuntimeStatNumberRect(runtimeHpTextRect, isHp: true);
                ApplyDefaultRuntimeStatNumberStyle(runtimeHpText);
                return;
            }

            CopyRectTransform(_runtimeStatOverlayTemplate, runtimeOverlayRect);

            var templateImage = _runtimeStatOverlayTemplate.GetComponent<Image>();
            if (runtimeOverlayImage != null)
            {
                runtimeOverlayImage.sprite = templateImage != null && templateImage.sprite != null
                    ? templateImage.sprite
                    : GetRuntimeWhiteSprite();
                runtimeOverlayImage.type = templateImage != null ? templateImage.type : Image.Type.Simple;
                runtimeOverlayImage.color = templateImage != null ? templateImage.color : Color.clear;
                runtimeOverlayImage.preserveAspect = templateImage != null && templateImage.preserveAspect;
                runtimeOverlayImage.raycastTarget = false;
            }

            if (_runtimeStatAttackTemplateText == null)
            {
                ApplyDefaultRuntimeStatNumberRect(runtimeAttackTextRect, isHp: false);
                ApplyDefaultRuntimeStatNumberStyle(runtimeAttackText);
            }
            else
            {
                CopyRectTransform(_runtimeStatAttackTemplateText.rectTransform, runtimeAttackTextRect);
                CopyTextStyle(_runtimeStatAttackTemplateText, runtimeAttackText);
            }

            if (_runtimeStatHpTemplateText == null)
            {
                ApplyDefaultRuntimeStatNumberRect(runtimeHpTextRect, isHp: true);
                ApplyDefaultRuntimeStatNumberStyle(runtimeHpText);
            }
            else
            {
                CopyRectTransform(_runtimeStatHpTemplateText.rectTransform, runtimeHpTextRect);
                CopyTextStyle(_runtimeStatHpTemplateText, runtimeHpText);
            }
        }

        private void ConvertLegacyRuntimeStatOverlayTemplate(RectTransform templateRect)
        {
            if (templateRect == null)
            {
                return;
            }

            var templateImage = templateRect.GetComponent<Image>();
            if (templateImage != null)
            {
                templateImage.color = Color.clear;
                templateImage.raycastTarget = false;
            }

            if (templateRect.Find("StatOverlayText") is RectTransform legacyTextRect)
            {
                ApplyDefaultRuntimeStatOverlayRect(templateRect);
                legacyTextRect.gameObject.SetActive(false);
            }
        }

        private void EnsureEditableRuntimeStatNumberTemplates(RectTransform templateRect)
        {
            if (templateRect == null)
            {
                return;
            }

            if (_runtimeStatAttackTemplateText == null &&
                templateRect.Find("AttackValueText") is RectTransform attackTextRect)
            {
                _runtimeStatAttackTemplateText = attackTextRect.GetComponent<Text>();
            }

            if (_runtimeStatHpTemplateText == null &&
                templateRect.Find("HpValueText") is RectTransform hpTextRect)
            {
                _runtimeStatHpTemplateText = hpTextRect.GetComponent<Text>();
            }

            if (_runtimeStatAttackTemplateText == null)
            {
                _runtimeStatAttackTemplateText = CreateEditableRuntimeStatNumberText(templateRect, "AttackValueText", "0", isHp: false);
            }

            if (_runtimeStatHpTemplateText == null)
            {
                _runtimeStatHpTemplateText = CreateEditableRuntimeStatNumberText(templateRect, "HpValueText", "0", isHp: true);
            }
        }

        private Text CreateEditableRuntimeStatNumberText(RectTransform templateRect, string objectName, string text, bool isHp)
        {
            var templateTextObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var templateTextRect = templateTextObject.GetComponent<RectTransform>();
            templateTextRect.SetParent(templateRect, false);
            ApplyDefaultRuntimeStatNumberRect(templateTextRect, isHp);

            var templateText = templateTextObject.GetComponent<Text>();
            ApplyDefaultRuntimeStatNumberStyle(templateText);
            templateText.text = text;

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(templateTextObject, $"Create {objectName}");
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif

            return templateText;
        }

        private void ApplyDefaultRuntimeStatOverlayRect(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            target.localScale = Vector3.one;
            target.localRotation = Quaternion.identity;
        }

        private void ApplyDefaultRuntimeStatNumberRect(RectTransform target, bool isHp)
        {
            if (target == null)
            {
                return;
            }

            var anchor = isHp
                ? new Vector2(0.895f, 0.085f)
                : new Vector2(0.115f, 0.085f);
            target.anchorMin = anchor;
            target.anchorMax = anchor;
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = Vector2.zero;
            target.sizeDelta = new Vector2(70f, 54f);
            target.localScale = Vector3.one;
            target.localRotation = Quaternion.identity;
        }

        private void ApplyDefaultRuntimeStatNumberStyle(Text target)
        {
            if (target == null)
            {
                return;
            }

            target.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            target.alignment = TextAnchor.MiddleCenter;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.verticalOverflow = VerticalWrapMode.Truncate;
            target.fontSize = _runtimeStatOverlayFontSize;
            target.resizeTextForBestFit = false;
            target.resizeTextMinSize = 10;
            target.resizeTextMaxSize = _runtimeStatOverlayFontSize;
            target.supportRichText = false;
            target.color = _runtimeStatOverlayTextColor;
            target.raycastTarget = false;
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }

        private static void CopyTextStyle(Text source, Text target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.font = source.font != null
                ? source.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            target.alignment = source.alignment;
            target.horizontalOverflow = source.horizontalOverflow;
            target.verticalOverflow = source.verticalOverflow;
            target.fontSize = source.fontSize;
            target.resizeTextForBestFit = false;
            target.resizeTextMinSize = source.resizeTextMinSize;
            target.resizeTextMaxSize = source.resizeTextMaxSize;
            target.fontStyle = source.fontStyle;
            target.lineSpacing = source.lineSpacing;
            target.supportRichText = source.supportRichText;
            target.color = source.color;
            target.raycastTarget = false;
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

                BindRuntimeInteractionCallbacks();
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
            _createdRuntimeVisualAtRuntime = UnityEngine.Application.isPlaying;

            _runtimeCanvasGroup = runtimeCardObject.GetComponent<CanvasGroup>();
            _runtimeCanvasGroup.alpha = 0f;
            _runtimeCanvasGroup.blocksRaycasts = false;
            _runtimeCanvasGroup.interactable = false;

            _runtimeCardCanvas = runtimeCardObject.GetComponent<Canvas>();
            _runtimeCardCanvas.overrideSorting = true;
            _runtimeCardCanvas.sortingOrder = ResolveRuntimeCardSortingBaseOrder();

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

            var statOverlayObject = new GameObject("StatOverlay", typeof(RectTransform), typeof(Image));
            var statOverlayRect = statOverlayObject.GetComponent<RectTransform>();
            statOverlayRect.SetParent(_runtimeCardRect, false);
            _runtimeStatOverlayRect = statOverlayRect;

            _runtimeStatOverlayBackgroundImage = statOverlayObject.GetComponent<Image>();
            _runtimeStatOverlayBackgroundImage.sprite = GetRuntimeWhiteSprite();
            _runtimeStatOverlayBackgroundImage.type = Image.Type.Simple;
            _runtimeStatOverlayBackgroundImage.raycastTarget = false;
            _runtimeStatOverlayBackgroundImage.color = _runtimeStatOverlayBackgroundColor;
            _runtimeStatOverlayBackgroundImage.enabled = false;

            var attackTextObject = new GameObject("AttackValueText", typeof(RectTransform), typeof(Text));
            _runtimeStatAttackTextRect = attackTextObject.GetComponent<RectTransform>();
            _runtimeStatAttackTextRect.SetParent(statOverlayRect, false);
            _runtimeStatAttackText = attackTextObject.GetComponent<Text>();
            _runtimeStatAttackText.raycastTarget = false;
            _runtimeStatAttackText.enabled = false;

            var hpTextObject = new GameObject("HpValueText", typeof(RectTransform), typeof(Text));
            _runtimeStatHpTextRect = hpTextObject.GetComponent<RectTransform>();
            _runtimeStatHpTextRect.SetParent(statOverlayRect, false);
            _runtimeStatHpText = hpTextObject.GetComponent<Text>();
            _runtimeStatHpText.raycastTarget = false;
            _runtimeStatHpText.enabled = false;

            ApplyRuntimeStatOverlayTemplate(
                statOverlayRect,
                _runtimeStatOverlayBackgroundImage,
                _runtimeStatAttackTextRect,
                _runtimeStatAttackText,
                _runtimeStatHpTextRect,
                _runtimeStatHpText);
            BindRuntimeInteractionCallbacks();
        }

        private void BindRuntimeInteractionCallbacks()
        {
            if (_runtimeButton != null)
            {
                _runtimeButton.onClick.RemoveListener(NotifyRuntimeCardClicked);
                _runtimeButton.onClick.AddListener(NotifyRuntimeCardClicked);
            }

            if (_runtimeDragRelay == null)
            {
                return;
            }

            _runtimeDragRelay.BeginDrag = HandleRuntimeBeginDrag;
            _runtimeDragRelay.Drag = HandleRuntimeDrag;
            _runtimeDragRelay.EndDrag = HandleRuntimeEndDrag;
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
            _artworkImage.color = ResolveRuntimeArtworkColor(highlightState);
            _artworkImage.enabled = true;
        }

        private Color ResolveRuntimeArtworkColor(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _artworkSelectedColor,
                BattleHighlightState.MulliganSelected => _artworkMulliganSelectedColor,
                BattleHighlightState.Playable => _artworkNormalColor,
                _ => _artworkNormalColor,
            };
        }

        private Color ResolveRuntimeTextColor(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _selectedColor,
                BattleHighlightState.MulliganSelected => _mulliganSelectedColor,
                BattleHighlightState.Playable => _playableColor,
                _ => _normalColor,
            };
        }

        private Color ResolveRuntimeShadowColor(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _selectedShadowColor,
                BattleHighlightState.MulliganSelected => _mulliganSelectedShadowColor,
                BattleHighlightState.Playable => _shadowColor,
                _ => _shadowColor,
            };
        }

        private Vector2 ResolveRuntimeShadowDistance(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => _selectedShadowDistance,
                BattleHighlightState.MulliganSelected => _selectedShadowDistance,
                BattleHighlightState.Playable => _shadowDistance,
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
            var baseOrder = highlightState == BattleHighlightState.Selected ||
                            highlightState == BattleHighlightState.MulliganSelected
                ? _runtimeSelectedCardSortingBaseOrder
                : ResolveRuntimeCardSortingBaseOrder();
            return baseOrder + normalizedSiblingIndex;
        }

        private int ResolveRuntimeCardSortingBaseOrder()
        {
            return Mathf.Max(MinimumRuntimeCardSortingBaseOrder, _runtimeCardSortingBaseOrder);
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
            var shouldPulse = _lastHighlightState == BattleHighlightState.Selected ||
                              _lastHighlightState == BattleHighlightState.MulliganSelected;

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

        private void RefreshRuntimeStatOverlay(string cardId)
        {
            if (!_showRuntimeStatOverlay ||
                string.IsNullOrWhiteSpace(cardId) ||
                !TryResolveRuntimeStats(cardId, out var stats))
            {
                SetRuntimeStatOverlayVisible(false);
                return;
            }

            ApplyRuntimeStatOverlayTemplate(
                _runtimeStatOverlayRect,
                _runtimeStatOverlayBackgroundImage,
                _runtimeStatAttackTextRect,
                _runtimeStatAttackText,
                _runtimeStatHpTextRect,
                _runtimeStatHpText);
            RefreshRuntimeStatLayout();

            if (_runtimeStatAttackText != null)
            {
                _runtimeStatAttackText.text = stats.Attack.ToString();
            }

            if (_runtimeStatHpText != null)
            {
                _runtimeStatHpText.text = stats.HasHp ? stats.Hp.ToString() : string.Empty;
            }

            SetRuntimeStatOverlayVisible(true, stats.HasHp);
        }

        private void RefreshRuntimeStatLayout()
        {
            if (_runtimeCardImage == null ||
                _runtimeCardImage.sprite == null ||
                _runtimeStatOverlayRect == null)
            {
                return;
            }

            var artworkRectTransform = _runtimeCardImage.rectTransform;
            var renderedSpriteRect = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(_runtimeCardImage);

            PositionRuntimeStatText(
                _runtimeStatAttackText,
                artworkRectTransform,
                renderedSpriteRect,
                _runtimeStatAttackNormalizedPosition);
            PositionRuntimeStatText(
                _runtimeStatHpText,
                artworkRectTransform,
                renderedSpriteRect,
                _runtimeStatHpNormalizedPosition);

            var renderedCardHeight = Mathf.Max(1f, renderedSpriteRect.height);
            ScaleRuntimeStatFont(
                _runtimeStatAttackText,
                _runtimeStatAttackTemplateText,
                renderedCardHeight);
            ScaleRuntimeStatFont(
                _runtimeStatHpText,
                _runtimeStatHpTemplateText,
                renderedCardHeight);
        }

        private static void PositionRuntimeStatText(Text text, RectTransform artworkRectTransform,
            Rect renderedSpriteRect, Vector2 normalizedSpritePosition)
        {
            HandCardStatOverlayLayout.PositionStatText(text?.rectTransform, artworkRectTransform,
                renderedSpriteRect, normalizedSpritePosition);
        }

        private void ScaleRuntimeStatFont(Text runtimeText, Text templateText, float renderedCardHeight)
        {
            if (runtimeText == null)
            {
                return;
            }

            var templateFontSize = templateText != null
                ? templateText.fontSize
                : _runtimeStatOverlayFontSize;
            var scaledFontSize = HandCardStatOverlayLayout.CalculateScaledFontSize(
                templateFontSize,
                renderedCardHeight,
                _runtimeStatReferenceCardHeight);

            runtimeText.fontSize = scaledFontSize;
            runtimeText.resizeTextForBestFit = false;
            runtimeText.resizeTextMaxSize = scaledFontSize;
        }

        private void SetRuntimeStatOverlayVisible(bool visible, bool showHp = true)
        {
            if (_runtimeStatOverlayBackgroundImage != null)
            {
                _runtimeStatOverlayBackgroundImage.enabled = false;
            }

            if (_runtimeStatAttackText != null)
            {
                _runtimeStatAttackText.enabled = visible;
            }

            if (_runtimeStatHpText != null)
            {
                _runtimeStatHpText.enabled = visible && showHp;
            }
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

        private bool TryResolveRuntimeStats(string cardId, out CardStatDisplay stats)
        {
            stats = default;

            var provider = GetRuntimeCardDefinitionProvider();
            if (provider == null || string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            try
            {
                return CardStatDisplay.TryCreate(provider.GetRequired(cardId),
                    AccountSessionState.GetOwnedCardUpgradeLevel(cardId), out stats,
                    _handCardView?.SpellPower ?? 0);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static ICardDefinitionProvider GetRuntimeCardDefinitionProvider()
        {
            if (s_runtimeCardDefinitionProviderLoadAttempted)
            {
                return s_runtimeCardDefinitionProvider;
            }

            s_runtimeCardDefinitionProviderLoadAttempted = true;

            var jsonAsset = Resources.Load<TextAsset>(CardDefinitionJsonResourcePath);
            if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
            {
                return null;
            }

            try
            {
                var validation = CardDatabaseValidator.ValidateJson(jsonAsset.text);
                if (!validation.IsValid)
                {
                    Debug.LogWarning(
                        $"Hand card stat overlay definitions failed validation from Resources/{CardDefinitionJsonResourcePath}: {string.Join("; ", validation.Errors)}");
                    return null;
                }

                var database = JsonCardDefinitionDatabase.FromJson(jsonAsset.text);
                s_runtimeCardDefinitionProvider = database.CreateProvider();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load hand card stat overlay definitions from Resources/{CardDefinitionJsonResourcePath}: {exception.Message}");
            }

            return s_runtimeCardDefinitionProvider;
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

    /// <summary>A cached, hollow halo following the artwork's exterior alpha contour.</summary>
    internal sealed class HandCardEdgeGlow
    {
        private const int MaskResolution = 384;
        private const int Padding = 24;
        private static readonly Dictionary<Sprite, Sprite> Cache = new Dictionary<Sprite, Sprite>();
        private readonly Image _image;
        private Sprite _source;

        public HandCardEdgeGlow(Image image)
        {
            _image = image;
            _image.raycastTarget = false;
            _image.preserveAspect = false;
        }

        public void Present(Image artwork, bool visible)
        {
            var source = artwork != null ? artwork.overrideSprite : null;
            _image.enabled = visible && source != null && artwork.enabled;
            if (!_image.enabled)
                return;

            if (_source != source || _image.sprite == null)
            {
                _source = source;
                if (!Cache.TryGetValue(source, out var halo) || halo == null)
                {
                    halo = CreateHalo(source);
                    Cache[source] = halo;
                }
                _image.sprite = halo;
            }

            // Inherit rotation and drag scale, and fit the actual aspect-preserved image.
            var rect = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(artwork);
            var haloRect = _image.rectTransform;
            haloRect.anchorMin = haloRect.anchorMax = artwork.rectTransform.pivot;
            haloRect.pivot = Vector2.one * 0.5f;
            haloRect.anchoredPosition = rect.center;
            var textureSize = _image.sprite.rect.size;
            haloRect.sizeDelta = new Vector2(
                rect.width * textureSize.x / (textureSize.x - 2 * Padding),
                rect.height * textureSize.y / (textureSize.y - 2 * Padding));
            var intensity = 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 2.2f);
            _image.color = new Color(1f, 1f, 1f, intensity);
        }

        private static Sprite CreateHalo(Sprite source)
        {
            var scale = MaskResolution / Mathf.Max(source.rect.width, source.rect.height);
            var contentWidth = Mathf.Max(1, Mathf.RoundToInt(source.rect.width * scale));
            var contentHeight = Mathf.Max(1, Mathf.RoundToInt(source.rect.height * scale));
            var width = contentWidth + 2 * Padding;
            var height = contentHeight + 2 * Padding;
            var pixels = ReadAlpha(source, contentWidth, contentHeight);
            var solid = new bool[width * height];
            for (var y = 0; y < contentHeight; y++)
                for (var x = 0; x < contentWidth; x++)
                    solid[(y + Padding) * width + x + Padding] = pixels[y * contentWidth + x].a >= 32;

            // Flood only the exterior. Transparent holes inside the artwork never glow.
            var exterior = new bool[solid.Length];
            var queue = new int[solid.Length];
            var head = 0;
            var tail = 1;
            queue[0] = 0;
            exterior[0] = true;
            while (head < tail)
            {
                var index = queue[head++];
                var x = index % width;
                var y = index / width;
                if (x > 0) Visit(index - 1);
                if (x < width - 1) Visit(index + 1);
                if (y > 0) Visit(index - width);
                if (y < height - 1) Visit(index + width);
            }

            void Visit(int index)
            {
                if (solid[index] || exterior[index]) return;
                exterior[index] = true;
                queue[tail++] = index;
            }

            // Two-pass chamfer distance keeps generation linear in the mask size.
            var distance = new float[solid.Length];
            for (var i = 0; i < distance.Length; i++)
                distance[i] = exterior[i] ? 10000f : 0f;
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    if (x > 0) distance[i] = Mathf.Min(distance[i], distance[i - 1] + 1f);
                    if (y == 0) continue;
                    distance[i] = Mathf.Min(distance[i], distance[i - width] + 1f);
                    if (x > 0) distance[i] = Mathf.Min(distance[i], distance[i - width - 1] + 1.414214f);
                    if (x < width - 1) distance[i] = Mathf.Min(distance[i], distance[i - width + 1] + 1.414214f);
                }
            for (var y = height - 1; y >= 0; y--)
                for (var x = width - 1; x >= 0; x--)
                {
                    var i = y * width + x;
                    if (x < width - 1) distance[i] = Mathf.Min(distance[i], distance[i + 1] + 1f);
                    if (y == height - 1) continue;
                    distance[i] = Mathf.Min(distance[i], distance[i + width] + 1f);
                    if (x > 0) distance[i] = Mathf.Min(distance[i], distance[i + width - 1] + 1.414214f);
                    if (x < width - 1) distance[i] = Mathf.Min(distance[i], distance[i + width + 1] + 1.414214f);
                }

            var colors = new Color32[solid.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                var d = distance[i];
                var rim = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 3.6f, d));
                var bloom = 0.6f * Mathf.Exp(-d * d / 65f);
                var color = Color.Lerp(new Color(0.08f, 1f, 0.16f), new Color(0.7f, 1f, 0.48f), rim);
                color.a = exterior[i] ? Mathf.Max(rim, bloom) : 0f;
                colors[i] = color;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = source.name + "_EdgeGlow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            var result = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f,
                100f, 0, SpriteMeshType.FullRect);
            result.name = texture.name;
            result.hideFlags = HideFlags.HideAndDontSave;
            return result;
        }

        private static Color32[] ReadAlpha(Sprite source, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                var texture = source.texture;
                var rect = source.rect;
                Graphics.Blit(texture, target,
                    new Vector2(rect.width / texture.width, rect.height / texture.height),
                    new Vector2(rect.x / texture.width, rect.y / texture.height));
                RenderTexture.active = target;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (readable != null)
                {
                    if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(readable);
                    else UnityEngine.Object.DestroyImmediate(readable);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCache()
        {
            foreach (var halo in Cache.Values)
            {
                if (halo == null) continue;
                UnityEngine.Object.Destroy(halo.texture);
                UnityEngine.Object.Destroy(halo);
            }
            Cache.Clear();
        }
    }
}
