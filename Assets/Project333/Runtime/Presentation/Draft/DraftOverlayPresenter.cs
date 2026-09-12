using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Draft
{
    public sealed class DraftOverlayPresenter : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int _deckBuildingVisualVersion;
        [SerializeField] private Font _interfaceFont;
        public int DeckBuildingVisualVersion => _deckBuildingVisualVersion;

        [Serializable]
        private sealed class DraftOptionBinding
        {
            [SerializeField] private Button _button;
            [SerializeField] private Image _artworkImage;

            public bool IsValid =>
                _button != null &&
                _artworkImage != null;

            public DraftOptionView ToView()
            {
                return new DraftOptionView
                {
                    Button = _button,
                    ArtworkImage = _artworkImage,
                };
            }
        }

        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
        };

        private static Font s_cachedDraftFont;

        private sealed class DraftOptionView
        {
            public Button Button;
            public Image ArtworkImage;
            public Text AttackText;
            public Text HpText;
        }

        [Header("Canvas")]
        [SerializeField] private Canvas _targetCanvas;

        [Header("Scene UI References")]
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _deckPanelTitleText;
        [SerializeField] private Text _deckListText;
        [SerializeField] private ScrollRect _deckListScrollRect;
        [SerializeField] private RectTransform _optionsRoot;
        [SerializeField] private RectTransform _deckPanelRectTransform;
        [SerializeField] private DraftOptionBinding[] _optionBindings = Array.Empty<DraftOptionBinding>();

        [Header("Runtime Fallback Styling")]
        [SerializeField] private Color _overlayColor = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        [SerializeField] private Color _cardFallbackColor = new Color(0.18f, 0.2f, 0.25f, 1f);
        [SerializeField] private Color _titleColor = Color.white;
        [SerializeField] private Color _bodyColor = new Color(0.9f, 0.93f, 1f, 1f);
        [SerializeField] private Vector2 _cardSize = new Vector2(550f, 733f);
        [Header("Responsive Layout")]
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private Vector2 _deckPanelReferenceSize = new Vector2(384f, 700f);
        [SerializeField] private float _deckPanelRightPadding = 24f;
        [SerializeField] private float _contentTopInset = 140f;
        [SerializeField] private float _contentBottomInset = 24f;
        [SerializeField] private int _titleReferenceFontSize = 42;
        [SerializeField] private int _statusReferenceFontSize = 22;
        [SerializeField] private int _deckTitleReferenceFontSize = 26;
        [SerializeField] private int _deckListReferenceFontSize = 16;
        [Header("Option Layout")]
        [SerializeField] private float _optionSpacing = 50f;
        [SerializeField] private float _optionAreaLeftPadding = 24f;
        [SerializeField] private float _optionDeckPanelGap = 24f;
        [SerializeField] private float _optionVerticalOffset = -40f;
        [Header("Back Button")]
        [SerializeField] private bool _showBackButton = true;
        [SerializeField] private string _backButtonLabel = "Back To Start";
        [SerializeField] private Vector2 _backButtonOffset = new Vector2(28f, -24f);
        [SerializeField] private Vector2 _backButtonSize = new Vector2(260f, 62f);
        [SerializeField] private int _backButtonFontSize = 24;
        [SerializeField] private Color _backButtonColor = new Color(0.18f, 0.2f, 0.26f, 0.96f);
        [SerializeField] private Color _backButtonTextColor = Color.white;
        [Header("Option Stat Overlay")]
        [SerializeField] private bool _showOptionStatOverlay = true;
        [SerializeField] private Vector2 _optionStatTextSize = new Vector2(128f, 84f);
        [SerializeField] private Vector2 _optionAttackStatNormalizedPosition = HandCardStatOverlayLayout.DefaultAttackPosition;
        [SerializeField] private Vector2 _optionHpStatNormalizedPosition = HandCardStatOverlayLayout.DefaultHpPosition;
        [SerializeField] private int _optionStatFontSize = 52;
        [SerializeField] private Color _optionStatTextColor = Color.white;
        [SerializeField] private Color _optionStatOutlineColor = new Color(0f, 0f, 0f, 0.95f);
        [SerializeField] private Vector2 _optionStatOutlineDistance = new Vector2(2.8f, -2.8f);

        private IDraftOverlayHost _host;
        private RectTransform _rootRectTransform;
        private Button _backButton;
        private Text _backButtonText;
        private readonly List<DraftOptionView> _optionViews = new List<DraftOptionView>();
        private float _lastOptionLayoutRootWidth = -1f;
        private float _lastOptionLayoutRootHeight = -1f;

        private void Awake()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            if (TryInitializeFromSceneReferences())
            {
                SetVisible(false);
            }
        }

        private void LateUpdate()
        {
            RefreshResponsiveOptionLayout();

            for (var optionIndex = 0; optionIndex < _optionViews.Count; optionIndex++)
            {
                RefreshOptionStatLayout(_optionViews[optionIndex]);
            }
        }

        public void Bind(IDraftOverlayHost host)
        {
            _host = host;
        }

