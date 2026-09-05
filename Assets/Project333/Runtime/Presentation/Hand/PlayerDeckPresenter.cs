using System;
using System.Collections;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Hand
{
    [DisallowMultipleComponent]
    public class PlayerDeckPresenter : MonoBehaviour
    {
        private const string DefaultCardBackResourcePath = "Project333/CardArtwork/OpponentCardBack";
        private const string PlayerSafeAreaRootName = "PlayerDeckSafeArea";
        private const string PlayerDeckRootName = "PlayerDeckRoot";
        private const string PlayerCardBackObjectPrefix = "PlayerDeckCardBack_";
        private const string TouchTargetName = "DeckTouchTarget";
        private const string TooltipPanelName = "DeckCountTooltip";
        private const string TooltipTextName = "DeckCountTooltipText";
#if UNITY_EDITOR
        private const string KoreanFontAssetPath =
            "Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static/NotoSansKR-Bold.ttf";
#endif

        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "Apple SD Gothic Neo",
            "Noto Sans CJK KR",
            "Noto Sans KR",
            "sans-serif",
        };

        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _deckRoot;
        [SerializeField] private Sprite _cardBackSprite;
        [SerializeField] private Texture2D _cardBackTexture;
        [SerializeField] private string _cardBackResourcePath = DefaultCardBackResourcePath;
        [SerializeField] private Image[] _cardBackImages = Array.Empty<Image>();
        [SerializeField] private Button _touchTarget;
        [SerializeField] private RectTransform _tooltipPanel;
        [SerializeField] private Text _tooltipText;
        [SerializeField] private Font _tooltipFont;

        [Header("Deck Layout")]
        [SerializeField] private Vector2 _cardSize = new Vector2(100f, 133f);
        [SerializeField] private float _topPadding = 28f;
        [SerializeField, HideInInspector] private int _opponentCornerLayoutVersion;
        [SerializeField] private float _rightPadding = 28f;
        [SerializeField] private float _bottomPadding = 28f;
        [SerializeField] private Vector2 _stackOffset = new Vector2(5f, 5f);
        [SerializeField] [Range(1, 5)] private int _stackLayerCount = 3;
        [SerializeField] private int _canvasSortingOrder = 40;

        [Header("Deck Count Tooltip")]
        [SerializeField] private Vector2 _tooltipSize = new Vector2(200f, 35f);
        [SerializeField] private Vector2 _tooltipOffset = new Vector2(0f, 15f);
        [SerializeField] private int _tooltipFontSize = 14;
        [SerializeField] private float _tooltipDurationSeconds = 2f;
        [SerializeField] private Color _tooltipBackgroundColor = new Color(0.025f, 0.045f, 0.065f, 0.96f);
        [SerializeField] private Color _tooltipBorderColor = new Color(0.86f, 0.68f, 0.3f, 0.95f);
        [SerializeField] private Color _tooltipTextColor = Color.white;

        [Header("Editor Preview")]
        [SerializeField] [Min(0)] private int _previewDeckCount = 33;

        private int _remainingDeckCount = 33;
        private Sprite _generatedCardBackSprite;
        private Texture2D _generatedSpriteSource;
        private Coroutine _hideTooltipCoroutine;
        private static Font s_runtimeKoreanFont;

        public int RemainingDeckCount => _remainingDeckCount;
        public bool IsTooltipVisible =>
            _tooltipPanel != null && _tooltipPanel.gameObject.activeSelf;
        public string TooltipMessage => _tooltipText == null ? string.Empty : _tooltipText.text;

        protected virtual bool PresentsOpponentDeck => false;
        protected virtual string SafeAreaRootName => PlayerSafeAreaRootName;
        protected virtual string DeckRootName => PlayerDeckRootName;
        protected virtual string CardBackObjectPrefix => PlayerCardBackObjectPrefix;
        protected virtual string TooltipSubject => "덱";
        protected virtual string GeneratedSpriteSuffix => "PlayerDeckRuntimeSprite";

        protected virtual void Awake()
        {
            EnsureVisualHierarchy();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyDeckCountVisual();
            UpdateTooltipText();
        }

        protected virtual void OnEnable()
        {
            EnsureVisualHierarchy();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyDeckCountVisual();
            UpdateTooltipText();
        }

        protected virtual void OnDisable()
        {
            HideDeckCountTooltip();
        }

        protected virtual void OnValidate()
        {
            _cardSize.x = Mathf.Max(1f, _cardSize.x);
            _cardSize.y = Mathf.Max(1f, _cardSize.y);
            _stackOffset.x = Mathf.Max(0f, _stackOffset.x);
            _stackOffset.y = Mathf.Max(0f, _stackOffset.y);
            _stackLayerCount = Mathf.Clamp(_stackLayerCount, 1, 5);
            _tooltipSize.x = Mathf.Max(1f, _tooltipSize.x);
            _tooltipSize.y = Mathf.Max(1f, _tooltipSize.y);
            _tooltipFontSize = Mathf.Max(1, _tooltipFontSize);
            _tooltipDurationSeconds = Mathf.Max(0.1f, _tooltipDurationSeconds);
            _previewDeckCount = Mathf.Max(0, _previewDeckCount);

            if (!UnityEngine.Application.isPlaying)
            {
                _remainingDeckCount = _previewDeckCount;
            }

            EnsureVisualHierarchy();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyDeckCountVisual();
            UpdateTooltipText();
        }

        protected virtual void OnDestroy()
        {
            if (_generatedCardBackSprite == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(_generatedCardBackSprite);
            }
            else
            {
                DestroyImmediate(_generatedCardBackSprite);
            }

            _generatedCardBackSprite = null;
            _generatedSpriteSource = null;
        }

        public void Present(DeckState deck)
        {
            PresentCount(deck?.Count ?? 0);
        }

        public void PresentCount(int remainingDeckCount)
        {
            _remainingDeckCount = Mathf.Max(0, remainingDeckCount);
            EnsureVisualHierarchy();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyDeckCountVisual();
            UpdateTooltipText();
        }

        public void ShowDeckCountFromUi()
        {
            EnsureVisualHierarchy();
            UpdateTooltipText();
            if (_tooltipPanel == null)
            {
                return;
            }

            _tooltipPanel.gameObject.SetActive(true);
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            if (_hideTooltipCoroutine != null)
            {
                StopCoroutine(_hideTooltipCoroutine);
            }

            _hideTooltipCoroutine = StartCoroutine(HideTooltipAfterDelay());
        }

        [ContextMenu("Ensure Editable Player Deck UI")]
        public void EnsureEditableDeckUi()
        {
            EnsureVisualHierarchy();
            ResolveCardBackSprite();
            ApplyCardBackVisuals();
            ApplyDeckCountVisual();
            UpdateTooltipText();
        }

        [ContextMenu("Preview Deck Count Tooltip")]
        private void PreviewDeckCountTooltip()
        {
            ShowDeckCountFromUi();
        }

        [ContextMenu("Hide Deck Count Tooltip")]
        private void HideDeckCountTooltip()
        {
            if (_hideTooltipCoroutine != null)
            {
                StopCoroutine(_hideTooltipCoroutine);
                _hideTooltipCoroutine = null;
            }

            if (_tooltipPanel != null)
            {
                _tooltipPanel.gameObject.SetActive(false);
            }
        }

        private IEnumerator HideTooltipAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_tooltipDurationSeconds);
            _hideTooltipCoroutine = null;
            if (_tooltipPanel != null)
            {
                _tooltipPanel.gameObject.SetActive(false);
            }
        }

        private void EnsureVisualHierarchy()
        {
            EnsureCanvas();
            EnsureDeckRoot();
            EnsureCardBackStack();
            EnsureTouchTarget();
            EnsureTooltip();
        }

        private void EnsureCanvas()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                gameObject.AddComponent<CanvasScaler>();
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _canvasSortingOrder;

            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (_canvas.transform is RectTransform canvasRect)
            {
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
            }
        }

        private void EnsureDeckRoot()
        {
            if (_canvas == null)
            {
                return;
            }

            var canvasRect = _canvas.transform as RectTransform;
            var safeAreaRoot = SafeAreaFitter.EnsureCanvasContentRoot(canvasRect, SafeAreaRootName);
            if (_deckRoot == null && safeAreaRoot != null)
            {
                _deckRoot = safeAreaRoot.Find(DeckRootName) as RectTransform;
            }

            if (_deckRoot == null && safeAreaRoot != null)
            {
                var rootObject = new GameObject(DeckRootName, typeof(RectTransform));
                _deckRoot = rootObject.GetComponent<RectTransform>();
                _deckRoot.SetParent(safeAreaRoot, false);
            }

            if (_deckRoot == null)
            {
                return;
            }

            var stackDepth = Mathf.Max(0, _stackLayerCount - 1);
            _deckRoot.sizeDelta = new Vector2(
                _cardSize.x + (_stackOffset.x * stackDepth),
                _cardSize.y + (_stackOffset.y * stackDepth));

            if (PresentsOpponentDeck)
            {
                if (_opponentCornerLayoutVersion < 1)
                {
                    // Gear: 24 top margin + 86 height, followed by a 16-unit gap.
                    _topPadding = 126f;
                    _rightPadding = 24f;
                    _opponentCornerLayoutVersion = 1;
                }
                _deckRoot.anchorMin = Vector2.one;
                _deckRoot.anchorMax = Vector2.one;
                _deckRoot.pivot = Vector2.one;
                _deckRoot.anchoredPosition = new Vector2(-_rightPadding, -_topPadding);
            }
            else
            {
                _deckRoot.anchorMin = new Vector2(1f, 0f);
                _deckRoot.anchorMax = new Vector2(1f, 0f);
                _deckRoot.pivot = new Vector2(1f, 0f);
                _deckRoot.anchoredPosition = new Vector2(-_rightPadding, _bottomPadding);
            }

            _deckRoot.localScale = Vector3.one;
            _deckRoot.localRotation = Quaternion.identity;
        }

        private void EnsureCardBackStack()
        {
            if (_deckRoot == null)
            {
                return;
            }

            if (_cardBackImages == null || _cardBackImages.Length != _stackLayerCount)
            {
                Array.Resize(ref _cardBackImages, _stackLayerCount);
            }

            for (var index = 0; index < _cardBackImages.Length; index++)
            {
                var image = _cardBackImages[index];
                if (image == null)
                {
                    var childName = $"{CardBackObjectPrefix}{index:00}";
                    var existing = _deckRoot.Find(childName);
                    image = existing == null ? null : existing.GetComponent<Image>();
                    if (image == null)
                    {
                        var cardObject = new GameObject(
                            childName,
                            typeof(RectTransform),
                            typeof(CanvasRenderer),
                            typeof(Image));
                        cardObject.transform.SetParent(_deckRoot, false);
                        image = cardObject.GetComponent<Image>();
                    }

                    _cardBackImages[index] = image;
                }

                image.raycastTarget = false;
                image.preserveAspect = true;
                image.type = Image.Type.Simple;
                image.color = Color.white;

                var depth = _cardBackImages.Length - 1 - index;
                var rectTransform = image.rectTransform;
                rectTransform.sizeDelta = _cardSize;
                if (PresentsOpponentDeck)
                {
                    rectTransform.anchorMin = Vector2.one;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.pivot = Vector2.one;
                    rectTransform.anchoredPosition = new Vector2(
                        -_stackOffset.x * depth,
                        -_stackOffset.y * depth);
                }
                else
                {
                    rectTransform.anchorMin = new Vector2(1f, 0f);
                    rectTransform.anchorMax = new Vector2(1f, 0f);
                    rectTransform.pivot = new Vector2(1f, 0f);
                    rectTransform.anchoredPosition = new Vector2(
                        -_stackOffset.x * depth,
                        _stackOffset.y * depth);
                }

                rectTransform.localScale = Vector3.one;
                rectTransform.localRotation = Quaternion.identity;
            }
        }

        private void EnsureTouchTarget()
        {
            if (_deckRoot == null)
            {
                return;
            }

            if (_touchTarget == null && _deckRoot.Find(TouchTargetName) is RectTransform existing)
            {
                _touchTarget = existing.GetComponent<Button>();
            }

            if (_touchTarget == null)
            {
                var touchObject = new GameObject(
                    TouchTargetName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                touchObject.transform.SetParent(_deckRoot, false);
                _touchTarget = touchObject.GetComponent<Button>();
            }

            var touchImage = _touchTarget.GetComponent<Image>();
            touchImage.color = new Color(1f, 1f, 1f, 0.001f);
            touchImage.raycastTarget = true;
            _touchTarget.targetGraphic = touchImage;
            _touchTarget.transition = Selectable.Transition.None;
            _touchTarget.onClick.RemoveListener(ShowDeckCountFromUi);
            _touchTarget.onClick.AddListener(ShowDeckCountFromUi);

            var touchRect = _touchTarget.transform as RectTransform;
            touchRect.anchorMin = Vector2.zero;
            touchRect.anchorMax = Vector2.one;
            touchRect.offsetMin = Vector2.zero;
            touchRect.offsetMax = Vector2.zero;
            touchRect.localScale = Vector3.one;
            touchRect.localRotation = Quaternion.identity;
        }

        private void EnsureTooltip()
        {
            if (_deckRoot == null)
            {
                return;
            }

            if (_tooltipPanel == null && _deckRoot.Find(TooltipPanelName) is RectTransform existingPanel)
            {
                _tooltipPanel = existingPanel;
            }

            var wasCreated = false;
            if (_tooltipPanel == null)
            {
                var panelObject = new GameObject(
                    TooltipPanelName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline));
                panelObject.transform.SetParent(_deckRoot, false);
                _tooltipPanel = panelObject.GetComponent<RectTransform>();
                wasCreated = true;
            }

            _tooltipPanel.sizeDelta = _tooltipSize;
            if (PresentsOpponentDeck)
            {
                _tooltipPanel.anchorMin = new Vector2(1f, 0f);
                _tooltipPanel.anchorMax = new Vector2(1f, 0f);
                _tooltipPanel.pivot = Vector2.one;
                _tooltipPanel.anchoredPosition = new Vector2(
                    _tooltipOffset.x,
                    -Mathf.Abs(_tooltipOffset.y));
            }
            else
            {
                _tooltipPanel.anchorMin = new Vector2(1f, 1f);
                _tooltipPanel.anchorMax = new Vector2(1f, 1f);
                _tooltipPanel.pivot = new Vector2(1f, 0f);
                _tooltipPanel.anchoredPosition = _tooltipOffset;
            }

            _tooltipPanel.localScale = Vector3.one;
            _tooltipPanel.localRotation = Quaternion.identity;

            var panelImage = _tooltipPanel.GetComponent<Image>();
            panelImage.color = _tooltipBackgroundColor;
            panelImage.raycastTarget = false;

            var outline = _tooltipPanel.GetComponent<Outline>();
            outline.effectColor = _tooltipBorderColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            if (_tooltipText == null && _tooltipPanel.Find(TooltipTextName) is RectTransform existingText)
            {
                _tooltipText = existingText.GetComponent<Text>();
            }

            if (_tooltipText == null)
            {
                var textObject = new GameObject(
                    TooltipTextName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text),
                    typeof(Outline));
                textObject.transform.SetParent(_tooltipPanel, false);
                _tooltipText = textObject.GetComponent<Text>();
            }

            var textRect = _tooltipText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 8f);
            textRect.offsetMax = new Vector2(-14f, -8f);
            textRect.localScale = Vector3.one;
            textRect.localRotation = Quaternion.identity;

            _tooltipText.font = ResolveTooltipFont();
            _tooltipText.fontSize = _tooltipFontSize;
            _tooltipText.fontStyle = FontStyle.Bold;
            _tooltipText.alignment = TextAnchor.MiddleCenter;
            _tooltipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _tooltipText.verticalOverflow = VerticalWrapMode.Truncate;
            _tooltipText.color = _tooltipTextColor;
            _tooltipText.raycastTarget = false;

            var textOutline = _tooltipText.GetComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textOutline.effectDistance = new Vector2(1f, -1f);
            textOutline.useGraphicAlpha = true;

            if (wasCreated)
            {
                _tooltipPanel.gameObject.SetActive(false);
            }
        }

        private void ResolveCardBackSprite()
        {
            if (_cardBackSprite != null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_cardBackResourcePath))
            {
                _cardBackSprite = Resources.Load<Sprite>(_cardBackResourcePath);
                if (_cardBackSprite != null)
                {
                    return;
                }
            }

            if (_cardBackTexture == null && !string.IsNullOrWhiteSpace(_cardBackResourcePath))
            {
                _cardBackTexture = Resources.Load<Texture2D>(_cardBackResourcePath);
            }

            if (_cardBackTexture == null)
            {
                return;
            }

            if (_generatedCardBackSprite != null && _generatedSpriteSource == _cardBackTexture)
            {
                _cardBackSprite = _generatedCardBackSprite;
                return;
            }

            if (_generatedCardBackSprite != null)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(_generatedCardBackSprite);
                }
                else
                {
                    DestroyImmediate(_generatedCardBackSprite);
                }
            }

            _generatedCardBackSprite = Sprite.Create(
                _cardBackTexture,
                new Rect(0f, 0f, _cardBackTexture.width, _cardBackTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _generatedCardBackSprite.name = $"{_cardBackTexture.name}_{GeneratedSpriteSuffix}";
            _generatedSpriteSource = _cardBackTexture;
            _cardBackSprite = _generatedCardBackSprite;
        }

        private void ApplyCardBackVisuals()
        {
            if (_cardBackImages == null)
            {
                return;
            }

            for (var index = 0; index < _cardBackImages.Length; index++)
            {
                var image = _cardBackImages[index];
                if (image == null)
                {
                    continue;
                }

                image.sprite = _cardBackSprite;
                image.color = Color.white;
            }
        }

        private void ApplyDeckCountVisual()
        {
            if (_cardBackImages == null)
            {
                return;
            }

            var visibleLayerCount = Mathf.Min(_remainingDeckCount, _cardBackImages.Length);
            for (var index = 0; index < _cardBackImages.Length; index++)
            {
                var image = _cardBackImages[index];
                if (image != null)
                {
                    image.gameObject.SetActive(index >= _cardBackImages.Length - visibleLayerCount);
                }
            }
        }

        private void UpdateTooltipText()
        {
            if (_tooltipText == null)
            {
                return;
            }

            _tooltipText.text = _remainingDeckCount <= 0
                ? $"{TooltipSubject}에 남은 카드가 없습니다."
                : $"{TooltipSubject}에 카드가 {_remainingDeckCount}장 남았습니다.";
        }

        private Font ResolveTooltipFont()
        {
            if (_tooltipFont != null)
            {
                return _tooltipFont;
            }

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                _tooltipFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(KoreanFontAssetPath);
                if (_tooltipFont != null)
                {
                    return _tooltipFont;
                }
            }
#endif

            if (s_runtimeKoreanFont != null)
            {
                return s_runtimeKoreanFont;
            }

            try
            {
                s_runtimeKoreanFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not create runtime Korean font for deck tooltip: {exception.Message}");
            }

            if (s_runtimeKoreanFont == null)
            {
                s_runtimeKoreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return s_runtimeKoreanFont;
        }
    }
}
