using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Draft
{
    public sealed class DraftOverlayPresenter : MonoBehaviour
    {
        [Serializable]
        private sealed class DraftOptionBinding
        {
            [SerializeField] private Button _button;
            [SerializeField] private Image _artworkImage;
            [SerializeField] private Text _titleText;
            [SerializeField] private Text _subtitleText;

            public bool IsValid =>
                _button != null &&
                _artworkImage != null &&
                _titleText != null &&
                _subtitleText != null;

            public DraftOptionView ToView()
            {
                return new DraftOptionView
                {
                    Button = _button,
                    ArtworkImage = _artworkImage,
                    TitleText = _titleText,
                    SubtitleText = _subtitleText,
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
            public Text TitleText;
            public Text SubtitleText;
        }

        [Header("Canvas")]
        [SerializeField] private Canvas _targetCanvas;

        [Header("Scene UI References")]
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _progressText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _deckPanelTitleText;
        [SerializeField] private Text _deckListText;
        [SerializeField] private DraftOptionBinding[] _optionBindings = Array.Empty<DraftOptionBinding>();

        [Header("Runtime Fallback Styling")]
        [SerializeField] private Color _overlayColor = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        [SerializeField] private Color _cardFallbackColor = new Color(0.18f, 0.2f, 0.25f, 1f);
        [SerializeField] private Color _titleColor = Color.white;
        [SerializeField] private Color _bodyColor = new Color(0.9f, 0.93f, 1f, 1f);
        [SerializeField] private Vector2 _cardSize = new Vector2(290f, 460f);

        private IDraftOverlayHost _host;
        private RectTransform _rootRectTransform;
        private readonly List<DraftOptionView> _optionViews = new List<DraftOptionView>();

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

        public void Bind(IDraftOverlayHost host)
        {
            _host = host;
        }

        public void ShowOffer(DraftOffer draftOffer, IReadOnlyList<string> draftedCardIds)
        {
            if (draftOffer == null)
            {
                return;
            }

            EnsureUi();
            SetVisible(true);

            _titleText.text = draftOffer.IsLegendaryOpeningOffer
                ? "Legendary Opening Pick"
                : "Draft Pick";
            _titleText.color = _titleColor;
            _progressText.text = $"Pick {draftOffer.PickNumber} / 33";
            _progressText.color = _bodyColor;
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
            _titleText.text = "Draft Cannot Start";
            _titleText.color = _titleColor;
            _progressText.text = string.Empty;
            _statusText.text = string.IsNullOrWhiteSpace(message) ? "Unknown draft validation error." : message;
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

            optionView.TitleText.text = cardAsset == null
                ? "Unknown Card"
                : $"{cardAsset.DisplayName}\n{cardAsset.Rarity}";
            optionView.SubtitleText.text = cardAsset == null
                ? string.Empty
                : $"{GetCardTypeLabel(cardAsset)}  {FormatCost(cardAsset.Cost)}";

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
        }

        private void EnsureUi()
        {
            if (TryInitializeFromSceneReferences())
            {
                return;
            }

            if (_rootCanvasGroup != null && _rootRectTransform != null && _optionViews.Count > 0)
            {
                return;
            }

            BuildRuntimeFallbackUi();
        }

        private bool TryInitializeFromSceneReferences()
        {
            if (_rootCanvasGroup == null ||
                _titleText == null ||
                _progressText == null ||
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
            contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.sizeDelta = new Vector2(1480f, 650f);

            _titleText = CreateText("TitleText", contentRoot, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchTop(_titleText.rectTransform, 0f, 54f, 20f);

            _progressText = CreateText("ProgressText", contentRoot, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchTop(_progressText.rectTransform, 62f, 38f, 20f);

            _statusText = CreateText("StatusText", contentRoot, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchBottom(_statusText.rectTransform, 0f, 46f, 24f);

            var bodyRow = CreateRectChild("BodyRow", contentRoot);
            bodyRow.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRow.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRow.pivot = new Vector2(0.5f, 0.5f);
            bodyRow.sizeDelta = new Vector2(1380f, 490f);
            bodyRow.anchoredPosition = new Vector2(0f, -6f);

            var bodyLayout = bodyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.childAlignment = TextAnchor.MiddleCenter;
            bodyLayout.childControlHeight = false;
            bodyLayout.childControlWidth = false;
            bodyLayout.childForceExpandHeight = false;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.spacing = 26f;

            var optionsRoot = CreateRectChild("OptionsRoot", bodyRow);
            optionsRoot.sizeDelta = new Vector2(970f, 470f);

            var optionsLayoutElement = optionsRoot.gameObject.AddComponent<LayoutElement>();
            optionsLayoutElement.preferredWidth = 970f;
            optionsLayoutElement.preferredHeight = 470f;

            var horizontalLayoutGroup = optionsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
            horizontalLayoutGroup.childControlHeight = false;
            horizontalLayoutGroup.childControlWidth = false;
            horizontalLayoutGroup.childForceExpandHeight = false;
            horizontalLayoutGroup.childForceExpandWidth = false;
            horizontalLayoutGroup.spacing = 30f;

            _optionViews.Clear();
            for (var i = 0; i < 3; i++)
            {
                _optionViews.Add(CreateOptionView(optionsRoot, i));
            }

            CreateDeckPanel(bodyRow);

            SetVisible(false);
        }

        private void CreateDeckPanel(RectTransform parent)
        {
            var deckPanel = new GameObject("DeckPanel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var deckPanelRectTransform = deckPanel.GetComponent<RectTransform>();
            deckPanelRectTransform.SetParent(parent, false);
            deckPanelRectTransform.sizeDelta = new Vector2(384f, 470f);

            var layoutElement = deckPanel.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 384f;
            layoutElement.preferredHeight = 470f;

            var panelImage = deckPanel.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.12f, 0.16f, 0.96f);
            panelImage.raycastTarget = false;

            _deckPanelTitleText = CreateText("DeckPanelTitle", deckPanelRectTransform, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            _deckPanelTitleText.color = _titleColor;
            StretchTop(_deckPanelTitleText.rectTransform, 14f, 38f, 16f);
            _deckPanelTitleText.text = "Current Deck";

            _deckListText = CreateText("DeckListText", deckPanelRectTransform, 20, FontStyle.Normal, TextAnchor.UpperLeft);
            _deckListText.color = _bodyColor;
            _deckListText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _deckListText.verticalOverflow = VerticalWrapMode.Overflow;
            StretchFill(_deckListText.rectTransform, 60f, 18f, 18f, 16f);
            _deckListText.text = "No picks yet.";
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
            artworkRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            artworkRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            artworkRectTransform.pivot = new Vector2(0.5f, 0.5f);
            artworkRectTransform.sizeDelta = new Vector2(_cardSize.x - 18f, _cardSize.y - 96f);
            artworkRectTransform.anchoredPosition = new Vector2(0f, 20f);

            var artworkImage = artworkObject.GetComponent<Image>();
            artworkImage.preserveAspect = true;
            artworkImage.raycastTarget = false;
            artworkImage.color = _cardFallbackColor;

            var titleText = CreateText("OptionTitle", optionRectTransform, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchBottom(titleText.rectTransform, 44f, 48f, 12f);

            var subtitleText = CreateText("OptionSubtitle", optionRectTransform, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchBottom(subtitleText.rectTransform, 8f, 30f, 12f);

            return new DraftOptionView
            {
                Button = button,
                ArtworkImage = artworkImage,
                TitleText = titleText,
                SubtitleText = subtitleText,
            };
        }

        private Canvas ResolveCanvas()
        {
            if (_targetCanvas != null)
            {
                return _targetCanvas;
            }

            _targetCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (_targetCanvas != null)
            {
                return _targetCanvas;
            }

            var canvasObject = new GameObject("DraftOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _targetCanvas = canvasObject.GetComponent<Canvas>();
            _targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            return _targetCanvas;
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
            var draftFont = ResolveDraftFont();
            if (draftFont != null)
            {
                return draftFont;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
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

        private static string GetCardTypeLabel(CardDefinitionAsset cardAsset)
        {
            switch (cardAsset)
            {
                case UnitCardDefinitionAsset:
                    return "Unit";
                case BuildingCardDefinitionAsset:
                    return "Building";
                case DamageSpellCardDefinitionAsset:
                case PersistentResourceSpellCardDefinitionAsset:
                case ScriptedSpellCardDefinitionAsset:
                    return "Spell";
                default:
                    return "Card";
            }
        }

        private static string FormatCost(ResourceSetData cost)
        {
            var parts = new List<string>();

            if (cost.Mana > 0)
            {
                parts.Add($"M {cost.Mana}");
            }

            if (cost.Qi > 0)
            {
                parts.Add($"Q {cost.Qi}");
            }

            if (cost.Power > 0)
            {
                parts.Add($"P {cost.Power}");
            }

            if (cost.Gold > 0)
            {
                parts.Add($"G {cost.Gold}");
            }

            return parts.Count == 0 ? "Free" : string.Join(" / ", parts);
        }

        private void RefreshDeckList(IReadOnlyList<string> draftedCardIds)
        {
            if (_deckListText == null)
            {
                return;
            }

            _deckListText.text = BuildDeckListText(draftedCardIds);
        }

        private string BuildStatusSummary(IReadOnlyList<string> draftedCardIds)
        {
            var cardCount = draftedCardIds?.Count ?? 0;
            var uniqueCount = CountUniqueCards(draftedCardIds);
            return $"Current Deck: {cardCount} / 33   Unique Cards: {uniqueCount}";
        }

        private string BuildDeckListText(IReadOnlyList<string> draftedCardIds)
        {
            return DraftDeckListFormatter.BuildDeckListText(
                draftedCardIds,
                ResolveCardAsset,
                "No picks yet.");
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

        private static int CountUniqueCards(IReadOnlyList<string> draftedCardIds)
        {
            if (draftedCardIds == null)
            {
                return 0;
            }

            var uniqueCardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cardId in draftedCardIds)
            {
                if (string.IsNullOrWhiteSpace(cardId))
                {
                    continue;
                }

                uniqueCardIds.Add(cardId);
            }

            return uniqueCardIds.Count;
        }
    }
}