#if UNITY_EDITOR
        [ContextMenu("Refresh Editable Draft UI")]
        public void RefreshEditableSceneUi()
        {
            if (UnityEngine.Application.isPlaying || !TryInitializeFromSceneReferences()) return;
            EnsureBackButton();
            foreach (var option in _optionViews)
            {
                EnsureOptionStatTexts(option);
                RefreshOptionStatLayout(option);
            }
            RefreshResponsiveOptionLayout(force: true);
        }
#endif

        public void ShowOffer(DraftOffer draftOffer, IReadOnlyList<string> draftedCardIds)
        {
            if (draftOffer == null)
            {
                return;
            }

            EnsureUi();
            SetVisible(true);

            _titleText.text = draftOffer.IsLegendaryOpeningOffer
                ? "Choose 1 Legendary Card"
                : "Choose 1 Card";
            _titleText.color = _titleColor;
            _statusText.text = BuildStatusSummary(draftedCardIds);
            _statusText.color = _bodyColor;
            RefreshDeckList(draftedCardIds);

            for (var i = 0; i < _optionViews.Count; i++)
            {
                var optionView = _optionViews[i];
                if (optionView == null)
                {
                    continue;
                }

                if (i >= draftOffer.CandidateCards.Count)
                {
                    SetOptionStatOverlayVisible(optionView, false);
                    optionView.Button.gameObject.SetActive(false);
                    continue;
                }

                var cardAsset = draftOffer.CandidateCards[i];
                optionView.Button.gameObject.SetActive(true);
                ConfigureOption(optionView, cardAsset);
            }
        }

        public void ShowValidationMessage(string message)
        {
            EnsureUi();
            SetVisible(true);
            _titleText.text = "Deck Building Cannot Start";
            _titleText.color = _titleColor;
            _statusText.text = string.IsNullOrWhiteSpace(message) ? "Unknown deck-building validation error." : message;
            _statusText.color = _bodyColor;
            RefreshDeckList(null);

            foreach (var optionView in _optionViews)
            {
                if (optionView?.Button != null)
                {
                    optionView.Button.gameObject.SetActive(false);
                }
            }
        }

        public void Hide()
        {
            if (_rootCanvasGroup == null)
            {
                return;
            }

            SetVisible(false);
        }

        private void ConfigureOption(DraftOptionView optionView, CardDefinitionAsset cardAsset)
        {
            optionView.Button.onClick.RemoveAllListeners();

            var cardId = cardAsset == null ? string.Empty : cardAsset.CardId;
            optionView.Button.onClick.AddListener(() => _host?.SelectDraftCard(cardId));

            if (cardAsset != null && CardArtworkLibrary.TryGetArtwork(cardAsset.CardId, out var artwork))
            {
                optionView.ArtworkImage.sprite = artwork;
                optionView.ArtworkImage.color = Color.white;
            }
            else
            {
                optionView.ArtworkImage.sprite = null;
                optionView.ArtworkImage.color = _cardFallbackColor;
            }

            RefreshOptionStatOverlay(optionView, cardAsset);
        }

        private void RefreshOptionStatOverlay(DraftOptionView optionView, CardDefinitionAsset cardAsset)
        {
            EnsureOptionStatTexts(optionView);
            if (!_showOptionStatOverlay ||
                optionView == null ||
                string.IsNullOrWhiteSpace(cardAsset?.CardId) ||
                !TryResolveCardStats(cardAsset, out var stats))
            {
                SetOptionStatOverlayVisible(optionView, false);
                return;
            }

            if (optionView.AttackText != null)
            {
                optionView.AttackText.text = stats.Attack.ToString();
            }

            if (optionView.HpText != null)
            {
                optionView.HpText.text = stats.HasHp ? stats.Hp.ToString() : string.Empty;
            }

            RefreshOptionStatLayout(optionView);
            SetOptionStatOverlayVisible(optionView, true, stats.HasHp);
        }

        private void EnsureOptionStatTexts(DraftOptionView optionView)
        {
            if (optionView?.ArtworkImage == null ||
                optionView.ArtworkImage.rectTransform == null)
            {
                return;
            }

            var artworkRect = optionView.ArtworkImage.rectTransform;
            if (optionView.AttackText == null)
            {
                optionView.AttackText = ResolveOrCreateOptionStatText(artworkRect, "AttackValueText", isHp: false);
            }

            if (optionView.HpText == null)
            {
                optionView.HpText = ResolveOrCreateOptionStatText(artworkRect, "HpValueText", isHp: true);
            }

            ApplyOptionStatTextStyle(optionView.AttackText, isHp: false);
            ApplyOptionStatTextStyle(optionView.HpText, isHp: true);
        }

        private Text ResolveOrCreateOptionStatText(RectTransform parent, string objectName, bool isHp)
        {
            if (parent.Find(objectName) is RectTransform existingRect &&
                existingRect.TryGetComponent<Text>(out var existingText))
            {
                return existingText;
            }

            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.SetAsLastSibling();

            var text = textObject.GetComponent<Text>();
            text.raycastTarget = false;
            text.supportRichText = false;

            ApplyOptionStatTextStyle(text, isHp);
            return text;
        }

        private void ApplyOptionStatTextStyle(Text text, bool isHp)
        {
            if (text == null)
            {
                return;
            }

            if (_deckBuildingVisualVersion == 0 || text.font == null)
                text.font = ResolveFont();
            text.fontSize = Mathf.Max(1, _optionStatFontSize);
            text.fontStyle = FontStyle.Bold;
            text.color = _optionStatTextColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            if (text.rectTransform != null)
            {
                text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                text.rectTransform.sizeDelta = _optionStatTextSize;
                text.rectTransform.anchoredPosition = Vector2.zero;
            }

            if (text.TryGetComponent<Outline>(out var outline))
            {
                outline.effectColor = _optionStatOutlineColor;
                outline.effectDistance = _optionStatOutlineDistance;
                outline.useGraphicAlpha = true;
            }
        }

        private void RefreshOptionStatLayout(DraftOptionView optionView)
        {
            if (optionView?.ArtworkImage == null || optionView.ArtworkImage.sprite == null)
            {
                return;
            }

            var artworkRect = optionView.ArtworkImage.rectTransform;
            var renderedSpriteRect = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(optionView.ArtworkImage);

            PositionOptionStatText(
                optionView.AttackText,
                artworkRect,
                renderedSpriteRect,
                _optionAttackStatNormalizedPosition);
            PositionOptionStatText(
                optionView.HpText,
                artworkRect,
                renderedSpriteRect,
                _optionHpStatNormalizedPosition);
        }

        private static void PositionOptionStatText(Text text, RectTransform artworkRectTransform,
            Rect renderedSpriteRect, Vector2 normalizedPosition)
        {
            HandCardStatOverlayLayout.PositionStatText(text?.rectTransform, artworkRectTransform,
                renderedSpriteRect, normalizedPosition);
        }

        private static bool TryResolveCardStats(CardDefinitionAsset cardAsset, out CardStatDisplay stats)
        {
            stats = default;

            if (cardAsset == null)
            {
                return false;
            }

            try
            {
                return CardStatDisplay.TryCreate(cardAsset.ToDefinition(),
                    AccountSessionState.GetOwnedCardUpgradeLevel(cardAsset.CardId), out stats);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void SetOptionStatOverlayVisible(DraftOptionView optionView, bool visible, bool showHp = true)
        {
            if (optionView?.AttackText != null)
            {
                optionView.AttackText.enabled = visible;
            }

            if (optionView?.HpText != null)
            {
                optionView.HpText.enabled = visible && showHp;
            }
        }

        private void EnsureUi()
        {
            if (TryInitializeFromSceneReferences())
            {
                EnsureBackButton();
                RefreshResponsiveOptionLayout(force: true);
                return;
            }

            if (_rootCanvasGroup != null && _rootRectTransform != null && _optionViews.Count > 0)
            {
                EnsureBackButton();
                RefreshResponsiveOptionLayout(force: true);
                return;
            }

            BuildRuntimeFallbackUi();
            EnsureBackButton();
            RefreshResponsiveOptionLayout(force: true);
        }

        private bool TryInitializeFromSceneReferences()
        {
            if (_rootCanvasGroup == null ||
                _titleText == null ||
                _statusText == null ||
                _deckPanelTitleText == null ||
                _deckListText == null ||
                _optionBindings == null ||
                _optionBindings.Length == 0)
            {
                return false;
            }

            var rootRectTransform = _rootCanvasGroup.transform as RectTransform;
            if (rootRectTransform == null)
            {
                return false;
            }

            _optionViews.Clear();
            foreach (var binding in _optionBindings)
            {
                if (binding == null || !binding.IsValid)
                {
                    _optionViews.Clear();
                    return false;
                }

                _optionViews.Add(binding.ToView());
            }

            _rootRectTransform = rootRectTransform;
            ConfigureCanvasScaler(_targetCanvas);
            ResolveOptionLayoutReferences();
            EnsureDeckListScrollView();
            RefreshResponsiveOptionLayout(force: true);
            return _optionViews.Count > 0;
        }

        private void BuildRuntimeFallbackUi()
        {
            var canvas = ResolveCanvas();
            var root = new GameObject("DraftOverlayRoot", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _rootRectTransform = root.GetComponent<RectTransform>();
            _rootRectTransform.SetParent(canvas.transform, false);
            _rootRectTransform.anchorMin = Vector2.zero;
            _rootRectTransform.anchorMax = Vector2.one;
            _rootRectTransform.offsetMin = Vector2.zero;
            _rootRectTransform.offsetMax = Vector2.zero;
            _rootRectTransform.SetAsLastSibling();

            var overlayImage = root.GetComponent<Image>();
            overlayImage.color = _overlayColor;
            overlayImage.raycastTarget = true;

            _rootCanvasGroup = root.GetComponent<CanvasGroup>();

            var contentRoot = CreateRectChild("ContentRoot", _rootRectTransform);
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;

            _titleText = CreateText("TitleText", contentRoot, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchTop(_titleText.rectTransform, 16f, 54f, 20f);

            _statusText = CreateText("StatusText", contentRoot, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchTop(_statusText.rectTransform, 78f, 46f, 24f);

            var optionsRoot = CreateRectChild("OptionsRoot", _rootRectTransform);
            optionsRoot.anchorMin = new Vector2(0f, 0.5f);
            optionsRoot.anchorMax = new Vector2(0f, 0.5f);
            optionsRoot.pivot = new Vector2(0f, 0.5f);
            optionsRoot.anchoredPosition = new Vector2(_optionAreaLeftPadding, _optionVerticalOffset);
            optionsRoot.sizeDelta = new Vector2(
                (_cardSize.x * 3f) + (_optionSpacing * 2f),
                _cardSize.y);
            _optionsRoot = optionsRoot;

            var optionsLayoutElement = optionsRoot.gameObject.AddComponent<LayoutElement>();
            optionsLayoutElement.ignoreLayout = true;
            optionsLayoutElement.preferredWidth = optionsRoot.sizeDelta.x;
            optionsLayoutElement.preferredHeight = _cardSize.y;

            var horizontalLayoutGroup = optionsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
            horizontalLayoutGroup.childControlHeight = false;
            horizontalLayoutGroup.childControlWidth = false;
            horizontalLayoutGroup.childForceExpandHeight = false;
            horizontalLayoutGroup.childForceExpandWidth = false;
            horizontalLayoutGroup.spacing = _optionSpacing;

            _optionViews.Clear();
            for (var i = 0; i < 3; i++)
            {
                _optionViews.Add(CreateOptionView(optionsRoot, i));
            }

            CreateDeckPanel(_rootRectTransform);

            RefreshResponsiveOptionLayout(force: true);

            SetVisible(false);
        }

        private void EnsureBackButton()
        {
            if (!_showBackButton || _rootRectTransform == null)
            {
                if (_backButton != null)
                {
                    _backButton.gameObject.SetActive(false);
                }

                return;
            }

            if (_backButton == null)
            {
                var existing = _rootRectTransform.Find("BackToStartButton") as RectTransform;
                if (existing != null &&
                    existing.TryGetComponent<Button>(out var existingButton))
                {
                    _backButton = existingButton;
                    _backButtonText = ResolveButtonLabel(existingButton);
                }
            }

            var createdButton = false;
            if (_backButton == null)
            {
                var buttonObject = new GameObject("BackToStartButton", typeof(RectTransform), typeof(Image), typeof(Button));
                createdButton = true;
                var buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.SetParent(_rootRectTransform, false);

                _backButton = buttonObject.GetComponent<Button>();

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(buttonRect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 8f);
                labelRect.offsetMax = new Vector2(-12f, -8f);

                _backButtonText = labelObject.GetComponent<Text>();
                _backButtonText.raycastTarget = false;
                _backButtonText.alignment = TextAnchor.MiddleCenter;
                _backButtonText.fontStyle = FontStyle.Bold;
            }

            var rectTransform = _backButton.transform as RectTransform;
            if (rectTransform != null && (_deckBuildingVisualVersion == 0 || createdButton))
            {
                rectTransform.anchorMin = new Vector2(0f, 1f);
                rectTransform.anchorMax = new Vector2(0f, 1f);
                rectTransform.pivot = new Vector2(0f, 1f);
                rectTransform.anchoredPosition = _backButtonOffset;
                rectTransform.sizeDelta = _backButtonSize;
                rectTransform.SetAsLastSibling();
            }

            if ((_deckBuildingVisualVersion == 0 || createdButton) && _backButton.TryGetComponent<Image>(out var image))
            {
                image.color = _backButtonColor;
                image.raycastTarget = true;
            }

            var colors = _backButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            _backButton.colors = colors;

            _backButton.onClick.RemoveListener(NotifyBackButtonClicked);
            _backButton.onClick.AddListener(NotifyBackButtonClicked);
            _backButton.gameObject.SetActive(true);

            if (_backButtonText == null)
            {
                _backButtonText = ResolveButtonLabel(_backButton);
            }

            if (_backButtonText != null)
            {
                _backButtonText.text = string.IsNullOrWhiteSpace(_backButtonLabel) ? "Back To Start" : _backButtonLabel;
                if (_deckBuildingVisualVersion == 0 || createdButton)
                {
                    _backButtonText.font = ResolveFont();
                    _backButtonText.fontSize = Mathf.Max(1, _backButtonFontSize);
                    _backButtonText.color = _backButtonTextColor;
                    _backButtonText.alignment = TextAnchor.MiddleCenter;
                    _backButtonText.fontStyle = FontStyle.Bold;
                }
            }
        }

        private void NotifyBackButtonClicked()
        {
            _host?.ReturnFromDraftOverlay();
        }

        private static Text ResolveButtonLabel(Button button)
        {
            return button == null ? null : button.GetComponentInChildren<Text>(true);
        }

        private void ResolveOptionLayoutReferences()
        {
            if (_optionsRoot == null &&
                _optionBindings != null &&
                _optionBindings.Length > 0 &&
                _optionBindings[0]?.ToView()?.Button != null)
            {
                _optionsRoot = _optionBindings[0].ToView().Button.transform.parent as RectTransform;
            }

            if (_deckPanelRectTransform == null && _deckPanelTitleText != null)
            {
                _deckPanelRectTransform = _deckPanelTitleText.transform.parent as RectTransform;
            }
        }

        private void RefreshResponsiveOptionLayout(bool force = false)
        {
            if (_rootRectTransform == null || _optionsRoot == null || _optionViews.Count == 0)
            {
                return;
            }

            if (_optionsRoot.parent != _rootRectTransform)
            {
                _optionsRoot.SetParent(_rootRectTransform, false);
                force = true;
            }

            var rootRect = _rootRectTransform.rect;
            if (rootRect.width <= 1f || rootRect.height <= 1f)
            {
                return;
            }

            if (!force &&
                Mathf.Approximately(_lastOptionLayoutRootWidth, rootRect.width) &&
                Mathf.Approximately(_lastOptionLayoutRootHeight, rootRect.height))
            {
                return;
            }

            _lastOptionLayoutRootWidth = rootRect.width;
            _lastOptionLayoutRootHeight = rootRect.height;

            var referenceWidth = Mathf.Max(1f, _referenceResolution.x);
            var referenceHeight = Mathf.Max(1f, _referenceResolution.y);
            var layoutScale = Mathf.Max(
                0.1f,
                Mathf.Min(rootRect.width / referenceWidth, rootRect.height / referenceHeight));

            ApplyResponsiveChromeLayout(layoutScale, rootRect);

            var rightPadding = Mathf.Max(0f, _deckPanelRightPadding) * layoutScale;
            var deckPanelWidth = Mathf.Min(
                Mathf.Max(1f, _deckPanelReferenceSize.x * layoutScale),
                rootRect.width * 0.4f);
            var deckPanelLeft = rootRect.xMax - rightPadding - deckPanelWidth;

            var optionCount = _optionViews.Count;
            var cardWidth = Mathf.Max(1f, _cardSize.x);
            var cardHeight = Mathf.Max(1f, _cardSize.y);
            var resolvedSpacing = optionCount > 1 ? _optionSpacing : 0f;
            var leftPadding = Mathf.Max(0f, _optionAreaLeftPadding) * layoutScale;
            var panelGap = Mathf.Max(0f, _optionDeckPanelGap) * layoutScale;
            var availableWidth = Mathf.Max(
                1f,
                deckPanelLeft - panelGap - (rootRect.xMin + leftPadding));
            var minimumContentWidth =
                (cardWidth * optionCount) +
                (resolvedSpacing * Mathf.Max(0, optionCount - 1));
            minimumContentWidth = Mathf.Max(1f, minimumContentWidth);

            var optionCenterY = _optionVerticalOffset * layoutScale;
            var topLimit = rootRect.yMax - (Mathf.Max(0f, _contentTopInset) * layoutScale);
            var bottomLimit = rootRect.yMin + (Mathf.Max(0f, _contentBottomInset) * layoutScale);
            var availableHalfHeight = Mathf.Max(
                1f,
                Mathf.Min(topLimit - optionCenterY, optionCenterY - bottomLimit));
            var availableHeight = availableHalfHeight * 2f;
            var scale = Mathf.Clamp(
                Mathf.Min(availableWidth / minimumContentWidth, availableHeight / cardHeight),
                0.1f,
                1f);
            var unscaledAvailableWidth = availableWidth / scale;

            _optionsRoot.anchorMin = new Vector2(0f, 0.5f);
            _optionsRoot.anchorMax = new Vector2(0f, 0.5f);
            _optionsRoot.pivot = new Vector2(0f, 0.5f);
            _optionsRoot.anchoredPosition = new Vector2(leftPadding, optionCenterY);
            _optionsRoot.sizeDelta = new Vector2(unscaledAvailableWidth, cardHeight);
            _optionsRoot.localScale = new Vector3(scale, scale, 1f);

            if (_optionsRoot.TryGetComponent<LayoutElement>(out var rootLayoutElement))
            {
                rootLayoutElement.ignoreLayout = true;
                rootLayoutElement.preferredWidth = unscaledAvailableWidth;
                rootLayoutElement.preferredHeight = cardHeight;
            }

            if (_optionsRoot.TryGetComponent<HorizontalLayoutGroup>(out var layoutGroup))
            {
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
                layoutGroup.spacing = resolvedSpacing;
            }

            foreach (var optionView in _optionViews)
            {
                if (optionView?.Button == null)
                {
                    continue;
                }

                var optionRect = optionView.Button.transform as RectTransform;
                if (optionRect != null)
                {
                    optionRect.localScale = Vector3.one;
                    optionRect.sizeDelta = new Vector2(cardWidth, cardHeight);
                }

                if (optionView.Button.TryGetComponent<LayoutElement>(out var optionLayoutElement))
                {
                    optionLayoutElement.preferredWidth = cardWidth;
                    optionLayoutElement.preferredHeight = cardHeight;
                }

                var artworkRect = optionView.ArtworkImage?.rectTransform;
                if (artworkRect != null)
                {
                    artworkRect.anchorMin = Vector2.zero;
                    artworkRect.anchorMax = Vector2.one;
                    artworkRect.pivot = new Vector2(0.5f, 0.5f);
                    artworkRect.offsetMin = Vector2.zero;
                    artworkRect.offsetMax = Vector2.zero;
                    artworkRect.localScale = Vector3.one;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_optionsRoot);
        }

        private void ApplyResponsiveChromeLayout(float layoutScale, Rect rootRect)
        {
            if (_titleText != null && _deckBuildingVisualVersion == 0)
            {
                StretchTop(_titleText.rectTransform, 16f * layoutScale, 54f * layoutScale, 20f * layoutScale);
                _titleText.fontSize = ScaleFontSize(_titleReferenceFontSize, layoutScale);
            }

            if (_statusText != null && _deckBuildingVisualVersion == 0)
            {
                StretchTop(_statusText.rectTransform, 78f * layoutScale, 46f * layoutScale, 24f * layoutScale);
                _statusText.fontSize = ScaleFontSize(_statusReferenceFontSize, layoutScale);
            }

            if (_deckPanelRectTransform != null)
            {
                var rightPadding = Mathf.Max(0f, _deckPanelRightPadding) * layoutScale;
                var panelWidth = Mathf.Min(
                    Mathf.Max(1f, _deckPanelReferenceSize.x * layoutScale),
                    rootRect.width * 0.4f);
                var maximumPanelHeight = Mathf.Max(
                    1f,
                    rootRect.height -
                    ((Mathf.Max(0f, _contentTopInset) + Mathf.Max(0f, _contentBottomInset)) * layoutScale));
                var panelHeight = Mathf.Min(
                    Mathf.Max(1f, _deckPanelReferenceSize.y * layoutScale),
                    maximumPanelHeight);

                _deckPanelRectTransform.anchorMin = new Vector2(1f, 0.5f);
                _deckPanelRectTransform.anchorMax = new Vector2(1f, 0.5f);
                _deckPanelRectTransform.pivot = new Vector2(1f, 0.5f);
                _deckPanelRectTransform.anchoredPosition = new Vector2(-rightPadding, 0f);
                _deckPanelRectTransform.sizeDelta = new Vector2(panelWidth, panelHeight);

                if (_deckPanelRectTransform.TryGetComponent<LayoutElement>(out var panelLayoutElement))
                {
                    panelLayoutElement.ignoreLayout = true;
                    panelLayoutElement.preferredWidth = panelWidth;
                    panelLayoutElement.preferredHeight = panelHeight;
                }
            }

            if (_deckPanelTitleText != null && _deckBuildingVisualVersion == 0)
            {
                StretchTop(_deckPanelTitleText.rectTransform, 14f * layoutScale, 38f * layoutScale, 16f * layoutScale);
                _deckPanelTitleText.fontSize = ScaleFontSize(_deckTitleReferenceFontSize, layoutScale);
            }

            if (_deckListScrollRect != null && _deckBuildingVisualVersion == 0)
            {
                StretchFill(
                    _deckListScrollRect.GetComponent<RectTransform>(),
                    60f * layoutScale,
                    18f * layoutScale,
                    18f * layoutScale,
                    16f * layoutScale);
            }

            if (_deckListText != null && _deckBuildingVisualVersion == 0)
            {
                _deckListText.fontSize = ScaleFontSize(_deckListReferenceFontSize, layoutScale);
            }

            RefreshDeckListScrollLayout(resetToTop: false);

            ApplyResponsiveBackButtonLayout(layoutScale);
        }

        private void ApplyResponsiveBackButtonLayout(float layoutScale)
        {
            if (_backButton == null || _deckBuildingVisualVersion > 0)
            {
                return;
            }

            if (_backButton.transform is RectTransform buttonRect)
            {
                buttonRect.anchorMin = new Vector2(0f, 1f);
                buttonRect.anchorMax = new Vector2(0f, 1f);
                buttonRect.pivot = new Vector2(0f, 1f);
                buttonRect.anchoredPosition = _backButtonOffset * layoutScale;
                buttonRect.sizeDelta = _backButtonSize * layoutScale;
                buttonRect.SetAsLastSibling();
            }

            if (_backButtonText == null)
            {
                _backButtonText = ResolveButtonLabel(_backButton);
            }

            if (_backButtonText == null)
            {
                return;
            }

            _backButtonText.fontSize = ScaleFontSize(_backButtonFontSize, layoutScale);
            var labelRect = _backButtonText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 8f) * layoutScale;
            labelRect.offsetMax = new Vector2(-12f, -8f) * layoutScale;
        }

        private static int ScaleFontSize(int referenceFontSize, float layoutScale)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, referenceFontSize) * layoutScale));
        }

        private void CreateDeckPanel(RectTransform parent)
        {
            var deckPanel = new GameObject("DeckPanel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var deckPanelRectTransform = deckPanel.GetComponent<RectTransform>();
            deckPanelRectTransform.SetParent(parent, false);
            deckPanelRectTransform.anchorMin = new Vector2(1f, 0.5f);
            deckPanelRectTransform.anchorMax = new Vector2(1f, 0.5f);
            deckPanelRectTransform.pivot = new Vector2(1f, 0.5f);
            deckPanelRectTransform.anchoredPosition = new Vector2(-_deckPanelRightPadding, 0f);
            deckPanelRectTransform.sizeDelta = _deckPanelReferenceSize;
            _deckPanelRectTransform = deckPanelRectTransform;

            var layoutElement = deckPanel.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            layoutElement.preferredWidth = _deckPanelReferenceSize.x;
            layoutElement.preferredHeight = _deckPanelReferenceSize.y;

            var panelImage = deckPanel.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.12f, 0.16f, 0.96f);
            panelImage.raycastTarget = false;

            _deckPanelTitleText = CreateText("DeckPanelTitle", deckPanelRectTransform, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            _deckPanelTitleText.color = _titleColor;
            StretchTop(_deckPanelTitleText.rectTransform, 14f, 38f, 16f);
            _deckPanelTitleText.text = "Current Deck";

            _deckListScrollRect = CreateDeckListScrollView(deckPanelRectTransform, out _deckListText);
            _deckListText.text = "No picks yet.";
        }

        private ScrollRect CreateDeckListScrollView(RectTransform parent, out Text listText)
        {
            var scrollObject = new GameObject(
                "DeckListScrollView",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.SetParent(parent, false);
            StretchFill(scrollRectTransform, 60f, 18f, 18f, 16f);

            var inputImage = scrollObject.GetComponent<Image>();
            inputImage.color = Color.clear;
            inputImage.raycastTarget = true;

            var contentObject = new GameObject("Content", typeof(RectTransform));
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.SetParent(scrollRectTransform, false);
            ConfigureTopAnchoredRect(contentRect, 1f);

            listText = CreateText("DeckListText", contentRect, 16, FontStyle.Normal, TextAnchor.UpperLeft);
            listText.color = _bodyColor;
            listText.horizontalOverflow = HorizontalWrapMode.Wrap;
            listText.verticalOverflow = VerticalWrapMode.Overflow;
            listText.raycastTarget = false;
            ConfigureTopAnchoredRect(listText.rectTransform, 1f);

            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            ConfigureVerticalScrollRect(scrollRect, scrollRectTransform, contentRect);
            return scrollRect;
        }

        private DraftOptionView CreateOptionView(RectTransform parent, int index)
        {
            var optionRoot = new GameObject($"DraftOption_{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var optionRectTransform = optionRoot.GetComponent<RectTransform>();
            optionRectTransform.SetParent(parent, false);
            optionRectTransform.sizeDelta = _cardSize;

            var layoutElement = optionRoot.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = _cardSize.x;
            layoutElement.preferredHeight = _cardSize.y;

            var backgroundImage = optionRoot.GetComponent<Image>();
            backgroundImage.color = new Color(0.14f, 0.16f, 0.2f, 0.96f);
            backgroundImage.raycastTarget = true;

            var button = optionRoot.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var artworkObject = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            var artworkRectTransform = artworkObject.GetComponent<RectTransform>();
            artworkRectTransform.SetParent(optionRectTransform, false);
            artworkRectTransform.anchorMin = Vector2.zero;
            artworkRectTransform.anchorMax = Vector2.one;
            artworkRectTransform.pivot = new Vector2(0.5f, 0.5f);
            artworkRectTransform.offsetMin = Vector2.zero;
            artworkRectTransform.offsetMax = Vector2.zero;

            var artworkImage = artworkObject.GetComponent<Image>();
            artworkImage.preserveAspect = true;
            artworkImage.raycastTarget = false;
            artworkImage.color = _cardFallbackColor;

            return new DraftOptionView
            {
                Button = button,
                ArtworkImage = artworkImage,
            };
        }

        private Canvas ResolveCanvas()
        {
            if (_targetCanvas != null)
            {
                ConfigureCanvasScaler(_targetCanvas);
                return _targetCanvas;
            }

            _targetCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (_targetCanvas != null)
            {
                ConfigureCanvasScaler(_targetCanvas);
                return _targetCanvas;
            }

            var canvasObject = new GameObject("DraftOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _targetCanvas = canvasObject.GetComponent<Canvas>();
            _targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvasScaler(_targetCanvas);
            return _targetCanvas;
        }

        private void ConfigureCanvasScaler(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            if (!rootCanvas.TryGetComponent<CanvasScaler>(out var scaler))
            {
                scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                Mathf.Max(1f, _referenceResolution.x),
                Mathf.Max(1f, _referenceResolution.y));
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }

        private void SetVisible(bool isVisible)
        {
            _rootCanvasGroup.alpha = isVisible ? 1f : 0f;
            _rootCanvasGroup.interactable = isVisible;
            _rootCanvasGroup.blocksRaycasts = isVisible;
        }

        private Text CreateText(string name, RectTransform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = _bodyColor;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private Font ResolveFont()
        {
            if (_interfaceFont != null) return _interfaceFont;
            var draftFont = ResolveDraftFont();
            if (draftFont != null)
            {
                return draftFont;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Font ResolveDraftFont()
        {
            if (s_cachedDraftFont != null)
            {
                return s_cachedDraftFont;
            }

            try
            {
                s_cachedDraftFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
            }
            catch
            {
                s_cachedDraftFont = null;
            }

            return s_cachedDraftFont;
        }

        private static RectTransform CreateRectChild(string name, RectTransform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static void StretchTop(RectTransform rectTransform, float topOffset, float height, float horizontalPadding)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(horizontalPadding, -(topOffset + height));
            rectTransform.offsetMax = new Vector2(-horizontalPadding, -topOffset);
        }

        private static void StretchBottom(RectTransform rectTransform, float bottomOffset, float height, float horizontalPadding)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.offsetMin = new Vector2(horizontalPadding, bottomOffset);
            rectTransform.offsetMax = new Vector2(-horizontalPadding, bottomOffset + height);
        }

        private static void StretchFill(RectTransform rectTransform, float topOffset, float bottomOffset, float leftPadding, float rightPadding)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(leftPadding, bottomOffset);
            rectTransform.offsetMax = new Vector2(-rightPadding, -topOffset);
        }

        private void RefreshDeckList(IReadOnlyList<string> draftedCardIds)
        {
            if (_deckListText == null)
            {
                return;
            }

            var cardCount = draftedCardIds?.Count ?? 0;
            if (_deckPanelTitleText != null)
            {
                _deckPanelTitleText.text = $"Current Deck ({cardCount}/33)";
            }

            _deckListText.text = BuildDeckListText(draftedCardIds);
            RefreshDeckListScrollLayout(resetToTop: true);
        }

        private void EnsureDeckListScrollView()
        {
            if (_deckListText == null)
            {
                return;
            }

            if (_deckListScrollRect == null)
            {
                _deckListScrollRect = _deckListText.GetComponentInParent<ScrollRect>();
            }

            if (_deckListScrollRect == null)
            {
                var panelRect = _deckPanelRectTransform != null
                    ? _deckPanelRectTransform
                    : _deckListText.transform.parent as RectTransform;
                if (panelRect == null)
                {
                    return;
                }

                var scrollObject = new GameObject(
                    "DeckListScrollView",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(RectMask2D),
                    typeof(ScrollRect));
                var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
                scrollRectTransform.SetParent(panelRect, false);

                var inputImage = scrollObject.GetComponent<Image>();
                inputImage.color = Color.clear;
                inputImage.raycastTarget = true;

                var contentObject = new GameObject("Content", typeof(RectTransform));
                var contentRect = contentObject.GetComponent<RectTransform>();
                contentRect.SetParent(scrollRectTransform, false);
                ConfigureTopAnchoredRect(contentRect, 1f);

                _deckListText.rectTransform.SetParent(contentRect, false);
                ConfigureTopAnchoredRect(_deckListText.rectTransform, 1f);
                _deckListText.raycastTarget = false;

                _deckListScrollRect = scrollObject.GetComponent<ScrollRect>();
                ConfigureVerticalScrollRect(_deckListScrollRect, scrollRectTransform, contentRect);
            }
            else
            {
                var viewport = _deckListScrollRect.viewport != null
                    ? _deckListScrollRect.viewport
                    : _deckListScrollRect.transform as RectTransform;
                var content = _deckListScrollRect.content != null
                    ? _deckListScrollRect.content
                    : _deckListText.transform.parent as RectTransform;
                if (viewport != null && content != null)
                {
                    ConfigureVerticalScrollRect(_deckListScrollRect, viewport, content);
                }
            }

            RefreshDeckListScrollLayout(resetToTop: true);
        }

        private void RefreshDeckListScrollLayout(bool resetToTop)
        {
            if (_deckListScrollRect == null ||
                _deckListScrollRect.content == null ||
                _deckListText == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            var viewport = _deckListScrollRect.viewport != null
                ? _deckListScrollRect.viewport
                : _deckListScrollRect.transform as RectTransform;
            if (viewport == null)
            {
                return;
            }

            var previousPosition = _deckListScrollRect.verticalNormalizedPosition;
            var viewportHeight = Mathf.Max(1f, viewport.rect.height);
            var contentRect = _deckListScrollRect.content;
            ConfigureTopAnchoredRect(contentRect, viewportHeight);
            ConfigureTopAnchoredRect(_deckListText.rectTransform, viewportHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_deckListText.rectTransform);

            var contentHeight = Mathf.Max(viewportHeight, _deckListText.preferredHeight + 4f);
            ConfigureTopAnchoredRect(contentRect, contentHeight);
            ConfigureTopAnchoredRect(_deckListText.rectTransform, contentHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();

            _deckListScrollRect.verticalNormalizedPosition = resetToTop
                ? 1f
                : Mathf.Clamp01(previousPosition);
        }

        private static void ConfigureVerticalScrollRect(
            ScrollRect scrollRect,
            RectTransform viewport,
            RectTransform content)
        {
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 30f;
        }

        private static void ConfigureTopAnchoredRect(RectTransform rectTransform, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(0f, -Mathf.Max(1f, height));
            rectTransform.offsetMax = Vector2.zero;
        }

        private string BuildStatusSummary(IReadOnlyList<string> draftedCardIds)
        {
            var cardCount = draftedCardIds?.Count ?? 0;
            var remainingCount = Math.Max(0, 33 - cardCount);
            return $"Selected: {cardCount} / 33   Remaining: {remainingCount}";
        }

        private string BuildDeckListText(IReadOnlyList<string> draftedCardIds)
        {
            return DraftDeckListFormatter.BuildDeckBuildingPanelText(
                draftedCardIds,
                ResolveCardAsset,
                "No cards selected yet.");
        }

        private CardDefinitionAsset ResolveCardAsset(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return null;
            }

            return _host != null &&
                   _host.TryGetCardDefinitionAsset(cardId, out var cardAsset)
                ? cardAsset
                : null;
        }

    }
}
