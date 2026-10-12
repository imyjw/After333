using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.OwnedCards
{
    // Shared by the existing scene, runtime materialization, and the editor builder.
    public sealed partial class OwnedCardsSceneController
    {
        private static readonly Color CollectionInk = new Color(.026f, .063f, .073f, 1f);
        private static readonly Color CollectionPanel = new Color(.045f, .105f, .115f, 1f);
        private static readonly Color CollectionGold = new Color(.78f, .59f, .29f, 1f);
        private static readonly Color CollectionCream = new Color(.96f, .89f, .72f, 1f);
        private static readonly Color CollectionMuted = new Color(.63f, .77f, .76f, 1f);
        private RectTransform _detailModalRoot;
        private Text _detailStatusText;
        private Button _detailCloseButton;
        private ScrollRect _detailScroll;
        private CanvasGroup _galleryInteraction;
        private CanvasGroup _headerInteraction;
        private bool _galleryWasInteractable;
        private bool _headerWasInteractable;
        private bool _isCardDetailOpen;
        private GameObject _selectionBeforeDetail;
        private Vector2 _lastModalAvailableSize;

        public bool IsCardDetailOpen => _isCardDetailOpen;

        private void OpenCardDetail()
        {
            if (_detailModalRoot == null) return;
            if (!_isCardDetailOpen)
            {
                _selectionBeforeDetail = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                _galleryWasInteractable = _galleryInteraction == null || _galleryInteraction.interactable;
                _headerWasInteractable = _headerInteraction == null || _headerInteraction.interactable;
            }
            _isCardDetailOpen = true;
            if (_galleryInteraction != null) _galleryInteraction.interactable = false;
            if (_headerInteraction != null) _headerInteraction.interactable = false;
            _detailModalRoot.gameObject.SetActive(true);
            _detailModalRoot.SetAsLastSibling();
            ResizeCardDetailWindow();
            Canvas.ForceUpdateCanvases();
            if (_detailScroll != null)
            {
                _detailScroll.StopMovement();
                _detailScroll.verticalNormalizedPosition = 1f;
            }
            _detailStatLayoutDirty = true;
            if (EventSystem.current != null && _detailCloseButton != null)
                EventSystem.current.SetSelectedGameObject(_detailCloseButton.gameObject);
        }

        public void CloseCardDetailFromUi()
        {
            if (IsTutorialCollectionMode && isActiveAndEnabled && !_tutorialCollectionAllowDismiss) return;
            ResetDetailArtworkFlipToFront();
            if (!_isCardDetailOpen) return;
            _isCardDetailOpen = false;
            if (_detailModalRoot != null) _detailModalRoot.gameObject.SetActive(false);
            if (_galleryInteraction != null) _galleryInteraction.interactable = _galleryWasInteractable;
            if (_headerInteraction != null) _headerInteraction.interactable = _headerWasInteractable;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_selectionBeforeDetail != null && _selectionBeforeDetail.activeInHierarchy
                    ? _selectionBeforeDetail : null);
            _selectionBeforeDetail = null;
            // Only dismiss presentation: an in-flight server upgrade keeps its original request ID.
        }

        private void ApplyCollectionGalleryLayout()
        {
            if (_cardGridRoot == null || !(transform is RectTransform root)) return;
            _collectionVisualVersion = 2;
            _gridSlotColor = CollectionInk;
            _gridSlotEmptyArtworkColor = new Color(.075f, .15f, .16f, 1f);
            _ownedCountTextColor = CollectionCream;
            _upgradeReadyGlowColor = new Color(.39f, .85f, .55f, .85f);
            _upgradeReadyGlowDistance = new Vector2(2f, -2f);
            _detailPanelColor = CollectionPanel;
            _detailAccentColor = CollectionCream;
            _arrowButtonColor = CollectionInk;
            _upgradeButtonColor = Color.white;
            _upgradeButtonUnavailableColor = Color.white;

            var background = root.Find("Background")?.GetComponent<Image>();
            if (background != null) background.color = CollectionInk;
            var header = root.Find("HeaderPanel") as RectTransform;
            if (header != null)
            {
                Region(header, 0f, .875f, 1f, 1f);
                _headerInteraction = GalleryComponent<CanvasGroup>(header);
            }
            if (_titleText != null)
            {
                _titleText.text = "보유 카드";
                GalleryText(_titleText, 42, CollectionCream, TextAnchor.MiddleLeft);
            }
            if (_backButtonLabel != null) _backButtonLabel.text = "시작 화면";

            var collection = (RectTransform)_cardGridRoot.parent;
            Region(collection, .025f, .085f, .975f, .855f);
            PaintCollectionPanel(collection, CollectionPanel);
            _galleryInteraction = GalleryComponent<CanvasGroup>(collection);
            var oldScroll = collection.Find("Scroll View");
            if (oldScroll != null) oldScroll.gameObject.SetActive(false);
            var heading = GalleryLabel(collection, "CollectionHeading", "카드 컬렉션", 25, CollectionCream, TextAnchor.MiddleLeft);
            Region(heading.rectTransform, .018f, .925f, .35f, .993f);
            var hint = GalleryLabel(collection, "CollectionHint", "카드를 선택해 정보와 강화 내용을 확인하세요", 20, CollectionMuted, TextAnchor.MiddleRight);
            Region(hint.rectTransform, .37f, .925f, .98f, .993f);
            Region(_cardGridRoot, 0f, 0f, 1f, 1f);
            _cardGridRoot.offsetMin = new Vector2(72f, 70f);
            _cardGridRoot.offsetMax = new Vector2(-72f, -68f);
            var grid = _cardGridRoot.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumnCount;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset();
            if (_cardGridRoot.GetComponent<OwnedCardsResponsiveGrid>() == null)
                _cardGridRoot.gameObject.AddComponent<OwnedCardsResponsiveGrid>();
            foreach (Transform slot in _cardGridRoot)
            {
                var artwork = slot.Find("Artwork") as RectTransform;
                if (artwork == null) continue;
                Region(artwork, 0f, .12f, 1f, 1f, 5f);
                var count = slot.Find("CountLevelText")?.GetComponent<Text>();
                if (count != null)
                {
                    Region(count.rectTransform, 0f, 0f, 1f, .12f, 2f);
                    GalleryText(count, 23, CollectionCream, TextAnchor.MiddleCenter);
                    count.resizeTextForBestFit = true;
                    count.resizeTextMinSize = 14;
                    count.resizeTextMaxSize = 23;
                }
            }
            GalleryPageArrow(_previousPageButton, false);
            GalleryPageArrow(_nextPageButton, true);
            if (_pageText != null)
            {
                Region(_pageText.rectTransform, .41f, .018f, .59f, .072f);
                GalleryText(_pageText, 25, CollectionCream, TextAnchor.MiddleCenter);
            }
            var legend = GalleryLabel(collection, "UpgradeLegend", "초록 테두리 · 강화 가능", 19, new Color(.43f, .85f, .53f), TextAnchor.MiddleRight);
            Region(legend.rectTransform, .62f, .018f, .98f, .072f);
            var total = GalleryLabel(collection, "CollectionTotal", $"전체 {_cardGridEntries.Count}종", 19, CollectionMuted, TextAnchor.MiddleLeft);
            Region(total.rectTransform, .02f, .018f, .37f, .072f);
            if (_statusText != null)
            {
                Region(_statusText.rectTransform, .035f, .012f, .965f, .065f);
                GalleryText(_statusText, 20, CollectionMuted, TextAnchor.MiddleCenter);
            }
        }

        private void ApplyCardDetailModalLayout()
        {
            if (_detailPanelRoot == null || !(transform is RectTransform root)) return;
            if (_detailModalRoot == null)
                _detailModalRoot = GalleryRect(root, "CardDetailModal");
            StretchFull(_detailModalRoot);
            _detailModalRoot.SetAsLastSibling();
            var backdrop = GalleryRect(_detailModalRoot, "Backdrop");
            StretchFull(backdrop);
            PaintCollectionPanel(backdrop, new Color(.005f, .012f, .018f, .84f));
            var backdropButton = GalleryComponent<Button>(backdrop);
            backdropButton.targetGraphic = backdrop.GetComponent<Image>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.navigation = new Navigation { mode = Navigation.Mode.None };
            backdropButton.onClick.RemoveListener(CloseCardDetailFromUi);
            backdropButton.onClick.AddListener(CloseCardDetailFromUi);
            backdrop.SetAsFirstSibling();
            if (_detailPanelRoot.parent != _detailModalRoot) _detailPanelRoot.SetParent(_detailModalRoot, false);
            _detailPanelRoot.SetAsLastSibling();
            _detailPanelRoot.anchorMin = _detailPanelRoot.anchorMax = _detailPanelRoot.pivot = new Vector2(.5f, .5f);
            _detailPanelRoot.anchoredPosition = Vector2.zero;
            PaintCollectionPanel(_detailPanelRoot, CollectionPanel);
            var border = GalleryComponent<Outline>(_detailPanelRoot);
            border.effectColor = CollectionGold;
            border.effectDistance = new Vector2(2f, -2f);
            border.useGraphicAlpha = false;
            _lastModalAvailableSize = Vector2.zero;
            ResizeCardDetailWindow();
            Region(_detailTitleText.rectTransform, .045f, .895f, .85f, .98f);
            GalleryText(_detailTitleText, 38, CollectionCream, TextAnchor.MiddleLeft);
            _detailTitleText.resizeTextForBestFit = true;
            _detailTitleText.resizeTextMinSize = 22;
            _detailTitleText.resizeTextMaxSize = 38;
            var rule = GalleryRect(_detailPanelRoot, "ModalGoldRule");
            Region(rule, .045f, .878f, .955f, .88f);
            PaintCollectionPanel(rule, CollectionGold, false);
            Region(_detailArtworkImage.rectTransform, .04f, .105f, .395f, .85f);
            Region(_detailOwnershipText.rectTransform, .44f, .585f, .953f, .85f);
            GalleryText(_detailOwnershipText, 25, CollectionCream, TextAnchor.UpperLeft);
            _detailOwnershipText.resizeTextForBestFit = true;
            _detailOwnershipText.resizeTextMinSize = 20;
            _detailOwnershipText.resizeTextMaxSize = 25;
            var info = GalleryRect(_detailPanelRoot, "DetailInfoScroll");
            Region(info, .43f, .22f, .955f, .56f);
            PaintCollectionPanel(info, CollectionInk);
            var viewport = GalleryRect(info, "Viewport");
            StretchFull(viewport, 16f);
            PaintCollectionPanel(viewport, Color.clear);
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var initializeScrollContent = _detailScroll == null || _detailMetaText.transform.parent != viewport;
            if (_detailMetaText.transform.parent != viewport) _detailMetaText.transform.SetParent(viewport, false);
            if (initializeScrollContent)
            {
                Region(_detailMetaText.rectTransform, 0f, 1f, 1f, 1f);
                _detailMetaText.rectTransform.pivot = new Vector2(.5f, 1f);
            }
            GalleryText(_detailMetaText, 24, new Color(.82f, .88f, .85f), TextAnchor.UpperLeft);
            _detailMetaText.lineSpacing = 1.12f;
            _detailMetaText.supportRichText = true;
            _detailMetaText.verticalOverflow = VerticalWrapMode.Overflow;
            var fitter = GalleryComponent<ContentSizeFitter>(_detailMetaText.rectTransform);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _detailScroll = GalleryComponent<ScrollRect>(info);
            _detailScroll.viewport = viewport;
            _detailScroll.content = _detailMetaText.rectTransform;
            _detailScroll.horizontal = false;
            _detailScroll.vertical = true;
            _detailScroll.movementType = ScrollRect.MovementType.Clamped;
            _detailScroll.scrollSensitivity = 36f;
            var scrollHint = GalleryLabel(_detailPanelRoot, "ScrollHint", "카드 정보 · 위아래로 스크롤", 17, CollectionMuted, TextAnchor.MiddleLeft);
            Region(scrollHint.rectTransform, .44f, .18f, .955f, .22f);
            Region((RectTransform)_upgradeButton.transform, .59f, .075f, .955f, .165f);
            GalleryText(_upgradeButtonLabel, 28, CollectionCream, TextAnchor.MiddleCenter);
            _detailStatusText = GalleryLabel(_detailPanelRoot, "DetailStatusText", _statusText != null ? _statusText.text : string.Empty,
                19, CollectionMuted, TextAnchor.MiddleLeft);
            Region(_detailStatusText.rectTransform, .44f, .012f, .955f, .065f);
            var close = GalleryRect(_detailPanelRoot, "CloseButton");
            Region(close, .88f, .902f, .958f, .974f);
            PaintCollectionPanel(close, CollectionInk);
            _detailCloseButton = GalleryComponent<Button>(close);
            _detailCloseButton.targetGraphic = close.GetComponent<Image>();
            _detailCloseButton.onClick.RemoveListener(CloseCardDetailFromUi);
            _detailCloseButton.onClick.AddListener(CloseCardDetailFromUi);
            var closeLabel = GalleryLabel(close, "Label", "닫기", 23, CollectionCream, TextAnchor.MiddleCenter);
            StretchFull(closeLabel.rectTransform);
            EnsureDetailArtworkFlipUi();
            _detailModalRoot.gameObject.SetActive(_isCardDetailOpen);
        }

        private void ResizeCardDetailWindow()
        {
            if (_detailModalRoot == null || _detailPanelRoot == null) return;
            var available = ((RectTransform)transform).rect.size;
            var tutorialPosition = new Vector2(0, available.y * .055f);
            if (available == _lastModalAvailableSize && (!IsTutorialCollectionMode || _detailPanelRoot.anchoredPosition == tutorialPosition)) return;
            _lastModalAvailableSize = available;
            _detailPanelRoot.sizeDelta = new Vector2(Mathf.Min(1360f, available.x * .90f), Mathf.Min(920f, available.y * (IsTutorialCollectionMode ? .68f : .88f)));
            _detailPanelRoot.anchoredPosition = IsTutorialCollectionMode ? new Vector2(0, available.y * .055f) : Vector2.zero;
            _detailStatLayoutDirty = true;
        }

        private void GalleryPageArrow(Button button, bool right)
        {
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1f : 0f, .5f);
            rect.sizeDelta = new Vector2(52f, 104f);
            rect.anchoredPosition = new Vector2(right ? -10f : 10f, 0f);
            PaintCollectionPanel(rect, CollectionInk);
            GalleryText(button.GetComponentInChildren<Text>(true), 38, CollectionCream, TextAnchor.MiddleCenter);
        }

        private RectTransform GalleryRect(RectTransform parent, string name)
        {
            if (parent.Find(name) is RectTransform existing) return existing;
            var obj = new GameObject(name, typeof(RectTransform));
            RegisterEditorCreatedObject(obj);
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private Text GalleryLabel(RectTransform parent, string name, string value, int size, Color color, TextAnchor alignment)
        {
            var rect = GalleryRect(parent, name);
            var text = GalleryComponent<Text>(rect);
            if (text.font == null) text.font = _titleText != null && _titleText.font != null ? _titleText.font : ResolveRuntimeFont();
            text.text = value;
            GalleryText(text, size, color, alignment);
            return text;
        }

        private static void GalleryText(Text text, int size, Color color, TextAnchor alignment)
        {
            if (text == null) return;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static void Region(RectTransform rect, float x0, float y0, float x1, float y1, float inset = 0f)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static T GalleryComponent<T>(RectTransform rect) where T : Component
        {
            var component = rect.GetComponent<T>();
            // Unity's missing native objects must use Unity null equality, not CLR coalescing.
            if (component == null) component = rect.gameObject.AddComponent<T>();
            return component;
        }
        private static void PaintCollectionPanel(RectTransform rect, Color color, bool blocksInput = true)
        {
            var image = GalleryComponent<Image>(rect);
            image.color = color;
            image.raycastTarget = blocksInput;
        }
    }
}