using System;
using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Runtime.Presentation.Shop
{
    public enum ShopCardPackRevealPhase { Hidden, Sealed, Tearing, Ejecting, Results }

    /// <summary>An already-paid card copy. The reveal never changes wallet or collection state.</summary>
    public sealed class ShopCardPackRevealItem
    {
        public string CardId { get; }
        public string CardName { get; }
        public CardRarity Rarity { get; }
        public Sprite Artwork { get; }

        public ShopCardPackRevealItem(string cardId, string cardName, CardRarity rarity, Sprite artwork)
        {
            if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("Card ID is required.", nameof(cardId));
            CardId = cardId.Trim();
            CardName = string.IsNullOrWhiteSpace(cardName) ? CardId : cardName.Trim();
            Rarity = rarity;
            Artwork = artwork;
        }
    }

    /// <summary>Reusable, unscaled-time pack opening over the shop's existing Canvas.</summary>
    public sealed class ShopCardPackRevealView : MonoBehaviour
    {
        private const int CardCount = 10;
        private const float TearDuration = .48f;
        private const float EjectDuration = 1.22f;
        private const float ResultEntranceDuration = .32f;
        private const float DismissDelay = .55f;
        private static readonly Color Gold = new Color(1f, .81f, .34f);
        private Action _dismissed;
        private RectTransform _safeArea;
        private RectTransform _pack;
        private RectTransform _results;
        private RectTransform _grid;
        private UnityEngine.UI.Image _closed;
        private UnityEngine.UI.Image _open;
        private UnityEngine.UI.Image _tornTop;
        private UnityEngine.UI.Image _packGlow;
        private UnityEngine.UI.Button _packButton;
        private UnityEngine.UI.Button _dismissButton;
        private UnityEngine.UI.Text _title;
        private UnityEngine.UI.Text _hint;
        private UnityEngine.UI.Text _packFallback;
        private readonly FlyingCard[] _flying = new FlyingCard[CardCount];
        private readonly CardSlot[] _slots = new CardSlot[CardCount];
        private Sprite _glowSprite;
        private Texture2D _glowTexture;
        private ShopCardPackRevealItem[] _items;
        private float _elapsed;
        private float _ambient;
        private float _packSize;
        private Vector2 _lastSize;
        private Rect _lastSafeArea;

        private sealed class FlyingCard
        {
            public RectTransform Root;
            public CanvasGroup Group;
        }

        private sealed class CardSlot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform Card;
            public UnityEngine.UI.Image Artwork;
            public UnityEngine.UI.Text Fallback;
            public UnityEngine.UI.Text Name;
            public UnityEngine.UI.Text Rarity;
            public UnityEngine.UI.Image[] Edges;
            public UnityEngine.UI.Image[] Sparks;
            public Color Aura;
            public int SparkCount;
            public Vector2 Position;
            public Vector2 ArtworkSize;
        }

        public ShopCardPackRevealPhase Phase { get; private set; }
        public bool IsShowing => Phase != ShopCardPackRevealPhase.Hidden;
        public bool IsAnimating => Phase == ShopCardPackRevealPhase.Tearing || Phase == ShopCardPackRevealPhase.Ejecting;

        public static ShopCardPackRevealView Create(RectTransform parent, Action dismissed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var root = Rect("ShopCardPackReveal", parent);
            Stretch(root);
            var view = root.gameObject.AddComponent<ShopCardPackRevealView>();
            view._dismissed = dismissed;
            view.Build();
            view.Hide();
            return view;
        }

        public void ShowPack(IReadOnlyList<ShopCardPackRevealItem> cards)
        {
            if (cards == null || cards.Count != CardCount) throw new ArgumentException("A paid pack must contain exactly ten card copies.", nameof(cards));
            for (var i = 0; i < CardCount; i++)
                if (cards[i] == null) throw new ArgumentException("A paid card copy cannot be null.", nameof(cards));
            // A refresh cannot restart an opening or replace the receipt already being reviewed.
            if (IsShowing) return;
            _items = new ShopCardPackRevealItem[CardCount];
            for (var i = 0; i < CardCount; i++) _items[i] = cards[i];
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Phase = ShopCardPackRevealPhase.Sealed;
            _elapsed = 0;
            _ambient = 0;
            _packButton.interactable = true;
            _dismissButton.interactable = false;
            _pack.gameObject.SetActive(true);
            _results.gameObject.SetActive(false);
            _title.text = "카드팩";
            _hint.text = "카드팩을 눌러 개봉하세요";
            _hint.gameObject.SetActive(true);
            foreach (var card in _flying) card.Group.alpha = 0;
            SetPackPose(0);
            _lastSize = Vector2.zero;
            RefreshLayout();
        }

        public void Hide()
        {
            Phase = ShopCardPackRevealPhase.Hidden;
            _items = null;
            gameObject.SetActive(false);
        }

        private void OpenPack()
        {
            if (Phase != ShopCardPackRevealPhase.Sealed) return;
            Phase = ShopCardPackRevealPhase.Tearing;
            _elapsed = 0;
            _packButton.interactable = false;
            _hint.text = "카드팩을 여는 중입니다";
        }

        private void DismissResults()
        {
            if (Phase != ShopCardPackRevealPhase.Results || !_dismissButton.interactable) return;
            Hide();
            _dismissed?.Invoke();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        // Kept deterministic so the same native UI can be exercised without real-time waits.
        private void Advance(float delta)
        {
            if (!IsShowing) return;
            delta = Mathf.Max(0, delta);
            _ambient += delta;
            RefreshLayout();
            if (Phase == ShopCardPackRevealPhase.Sealed)
            {
                _pack.localScale = Vector3.one * (1 + .012f * Mathf.Sin(_ambient * 2.2f));
                _packGlow.color = new Color(.8f, .91f, 1f, .16f + .025f * Mathf.Sin(_ambient * 2.2f));
                return;
            }
            _elapsed += delta;
            if (Phase == ShopCardPackRevealPhase.Tearing)
            {
                SetPackPose(Mathf.Clamp01(_elapsed / TearDuration));
                if (_elapsed < TearDuration) return;
                _elapsed -= TearDuration;
                Phase = ShopCardPackRevealPhase.Ejecting;
            }
            if (Phase == ShopCardPackRevealPhase.Ejecting)
            {
                AnimateEjection(_elapsed);
                if (_elapsed < EjectDuration) return;
                // Do not transfer excess time into the dismissal guard. The click that opens
                // a pack must never also dismiss its results after a slow frame.
                ShowResults();
                return;
            }
            if (Phase == ShopCardPackRevealPhase.Results)
            {
                _dismissButton.interactable = _elapsed >= DismissDelay;
                _hint.gameObject.SetActive(_dismissButton.interactable);
                AnimateResults();
            }
        }

        private void OnDisable()
        {
            Phase = ShopCardPackRevealPhase.Hidden;
            _items = null;
        }

        private void OnDestroy()
        {
            Release(_glowSprite);
            Release(_glowTexture);
        }

        private void Build()
        {
            var backdrop = Image("DimmedBackground", transform, new Color(.004f, .008f, .017f, .85f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            _dismissButton = backdrop.gameObject.AddComponent<UnityEngine.UI.Button>();
            _dismissButton.targetGraphic = backdrop;
            _dismissButton.transition = UnityEngine.UI.Selectable.Transition.None;
            _dismissButton.onClick.AddListener(DismissResults);
            _safeArea = Rect("SafeArea", transform);
            Stretch(_safeArea);
            _title = Text("Title", _safeArea, "카드팩", 40, Gold);
            _hint = Text("Hint", _safeArea, "", 26, new Color(.89f, .92f, .98f));
            _pack = Rect("CardPack", _safeArea);
            _glowSprite = CreateGlowSprite();
            _packGlow = Image("PackLight", _pack, new Color(.8f, .91f, 1f, .16f));
            _packGlow.sprite = _glowSprite;
            Stretch(_packGlow.rectTransform);
            var sprites = Resources.LoadAll<Sprite>("Project333/UI/Shop/CardPackStates");
            _closed = Image("ClosedPack", _pack, Color.white);
            _open = Image("OpenPack", _pack, new Color(1, 1, 1, 0));
            _tornTop = Image("TornTop", _pack, new Color(1, 1, 1, 0));
            foreach (var sprite in sprites)
            {
                if (sprite.name == "CardPackClosed") _closed.sprite = sprite;
                else if (sprite.name == "CardPackOpen") _open.sprite = sprite;
                else if (sprite.name == "CardPackTornTop") _tornTop.sprite = sprite;
            }
            foreach (var image in new[] { _closed, _open, _tornTop })
            {
                Stretch(image.rectTransform);
                image.preserveAspect = true;
            }
            _closed.raycastTarget = true;
            _packButton = _pack.gameObject.AddComponent<UnityEngine.UI.Button>();
            _packButton.targetGraphic = _closed;
            _packButton.transition = UnityEngine.UI.Selectable.Transition.None;
            _packButton.onClick.AddListener(OpenPack);
            _packFallback = Text("MissingPackArtwork", _pack, "After333\n카드팩", 36, Gold);
            Center(_packFallback.rectTransform, new Vector2(240, 200), Vector2.zero);
            _packFallback.gameObject.SetActive(_closed.sprite == null);
            for (var i = 0; i < CardCount; i++) BuildFlyingCard(i);
            _results = Rect("PackResults", _safeArea);
            Stretch(_results);
            _grid = Rect("TenCards", _results);
            Stretch(_grid);
            for (var i = 0; i < CardCount; i++) BuildSlot(i);
        }

        private void BuildFlyingCard(int index)
        {
            var root = Rect("FlyingCard" + index, _pack);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var glow = Image("WhiteTrail", root, new Color(.7f, .88f, 1f, .8f));
            glow.sprite = _glowSprite;
            Center(glow.rectTransform, new Vector2(132, 206), Vector2.zero);
            var core = Image("WhiteCard", root, Color.white);
            Center(core.rectTransform, new Vector2(66, 94), Vector2.zero);
            var rim = core.gameObject.AddComponent<UnityEngine.UI.Outline>();
            rim.effectColor = new Color(.6f, .9f, 1f, .75f);
            rim.effectDistance = new Vector2(3, 3);
            _flying[index] = new FlyingCard { Root = root, Group = group };
        }

        private void BuildSlot(int index)
        {
            var slot = new CardSlot();
            slot.Root = Rect("CardSlot" + index, _grid);
            slot.Group = slot.Root.gameObject.AddComponent<CanvasGroup>();
            slot.Group.blocksRaycasts = false;
            slot.Card = Rect("Card", slot.Root);
            var background = Image("CardBackground", slot.Card, Color.white);
            Stretch(background.rectTransform);
            NoticeTheme.Panel(background, false);
            slot.Edges = new UnityEngine.UI.Image[4];
            for (var i = 0; i < slot.Edges.Length; i++)
            {
                slot.Edges[i] = Image("AuraEdge" + i, slot.Card, Color.clear);
                slot.Edges[i].sprite = _glowSprite;
            }
            slot.Artwork = Image("Artwork", slot.Card, Color.white);
            Stretch(slot.Artwork.rectTransform);
            slot.Artwork.preserveAspect = true;
            slot.Fallback = Text("MissingArtwork", slot.Card, "", 24, new Color(.94f, .96f, 1f));
            Stretch(slot.Fallback.rectTransform);
            slot.Fallback.rectTransform.offsetMin = new Vector2(12, 12);
            slot.Fallback.rectTransform.offsetMax = new Vector2(-12, -12);
            slot.Sparks = new UnityEngine.UI.Image[10];
            for (var i = 0; i < slot.Sparks.Length; i++)
            {
                slot.Sparks[i] = Image("AuraSpark" + i, slot.Card, Color.clear);
                slot.Sparks[i].sprite = _glowSprite;
                Center(slot.Sparks[i].rectTransform, new Vector2(25, 25), Vector2.zero);
                var star = Image("Core", slot.Sparks[i].transform, Color.white);
                Center(star.rectTransform, new Vector2(4, 4), Vector2.zero);
                star.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            }
            slot.Name = Text("CardName", slot.Root, "", 24, Color.white);
            slot.Name.verticalOverflow = VerticalWrapMode.Truncate;
            slot.Name.resizeTextForBestFit = true;
            slot.Name.resizeTextMinSize = 17;
            slot.Name.resizeTextMaxSize = 26;
            slot.Rarity = Text("CardRarity", slot.Root, "", 20, new Color(.74f, .81f, .91f));
            _slots[index] = slot;
        }

        private void ShowResults()
        {
            Phase = ShopCardPackRevealPhase.Results;
            _elapsed = 0;
            _pack.gameObject.SetActive(false);
            _results.gameObject.SetActive(true);
            _title.text = "획득 카드 10장";
            _hint.text = "화면을 눌러 상점으로 돌아가기";
            _hint.gameObject.SetActive(false);
            _dismissButton.interactable = false;
            for (var i = 0; i < CardCount; i++)
            {
                var item = _items[i];
                var slot = _slots[i];
                slot.Name.text = item.CardName;
                slot.Rarity.text = RarityLabel(item.Rarity);
                slot.Artwork.sprite = item.Artwork;
                slot.Artwork.enabled = item.Artwork != null;
                slot.Fallback.text = item.CardName + "\n" + item.CardId;
                slot.Fallback.gameObject.SetActive(item.Artwork == null);
                slot.Aura = RarityColor(item.Rarity);
                slot.SparkCount = item.Rarity == CardRarity.Legendary ? 10 : item.Rarity == CardRarity.Unique ? 6 : item.Rarity == CardRarity.Rare ? 4 : 0;
                slot.Rarity.color = slot.SparkCount == 0 ? new Color(.74f, .81f, .91f) : slot.Aura;
                foreach (var edge in slot.Edges) edge.gameObject.SetActive(slot.SparkCount > 0);
                for (var j = 0; j < slot.Sparks.Length; j++) slot.Sparks[j].gameObject.SetActive(j < slot.SparkCount);
            }
            _lastSize = Vector2.zero;
            RefreshLayout();
            AnimateResults();
        }

        private void SetPackPose(float progress)
        {
            var tear = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .65f, progress));
            _closed.color = new Color(1, 1, 1, 1 - tear);
            _open.color = new Color(1, 1, 1, tear);
            _tornTop.color = new Color(1, 1, 1, tear * (1 - Mathf.InverseLerp(.66f, 1f, progress)));
            _pack.localScale = Vector3.one;
            _pack.localEulerAngles = new Vector3(0, 0, Mathf.Sin(progress * 40) * (1 - progress) * 1.5f);
            SetImageOffset(_closed.rectTransform, new Vector2(-53.5f / 724 * _packSize, 0));
            SetImageOffset(_open.rectTransform, Vector2.zero);
            SetImageOffset(_tornTop.rectTransform, new Vector2((51.5f / 724 + .17f * tear) * _packSize, .17f * tear * _packSize));
            _tornTop.rectTransform.localEulerAngles = new Vector3(0, 0, -20 * tear);
            _packGlow.color = new Color(.8f, .93f, 1f, .16f + .44f * tear);
            _packGlow.rectTransform.localScale = Vector3.one * (1 + tear * .25f);
        }

        private void AnimateEjection(float elapsed)
        {
            _pack.localEulerAngles = Vector3.zero;
            _packGlow.color = new Color(.8f, .93f, 1f, .42f + .11f * Mathf.Sin(elapsed * 24));
            var scale = _packSize / 620;
            for (var i = 0; i < CardCount; i++)
            {
                var t = (elapsed - i * .067f) / .52f;
                var card = _flying[i];
                card.Group.alpha = t < 0 || t > 1 ? 0 : Mathf.Min(1, t * 6) * (1 - Mathf.InverseLerp(.76f, 1f, t));
                t = Mathf.Clamp01(t);
                var side = ((i % 5) - 2) * 34;
                card.Root.anchoredPosition = new Vector2(side * t, 208 + 245 * t + 68 * t * t) * scale;
                card.Root.localEulerAngles = new Vector3(0, 0, ((i % 3) - 1) * (8 + 23 * t));
                card.Root.localScale = Vector3.one * scale * (.82f + .28f * t);
            }
        }

        private void AnimateResults()
        {
            for (var i = 0; i < CardCount; i++)
            {
                var slot = _slots[i];
                var entrance = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_elapsed - i * .012f) / ResultEntranceDuration));
                slot.Group.alpha = entrance;
                slot.Root.anchoredPosition = slot.Position + new Vector2(0, -24 * (1 - entrance));
                slot.Root.localScale = Vector3.one * (.91f + .09f * entrance);
                if (slot.SparkCount == 0) continue;
                var pulse = .62f + .2f * Mathf.Sin(_ambient * 3 + i);
                foreach (var edge in slot.Edges) edge.color = new Color(slot.Aura.r, slot.Aura.g, slot.Aura.b, pulse);
                for (var j = 0; j < slot.SparkCount; j++)
                {
                    var along = Mathf.Repeat(_ambient * .09f + j / (float)slot.SparkCount, 1);
                    var spark = slot.Sparks[j];
                    spark.rectTransform.anchoredPosition = BorderPosition(along, slot.ArtworkSize);
                    spark.color = new Color(slot.Aura.r, slot.Aura.g, slot.Aura.b, .6f + .3f * Mathf.Sin(_ambient * 5 + j));
                    spark.rectTransform.localScale = Vector3.one * (slot.SparkCount == 10 ? 1.3f : 1);
                }
            }
        }

        private void RefreshLayout()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size == _lastSize && _lastSafeArea == Screen.safeArea) return;
            _lastSize = size;
            _lastSafeArea = Screen.safeArea;
            if (Screen.width > 0 && Screen.height > 0 && Screen.safeArea.width > 0)
            {
                _safeArea.anchorMin = new Vector2(Screen.safeArea.xMin / Screen.width, Screen.safeArea.yMin / Screen.height);
                _safeArea.anchorMax = new Vector2(Screen.safeArea.xMax / Screen.width, Screen.safeArea.yMax / Screen.height);
                _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;
            }
            var width = Mathf.Max(320, size.x * (_safeArea.anchorMax.x - _safeArea.anchorMin.x));
            var height = Mathf.Max(320, size.y * (_safeArea.anchorMax.y - _safeArea.anchorMin.y));
            var uiScale = Mathf.Min(1, width / 1280, height / 720);
            _title.fontSize = Mathf.RoundToInt(40 * uiScale);
            _hint.fontSize = Mathf.RoundToInt(26 * uiScale);
            Center(_title.rectTransform, new Vector2(width - 40, 58 * uiScale), new Vector2(0, height * .5f - 45 * uiScale));
            Center(_hint.rectTransform, new Vector2(width - 40, 44 * uiScale), new Vector2(0, -height * .5f + 32 * uiScale));
            _packSize = Mathf.Min(620, width - 80, height - 160 * uiScale);
            Center(_pack, Vector2.one * _packSize, new Vector2(0, -8 * uiScale));
            _grid.offsetMin = new Vector2(30 * uiScale, 68 * uiScale);
            _grid.offsetMax = new Vector2(-30 * uiScale, -92 * uiScale);
            var gridWidth = width - 60 * uiScale;
            var gridHeight = height - 160 * uiScale;
            var gap = 18 * uiScale;
            var cell = new Vector2((gridWidth - gap * 4) / 5, (gridHeight - gap) / 2);
            for (var i = 0; i < CardCount; i++) LayoutSlot(_slots[i], i, cell, gap, gridWidth, gridHeight, uiScale);
            if (Phase == ShopCardPackRevealPhase.Sealed) SetPackPose(0);
        }

        private void LayoutSlot(CardSlot slot, int index, Vector2 cell, float gap, float gridWidth, float gridHeight, float scale)
        {
            slot.Position = new Vector2(-gridWidth * .5f + cell.x * .5f + (index % 5) * (cell.x + gap),
                gridHeight * .5f - cell.y * .5f - (index / 5) * (cell.y + gap));
            Center(slot.Root, cell, slot.Position);
            var max = new Vector2(cell.x - 28 * scale, cell.y - 68 * scale);
            var sprite = slot.Artwork.sprite;
            var aspect = sprite != null && sprite.rect.height > 0 ? sprite.rect.width / sprite.rect.height : .7f;
            var artSize = new Vector2(Mathf.Min(max.x, max.y * aspect), Mathf.Min(max.y, max.x / aspect));
            slot.ArtworkSize = artSize;
            Center(slot.Card, artSize, new Vector2(0, 34 * scale));
            Center(slot.Name.rectTransform, new Vector2(cell.x - 4 * scale, 36 * scale), new Vector2(0, -cell.y * .5f + 44 * scale));
            slot.Name.resizeTextMinSize = Mathf.RoundToInt(17 * scale);
            slot.Name.resizeTextMaxSize = Mathf.RoundToInt(26 * scale);
            Center(slot.Rarity.rectTransform, new Vector2(cell.x, 24 * scale), new Vector2(0, -cell.y * .5f + 14 * scale));
            slot.Rarity.fontSize = Mathf.RoundToInt(20 * scale);
            Center(slot.Edges[0].rectTransform, new Vector2(artSize.x + 20 * scale, 28 * scale), new Vector2(0, artSize.y * .5f));
            Center(slot.Edges[1].rectTransform, new Vector2(artSize.x + 20 * scale, 28 * scale), new Vector2(0, -artSize.y * .5f));
            Center(slot.Edges[2].rectTransform, new Vector2(28 * scale, artSize.y + 20 * scale), new Vector2(-artSize.x * .5f, 0));
            Center(slot.Edges[3].rectTransform, new Vector2(28 * scale, artSize.y + 20 * scale), new Vector2(artSize.x * .5f, 0));
        }

        private static Vector2 BorderPosition(float t, Vector2 size)
        {
            var w = size.x * .5f;
            var h = size.y * .5f;
            var p = t * 4;
            if (p < 1) return new Vector2(Mathf.Lerp(-w, w, p), h);
            if (p < 2) return new Vector2(w, Mathf.Lerp(h, -h, p - 1));
            if (p < 3) return new Vector2(Mathf.Lerp(w, -w, p - 2), -h);
            return new Vector2(-w, Mathf.Lerp(-h, h, p - 3));
        }

        private static Color RarityColor(CardRarity rarity)
        {
            if (rarity == CardRarity.Legendary) return new Color(1f, .78f, .06f);
            if (rarity == CardRarity.Unique) return new Color(1f, .16f, .17f);
            if (rarity == CardRarity.Rare) return new Color(.18f, .57f, 1f);
            return new Color(.74f, .81f, .91f);
        }

        private static string RarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return "커먼";
                case CardRarity.Uncommon: return "언커먼";
                case CardRarity.Rare: return "레어";
                case CardRarity.Unique: return "유니크";
                case CardRarity.Legendary: return "레전더리";
                default: return "";
            }
        }

        private Sprite CreateGlowSprite()
        {
            const int side = 64;
            _glowTexture = new Texture2D(side, side, TextureFormat.RGBA32, false)
                { name = "ShopCardPackGlow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[side * side];
            for (var y = 0; y < side; y++) for (var x = 0; x < side; x++)
            {
                var d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * 32) / 32;
                pixels[y * side + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), 2));
            }
            _glowTexture.SetPixels(pixels);
            _glowTexture.Apply(false, true);
            return Sprite.Create(_glowTexture, new Rect(0, 0, side, side), new Vector2(.5f, .5f));
        }

        private static void SetImageOffset(RectTransform rect, Vector2 offset) => rect.offsetMin = rect.offsetMax = offset;
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        private static UnityEngine.UI.Text Text(string name, Transform parent, string value, int size, Color color)
        {
            var text = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Text>();
            NoticeTheme.Text(text, size);
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.color = color;
            text.supportRichText = false;
            return text;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
