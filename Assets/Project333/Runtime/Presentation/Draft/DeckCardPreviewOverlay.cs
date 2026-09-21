using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Draft
{
    // Separate from the scroll viewport so masks cannot crop the held card.
    public sealed class DeckCardPreviewOverlay : MonoBehaviour
    {
        private static DeckCardPreviewOverlay _instance;
        private Component _owner;
        private RectTransform _root;
        private Image _artwork;
        private Text _fallback;
        private Text _attack;
        private Text _hp;

        public static void ShowFor(Component owner, string cardId, CardDefinitionAsset asset, Font font)
        {
            if (owner == null || !owner.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(cardId))
                return;

            if (_instance == null)
                _instance = Create(owner.gameObject.scene);

            _instance._owner = owner;
            _instance.gameObject.SetActive(true);
            _instance.SetCard(cardId, asset, font);
            Canvas.ForceUpdateCanvases();
            _instance.RefreshLayout();
        }

        public static void HideFor(Component owner)
        {
            if (_instance == null || _instance._owner != owner)
                return;

            _instance._owner = null;
            _instance.gameObject.SetActive(false);
        }

        public static bool IsVisibleFor(Component owner)
        {
            return owner != null && _instance != null &&
                   _instance._owner == owner && _instance.gameObject.activeInHierarchy;
        }

        private static DeckCardPreviewOverlay Create(Scene scene)
        {
            var root = new GameObject("DeckCardHoldPreview", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(CanvasGroup), typeof(DeckCardPreviewOverlay));
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.MoveGameObjectToScene(root, scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var preview = root.GetComponent<DeckCardPreviewOverlay>();
            preview._root = root.GetComponent<RectTransform>();
            var artworkObject = new GameObject("CardArtwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            artworkObject.transform.SetParent(root.transform, false);
            preview._artwork = artworkObject.GetComponent<Image>();
            preview._artwork.raycastTarget = false;
            preview._artwork.preserveAspect = true;
            var rect = preview._artwork.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            preview._fallback = CreateText(rect, "CardName", 28);
            preview._fallback.rectTransform.anchorMin = Vector2.zero;
            preview._fallback.rectTransform.anchorMax = Vector2.one;
            preview._fallback.rectTransform.offsetMin = new Vector2(20f, 50f);
            preview._fallback.rectTransform.offsetMax = new Vector2(-20f, -50f);
            preview._attack = CreateText(rect, "Attack", 36);
            preview._hp = CreateText(rect, "Hp", 36);
            return preview;
        }

        private static Text CreateText(Transform parent, string name, int fontSize)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            node.transform.SetParent(parent, false);
            var text = node.GetComponent<Text>();
            text.raycastTarget = false;
            text.supportRichText = false;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.sizeDelta = new Vector2(100f, 60f);
            var outline = node.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private void SetCard(string cardId, CardDefinitionAsset asset, Font font)
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _fallback.font = _attack.font = _hp.font = font;
            var hasArtwork = CardArtworkLibrary.TryGetArtwork(cardId, out var sprite);
            _artwork.sprite = hasArtwork ? sprite : null;
            _artwork.color = hasArtwork ? Color.white : new Color(0.035f, 0.055f, 0.085f, 0.98f);
            _fallback.text = asset != null ? asset.DisplayName : cardId;
            _fallback.gameObject.SetActive(!hasArtwork);

            var stats = default(CardStatDisplay);
            var hasStats = asset != null && CardStatDisplay.TryCreate(
                asset.ToDefinition(), AccountSessionState.GetOwnedCardUpgradeLevel(cardId), out stats);
            _attack.text = stats.Attack.ToString();
            _hp.text = stats.Hp.ToString();
            _attack.gameObject.SetActive(hasStats);
            _hp.gameObject.SetActive(hasStats && stats.HasHp);
        }

        private void RefreshLayout()
        {
            if (_root == null || _artwork == null)
                return;

            var size = _root.rect.size;
            var scale = Mathf.Min(1f, Mathf.Min(size.x * 0.8f / 384f, size.y * 0.8f / 528f));
            _artwork.rectTransform.sizeDelta = new Vector2(384f, 528f) * Mathf.Max(0.01f, scale);
            var rendered = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(_artwork);
            HandCardStatOverlayLayout.PositionStatText(_attack.rectTransform, _artwork.rectTransform,
                rendered, HandCardStatOverlayLayout.DefaultAttackPosition);
            HandCardStatOverlayLayout.PositionStatText(_hp.rectTransform, _artwork.rectTransform,
                rendered, HandCardStatOverlayLayout.DefaultHpPosition);
            _attack.fontSize = _hp.fontSize =
                HandCardStatOverlayLayout.CalculateScaledFontSize(36, rendered.height, 528f);
        }

        private void LateUpdate()
        {
            if (_owner == null || !_owner.gameObject.activeInHierarchy)
            {
                _owner = null;
                gameObject.SetActive(false);
                return;
            }

            RefreshLayout();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                HideFor(_owner);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                HideFor(_owner);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
