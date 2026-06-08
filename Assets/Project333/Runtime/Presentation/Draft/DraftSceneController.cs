using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Draft
{
    public sealed class DraftSceneController : MonoBehaviour, IDraftOverlayHost
    {
        [SerializeField] private CardDefinitionCatalogAsset _cardCatalogAsset;
        [SerializeField] private DraftOverlayPresenter _draftOverlayPresenter;
        [SerializeField] private Text _recordText;
        [SerializeField] private Text _lastResultText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _deckListText;
        [SerializeField] private ScrollRect _deckListScrollRect;
        [SerializeField] private Button _startDraftButton;
        [SerializeField] private Button _startBattleButton;
        [SerializeField] private Button _returnToStartButton;
        [SerializeField] private Text _startBattleButtonLabel;
        [SerializeField] private bool _autoOpenDraftIfNoDeck = true;
        [SerializeField] private bool _useFixedDraftSeed;
        [SerializeField] private int _fixedDraftSeed = 333;
        [SerializeField] private string _startSceneName = "GameStart_VSlice";
        [SerializeField] private string _draftSceneName = "Draft_VSlice";
        [SerializeField] private string _battleSceneName = "Battle_VSlice";

        private DraftSessionService _draftSessionService;
        private Text _runtimeDeckListDisplayText;
        private Text _runtimeDeckPanelText;
        private Text _runtimeDeckSummaryText;
        private RectTransform _runtimeDeckPanelSurface;

        private void Awake()
        {
            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            EnsureReturnToStartButton();
            EnsureDeckListReferences();
            PrepareDeckListTextLayout();
            _draftOverlayPresenter?.Bind(this);
            RefreshStaticUi();
        }

        private void Start()
        {
            if (_autoOpenDraftIfNoDeck && !DraftRunSessionState.HasDraftedDeckReady)
            {
                BeginDraftFromUi();
            }
        }

        public bool TryGetCardDefinitionAsset(string cardId, out CardDefinitionAsset cardAsset)
        {
            cardAsset = null;
            return _cardCatalogAsset != null &&
                   _cardCatalogAsset.TryGetCardAsset(cardId, out cardAsset);
        }

        public void BeginDraftFromUi()
        {
            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            DraftRunSessionState.ResetForNewDraft();
            _draftSessionService = new DraftSessionService(_cardCatalogAsset, CreateDraftRandom());

            var validation = _draftSessionService.ValidateCatalog();
            if (!validation.IsValid)
            {
                _draftOverlayPresenter?.ShowValidationMessage(validation.Message);
                SetStatus(validation.Message);
                RefreshStaticUi();
                return;
            }

            var openingOffer = _draftSessionService.BeginDraft();
            SyncDraftRunDeckState();
            _draftOverlayPresenter?.ShowOffer(openingOffer, _draftSessionService.DeckState.CardIds);
            SetStatus("Draft started.");
            RefreshStaticUi();
        }

        public void SelectDraftCard(string cardId)
        {
            if (_draftSessionService == null)
            {
                SetStatus("Draft is not active.");
                return;
            }

            try
            {
                var result = _draftSessionService.SelectCard(cardId);
                SetStatus($"Draft picked '{result.SelectedCardId}'.");

                if (result.IsComplete)
                {
                    DraftRunSessionState.SetDraftDeck(result.CompletedDeckCardIds);
                    _draftSessionService = null;
                    _draftOverlayPresenter?.Hide();
                    SetStatus("Draft complete. Press Start Battle when you are ready.");
                    RefreshStaticUi();
                    return;
                }

                SyncDraftRunDeckState();
                _draftOverlayPresenter?.ShowOffer(result.NextOffer, _draftSessionService.DeckState.CardIds);
                RefreshStaticUi();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
                _draftOverlayPresenter?.ShowValidationMessage(ex.Message);
            }
        }

        public void StartBattleFromUi()
        {
            if (!DraftRunSessionState.HasDraftedDeckReady)
            {
                SetStatus("Complete the 33-card draft before starting a battle.");
                RefreshStaticUi();
                return;
            }

            DraftRunSessionState.ConfigureSceneNames(_draftSceneName, _battleSceneName);
            DraftRunSessionState.QueueBattleStart();
            SceneManager.LoadScene(_battleSceneName);
        }

        public void ReturnToStartSceneFromUi()
        {
            SceneManager.LoadScene(_startSceneName);
        }

        private void RefreshStaticUi()
        {
            EnsureReturnToStartButton();
            EnsureDeckListReferences();
            EnsureRuntimeDeckListDisplayText();
            var visibleDeckCardIds = ResolveVisibleDeckCardIds();
            var visibleDeckCount = visibleDeckCardIds?.Count ?? 0;

            if (_recordText != null)
            {
                _recordText.text = $"Wins: {DraftRunSessionState.Wins}   Losses: {DraftRunSessionState.Losses}";
            }

            if (_lastResultText != null)
            {
                _lastResultText.text = string.IsNullOrWhiteSpace(DraftRunSessionState.LastBattleOutcomeText)
                    ? "Last Battle: -"
                    : $"Last Battle: {DraftRunSessionState.LastBattleOutcomeText}";
            }

            if (_deckListText != null)
            {
                var deckListText = BuildDeckListText(visibleDeckCardIds);
                _deckListText.text = deckListText;

                if (_runtimeDeckListDisplayText != null)
                {
                    _runtimeDeckListDisplayText.text = deckListText;
                }

                 if (_runtimeDeckPanelText != null)
                 {
                     _runtimeDeckPanelText.text = deckListText;
                 }

                if (_runtimeDeckSummaryText != null)
                {
                    _runtimeDeckSummaryText.text = BuildDeckSummaryText(visibleDeckCardIds);
                }

                RefreshDeckListViewport();
            }

            var hasDraftDeck = DraftRunSessionState.HasDraftedDeckReady;
            if (_startBattleButton != null)
            {
                _startBattleButton.interactable = hasDraftDeck;
            }

            if (_startBattleButtonLabel != null)
            {
                _startBattleButtonLabel.text = hasDraftDeck
                    ? "Start Battle"
                    : $"Start Battle ({visibleDeckCount}/33)";
            }

            if (_startDraftButton != null)
            {
                _startDraftButton.interactable = true;
            }

            if (_returnToStartButton != null)
            {
                _returnToStartButton.interactable = true;
            }
        }

        private void EnsureReturnToStartButton()
        {
            if (_returnToStartButton != null)
            {
                return;
            }

            if (transform.Find("ControlPanel") is not RectTransform controlPanel)
            {
                return;
            }

            var buttonObject = new GameObject(
                "ReturnToStartButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(controlPanel, false);
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = new Vector2(340f, 72f);
            buttonRect.anchoredPosition = new Vector2(0f, -204f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.24f, 0.24f, 0.28f, 1f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(ReturnToStartSceneFromUi);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 12f);
            labelRect.offsetMax = new Vector2(-12f, -12f);

            var labelText = labelObject.GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontStyle = FontStyle.Bold;
            labelText.fontSize = 28;
            labelText.color = Color.white;
            labelText.text = "Back To Start";
            labelText.raycastTarget = false;
            labelText.font = ResolveRuntimeFont();

            RepositionControlPanelStatusArea(controlPanel);
            _returnToStartButton = button;
        }

        private static Font ResolveRuntimeFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void RepositionControlPanelStatusArea(RectTransform controlPanel)
        {
            if (controlPanel.Find("StatusTitleText") is RectTransform statusTitleRect)
            {
                statusTitleRect.offsetMin = new Vector2(28f, -302f);
                statusTitleRect.offsetMax = new Vector2(-28f, -264f);
            }

            if (controlPanel.Find("StatusText") is RectTransform statusTextRect)
            {
                statusTextRect.offsetMin = new Vector2(28f, 24f);
                statusTextRect.offsetMax = new Vector2(-28f, -306f);
            }
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = string.IsNullOrWhiteSpace(status) ? "Ready." : status;
            }
        }

        private string BuildDeckListText(IReadOnlyList<string> draftedCardIds)
        {
            return DraftDeckListFormatter.BuildDeckListText(
                draftedCardIds,
                ResolveCardAsset,
                "No drafted deck yet.",
                "•  ");
        }

        private string BuildDeckSummaryText(IReadOnlyList<string> draftedCardIds)
        {
            var cardCount = draftedCardIds?.Count ?? 0;
            var phaseLabel = cardCount >= 33 ? "Battle Ready" : "Drafting";
            return $"{phaseLabel}   {cardCount}/33 Cards";
        }

        private IReadOnlyList<string> ResolveVisibleDeckCardIds()
        {
            if (DraftRunSessionState.CurrentDraftDeckCardIds.Count > 0)
            {
                return DraftRunSessionState.CurrentDraftDeckCardIds;
            }

            if (_draftSessionService?.DeckState?.CardIds != null)
            {
                return _draftSessionService.DeckState.CardIds;
            }

            return DraftRunSessionState.CurrentDraftDeckCardIds;
        }

        private void SyncDraftRunDeckState()
        {
            if (_draftSessionService?.DeckState?.CardIds == null)
            {
                return;
            }

            DraftRunSessionState.SetDraftDeck(_draftSessionService.DeckState.CardIds);
        }

        private void RefreshDeckListViewport()
        {
            PrepareDeckListTextLayout();
            EnsureRuntimeDeckListDisplayText();
            EnsureRuntimeDeckPanelText();

            if (_deckListScrollRect == null)
            {
                return;
            }

            var viewportHeight = _deckListScrollRect.viewport != null
                ? Mathf.Max(1f, _deckListScrollRect.viewport.rect.height)
                : 1f;
            var activeDeckListText = _runtimeDeckListDisplayText != null ? _runtimeDeckListDisplayText : _deckListText;
            var preferredTextHeight = activeDeckListText != null
                ? Mathf.Max(activeDeckListText.preferredHeight + 16f, viewportHeight)
                : viewportHeight;

            var contentRect = _deckListScrollRect.content;
            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.offsetMin = new Vector2(0f, -preferredTextHeight);
                contentRect.offsetMax = new Vector2(0f, 0f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }

            if (_runtimeDeckListDisplayText != null)
            {
                var runtimeTextRect = _runtimeDeckListDisplayText.rectTransform;
                runtimeTextRect.anchorMin = new Vector2(0f, 1f);
                runtimeTextRect.anchorMax = new Vector2(1f, 1f);
                runtimeTextRect.pivot = new Vector2(0.5f, 1f);
                runtimeTextRect.offsetMin = new Vector2(12f, -preferredTextHeight);
                runtimeTextRect.offsetMax = new Vector2(-12f, 0f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(runtimeTextRect);
            }

            Canvas.ForceUpdateCanvases();
            if (contentRect != null)
            {
                contentRect.anchoredPosition = Vector2.zero;
            }

            _deckListScrollRect.verticalNormalizedPosition = 1f;
        }

        private void PrepareDeckListTextLayout()
        {
            if (_deckListText == null)
            {
                return;
            }

            _deckListText.alignment = TextAnchor.UpperLeft;
            _deckListText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _deckListText.verticalOverflow = VerticalWrapMode.Overflow;

            var textRect = _deckListText.rectTransform;
            if (textRect == null)
            {
                return;
            }

            var targetHeight = Mathf.Max(_deckListText.preferredHeight + 16f, 64f);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = new Vector2(12f, -targetHeight);
            textRect.offsetMax = new Vector2(-12f, 0f);
        }

        private void EnsureDeckListReferences()
        {
            if (!IsOverlayDeckListText(_deckListText) &&
                _deckListText != null &&
                _deckListText.GetComponentInParent<ScrollRect>() != null)
            {
                if (_deckListScrollRect == null)
                {
                    _deckListScrollRect = _deckListText.GetComponentInParent<ScrollRect>();
                }

                return;
            }

            var overlayTransform = _draftOverlayPresenter != null ? _draftOverlayPresenter.transform : null;
            var sceneTexts = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None);
            for (var i = 0; i < sceneTexts.Length; i++)
            {
                var text = sceneTexts[i];
                if (text == null || !string.Equals(text.gameObject.name, "DeckListText", StringComparison.Ordinal))
                {
                    continue;
                }

                if (overlayTransform != null && text.transform.IsChildOf(overlayTransform))
                {
                    continue;
                }

                var parentScrollRect = text.GetComponentInParent<ScrollRect>();
                if (parentScrollRect == null)
                {
                    continue;
                }

                _deckListText = text;
                _deckListScrollRect = parentScrollRect;
                return;
            }

            if (_deckListScrollRect == null && _deckListText != null)
            {
                _deckListScrollRect = _deckListText.GetComponentInParent<ScrollRect>();
            }
        }

        private bool IsOverlayDeckListText(Text deckListText)
        {
            return deckListText != null &&
                   _draftOverlayPresenter != null &&
                   deckListText.transform.IsChildOf(_draftOverlayPresenter.transform);
        }

        private void EnsureRuntimeDeckListDisplayText()
        {
            if (_runtimeDeckListDisplayText != null)
            {
                return;
            }

            if (_deckListScrollRect == null || _deckListScrollRect.content == null)
            {
                return;
            }

            var displayObject = new GameObject("RuntimeDeckListDisplayText", typeof(RectTransform), typeof(Text));
            var rectTransform = displayObject.GetComponent<RectTransform>();
            rectTransform.SetParent(_deckListScrollRect.content, false);
            rectTransform.SetAsLastSibling();

            var displayText = displayObject.GetComponent<Text>();
            if (_deckListText != null)
            {
                displayText.font = _deckListText.font;
                displayText.fontSize = _deckListText.fontSize;
                displayText.fontStyle = _deckListText.fontStyle;
                displayText.color = _deckListText.color;
            }
            else
            {
                displayText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                displayText.fontSize = 22;
                displayText.fontStyle = FontStyle.Normal;
                displayText.color = Color.white;
            }

            displayText.alignment = TextAnchor.UpperLeft;
            displayText.horizontalOverflow = HorizontalWrapMode.Wrap;
            displayText.verticalOverflow = VerticalWrapMode.Overflow;
            displayText.supportRichText = false;
            displayText.raycastTarget = false;
            displayText.text = string.Empty;

            _runtimeDeckListDisplayText = displayText;

            if (_deckListText != null)
            {
                _deckListText.enabled = false;
            }
        }

        private void EnsureRuntimeDeckPanelText()
        {
            if (_runtimeDeckPanelText != null)
            {
                return;
            }

            if (_deckListScrollRect == null)
            {
                return;
            }

            var panelRect = _deckListScrollRect.transform.parent as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            var surfaceObject = new GameObject("RuntimeDeckPanelSurface", typeof(RectTransform), typeof(Image), typeof(Outline));
            var surfaceRect = surfaceObject.GetComponent<RectTransform>();
            surfaceRect.SetParent(panelRect, false);
            surfaceRect.SetAsLastSibling();
            surfaceRect.anchorMin = new Vector2(0f, 0f);
            surfaceRect.anchorMax = new Vector2(1f, 1f);
            surfaceRect.pivot = new Vector2(0.5f, 0.5f);
            surfaceRect.offsetMin = new Vector2(20f, 20f);
            surfaceRect.offsetMax = new Vector2(-20f, -68f);

            var surfaceImage = surfaceObject.GetComponent<Image>();
            surfaceImage.color = new Color(0.07f, 0.095f, 0.14f, 0.96f);
            surfaceImage.raycastTarget = false;

            var surfaceOutline = surfaceObject.GetComponent<Outline>();
            surfaceOutline.effectColor = new Color(0.84f, 0.74f, 0.42f, 0.42f);
            surfaceOutline.effectDistance = new Vector2(1.2f, -1.2f);
            surfaceOutline.useGraphicAlpha = true;

            var summaryBarObject = new GameObject("RuntimeDeckSummaryBar", typeof(RectTransform), typeof(Image));
            var summaryBarRect = summaryBarObject.GetComponent<RectTransform>();
            summaryBarRect.SetParent(surfaceRect, false);
            summaryBarRect.anchorMin = new Vector2(0f, 1f);
            summaryBarRect.anchorMax = new Vector2(1f, 1f);
            summaryBarRect.pivot = new Vector2(0.5f, 1f);
            summaryBarRect.offsetMin = new Vector2(12f, -52f);
            summaryBarRect.offsetMax = new Vector2(-12f, -12f);

            var summaryBarImage = summaryBarObject.GetComponent<Image>();
            summaryBarImage.color = new Color(0.15f, 0.2f, 0.29f, 0.95f);
            summaryBarImage.raycastTarget = false;

            var summaryTextObject = new GameObject("RuntimeDeckSummaryText", typeof(RectTransform), typeof(Text));
            var summaryTextRect = summaryTextObject.GetComponent<RectTransform>();
            summaryTextRect.SetParent(summaryBarRect, false);
            summaryTextRect.anchorMin = Vector2.zero;
            summaryTextRect.anchorMax = Vector2.one;
            summaryTextRect.offsetMin = new Vector2(12f, 0f);
            summaryTextRect.offsetMax = new Vector2(-12f, 0f);

            var summaryText = summaryTextObject.GetComponent<Text>();
            summaryText.raycastTarget = false;
            summaryText.alignment = TextAnchor.MiddleCenter;
            summaryText.horizontalOverflow = HorizontalWrapMode.Overflow;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            summaryText.supportRichText = false;

            var panelObject = new GameObject("RuntimeCurrentDraftDeckText", typeof(RectTransform), typeof(Text));
            var rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(surfaceRect, false);
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(16f, 18f);
            rectTransform.offsetMax = new Vector2(-16f, -64f);

            var panelText = panelObject.GetComponent<Text>();
            if (_deckListText != null)
            {
                summaryText.font = _deckListText.font;
                summaryText.fontSize = Mathf.Max(16, _deckListText.fontSize - 4);
                summaryText.fontStyle = FontStyle.Bold;
                summaryText.color = new Color(0.98f, 0.95f, 0.84f, 1f);

                panelText.font = _deckListText.font;
                panelText.fontSize = Mathf.Max(18, _deckListText.fontSize - 1);
                panelText.fontStyle = _deckListText.fontStyle;
                panelText.color = new Color(0.93f, 0.96f, 1f, 1f);
            }
            else
            {
                summaryText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                summaryText.fontSize = 18;
                summaryText.fontStyle = FontStyle.Bold;
                summaryText.color = new Color(0.98f, 0.95f, 0.84f, 1f);

                panelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                panelText.fontSize = 20;
                panelText.fontStyle = FontStyle.Normal;
                panelText.color = new Color(0.93f, 0.96f, 1f, 1f);
            }

            panelText.alignment = TextAnchor.UpperLeft;
            panelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            panelText.verticalOverflow = VerticalWrapMode.Overflow;
            panelText.supportRichText = false;
            panelText.raycastTarget = false;
            panelText.text = string.Empty;

            _runtimeDeckPanelSurface = surfaceRect;
            _runtimeDeckPanelText = panelText;
            _runtimeDeckSummaryText = summaryText;
            _deckListScrollRect.gameObject.SetActive(false);
        }


        private CardDefinitionAsset ResolveCardAsset(string cardId)
        {
            return TryGetCardDefinitionAsset(cardId, out var cardAsset)
                ? cardAsset
                : null;
        }

        private System.Random CreateDraftRandom()
        {
            return _useFixedDraftSeed
                ? new System.Random(_fixedDraftSeed)
                : new System.Random();
        }
    }
}
