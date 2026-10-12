using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Cards;
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
        public const int SortingOrder = 31000;
        private static DeckCardPreviewOverlay _instance;
        private Component _owner;
        private RectTransform _root;
        private Image _artwork;
        private Text _fallback;
        private Text _attack;
        private Text _hp;

        public static void ShowFor(Component owner, string cardId, CardDefinitionAsset asset, Font font, CardStatDisplay? statsOverride = null, CardDefinition definition = null, HandCardView handCard = null, System.Func<BattleState> battleProvider = null, int? upgradeLevelOverride = null)
        {
            if (owner == null || !owner.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(cardId))
                return;

            if (_instance == null)
                _instance = Create(owner.gameObject.scene);

            _instance._owner = owner;
            _instance._upgradeLevelOverride = upgradeLevelOverride;
            _instance.gameObject.SetActive(true);
            _instance._definition = definition ?? asset?.ToDefinition();
            _instance._cardId = cardId;
            _instance._handCard = handCard;
            _instance._battleProvider = battleProvider;
            _instance._handRuntimeId = handCard != null ? handCard.RuntimeId : null;
            _instance.SetCard(cardId, asset, font, statsOverride);
            _instance.BindTooltips();
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
            canvas.sortingOrder = SortingOrder;
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

        private void SetCard(string cardId, CardDefinitionAsset asset, Font font, CardStatDisplay? statsOverride)
        {
            if (font == null || !font.HasCharacter('가'))
                font = NoticeTheme.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _font = font;
            _fallback.font = _attack.font = _hp.font = font;
            var hasArtwork = CardArtworkLibrary.TryGetArtwork(cardId, out var sprite);
            _artwork.sprite = hasArtwork ? sprite : null;
            _artwork.color = hasArtwork ? Color.white : new Color(0.035f, 0.055f, 0.085f, 0.98f);
            _fallback.text = _definition != null ? _definition.DisplayName : cardId;
            _fallback.gameObject.SetActive(!hasArtwork);

            var stats = default(CardStatDisplay);
            var hasStats = _definition != null && CardStatDisplay.TryCreate(
                _definition, CurrentUpgradeLevel, out stats, _handCard != null ? _handCard.SpellPower : 0);
            if (statsOverride.HasValue) { stats = statsOverride.Value; hasStats = true; }
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
            if (!_layoutDirty && size == _layoutSize) return;
            _layoutDirty = false;
            _layoutSize = size;
            if (size.x <= 1 || size.y <= 1) size = new Vector2(1920, 1080);
            var layout = CardPreviewTooltipLayout.Calculate(size.x, size.y, 384, 528,
                VisibleCount(_abilities), VisibleCount(_attached), VisibleCount(_influences), MeasureTooltip);
            SetPreviewRect(_artwork.rectTransform, layout.Card);
            LayoutPanels(_abilities, layout.Abilities, layout.TextScale);
            LayoutPanels(_attached, layout.Attached, layout.TextScale);
            LayoutPanels(_influences, layout.Influences, layout.TextScale);
            var rendered = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(_artwork);
            HandCardStatOverlayLayout.PositionStatText(_attack.rectTransform, _artwork.rectTransform,
                rendered, HandCardStatOverlayLayout.DefaultAttackPosition);
            HandCardStatOverlayLayout.PositionStatText(_hp.rectTransform, _artwork.rectTransform,
                rendered, HandCardStatOverlayLayout.DefaultHpPosition);
            _attack.fontSize = _hp.fontSize =
                HandCardStatOverlayLayout.CalculateScaledFontSize(36, rendered.height, 528f);
        }

        private sealed class TooltipPanel
        {
            public RectTransform Root;
            public UnityEngine.UI.Text Title;
            public UnityEngine.UI.Text Description;
        }

        private readonly System.Collections.Generic.List<TooltipPanel> _abilities = new();
        private readonly System.Collections.Generic.List<TooltipPanel> _attached = new();
        private readonly System.Collections.Generic.List<TooltipPanel> _influences = new();
        private System.Func<BattleState> _battleProvider;
        private BattleState _shownBattle;
        private float _nextBattleRefresh;
        private CardDefinition _definition;
        private HandCardView _handCard;
        private string _handRuntimeId;
        private string _cardId;
        private Font _font;
        private int _shownSpellPower = -1;
        private int _shownUpgradeLevel = -1;
        private int? _upgradeLevelOverride;
        private int CurrentUpgradeLevel => _upgradeLevelOverride ?? AccountSessionState.GetOwnedCardUpgradeLevel(_cardId);
        private bool _shownTemporary;
        private Vector2 _layoutSize;
        private bool _layoutDirty;

        private string _shownInfluences = string.Empty;

        private void BindTooltips(bool force = true)
        {

            var attached = new System.Collections.Generic.List<OccupantSpecialEffectTooltip>();
            if (_handCard != null && _handCard.IsTemporaryReplicate)
                attached.Add(new OccupantSpecialEffectTooltip("임시 복제", string.Empty, OccupantTooltipKind.State));

            var influences = new System.Collections.Generic.List<OccupantSpecialEffectTooltip>();
            var battle = _handCard != null ? _battleProvider?.Invoke() : null;
            if (battle != null) influences.AddRange(OccupantStatusTooltipCatalog.BuildHandInfluences(battle, PlayerId.Player));
            else if (_handCard != null && _handCard.SpellPower > 0)
                influences.Add(new OccupantSpecialEffectTooltip($"주문력 +{_handCard.SpellPower}", string.Empty, OccupantTooltipKind.Influence));
            var influenceKey = string.Empty;
            foreach (var influence in influences) influenceKey += influence.Title + "\n";
            var upgradeLevel = CurrentUpgradeLevel;
            var unchanged = _shownUpgradeLevel == upgradeLevel && _shownSpellPower == (_handCard != null ? _handCard.SpellPower : 0) &&
                _shownTemporary == (_handCard != null && _handCard.IsTemporaryReplicate) && _shownInfluences == influenceKey;
            _shownInfluences = influenceKey;
            _shownBattle = battle;
            _nextBattleRefresh = Time.unscaledTime + 0.15f;
            _shownSpellPower = _handCard != null ? _handCard.SpellPower : 0;
            _shownUpgradeLevel = upgradeLevel;
            _shownTemporary = _handCard != null && _handCard.IsTemporaryReplicate;
            if (!force && unchanged) return;
            var abilities = new System.Collections.Generic.List<OccupantSpecialEffectTooltip>(OccupantSpecialEffectTooltipCatalog.Build(_definition));
            if (_definition is ScriptedSpellCardDefinition earthquake && earthquake.CardId == EarthquakeRules.CardId && earthquake.EffectId == EarthquakeRules.EffectId)
                abilities.Add(new OccupantSpecialEffectTooltip(earthquake.DisplayName,
                    CardStatDisplay.GetEffectText(earthquake, EarthquakeRules.EffectDescription, upgradeLevel)));
            if (_definition is ScriptedSpellCardDefinition meteor && meteor.CardId == MeteorRules.CardId && meteor.EffectId == MeteorRules.EffectId)
                abilities.Add(new OccupantSpecialEffectTooltip(meteor.DisplayName,
                    CardStatDisplay.GetEffectText(meteor, MeteorRules.EffectDescription, upgradeLevel)));
            BindPanels(_abilities, "Ability", abilities);
            BindPanels(_attached, "Attached", attached);
            BindPanels(_influences, "Influence", influences);
            _layoutDirty = true;
        }

        private void BindPanels(System.Collections.Generic.List<TooltipPanel> panels, string prefix,
            System.Collections.Generic.IReadOnlyList<OccupantSpecialEffectTooltip> entries)
        {
            while (panels.Count < entries.Count)
            {
                var node = new GameObject(prefix + "Tooltip" + panels.Count, typeof(RectTransform),
                    typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
                node.transform.SetParent(_root, false);
                var image = node.GetComponent<UnityEngine.UI.Image>();
                image.color = new Color(0.055f, 0.07f, 0.085f, 0.96f);
                image.raycastTarget = false;
                node.GetComponent<UnityEngine.UI.Outline>().effectDistance = new Vector2(2, -2);
                panels.Add(new TooltipPanel { Root = node.GetComponent<RectTransform>(),
                    Title = CreateText(node.transform, "Title", 24), Description = CreateText(node.transform, "Description", 17) });
            }
            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                panel.Root.gameObject.SetActive(i < entries.Count);
                if (i >= entries.Count) continue;
                var entry = entries[i];
                var color = entry.Kind == OccupantTooltipKind.State ? new Color(0.60f, 0.82f, 1f) :
                    entry.Kind == OccupantTooltipKind.Buff ? new Color(0.58f, 1f, 0.66f) :
                    entry.Kind == OccupantTooltipKind.Debuff ? new Color(1f, 0.62f, 0.59f) : new Color(1f, 0.88f, 0.5f);
                panel.Root.GetComponent<UnityEngine.UI.Outline>().effectColor = color;
                panel.Title.font = panel.Description.font = _font;
                panel.Title.alignment = panel.Description.alignment = TextAnchor.UpperLeft;
                panel.Title.text = entry.Title;
                panel.Title.color = color;
                panel.Description.fontStyle = FontStyle.Normal;
                panel.Description.text = entry.Description;
                panel.Description.gameObject.SetActive(!string.IsNullOrEmpty(entry.Description));
            }
        }

        private static int VisibleCount(System.Collections.Generic.List<TooltipPanel> panels)
        {
            var count = 0;
            foreach (var panel in panels) if (panel.Root.gameObject.activeSelf) count++;
            return count;
        }

        private float MeasureTooltip(CardPreviewTooltipZone zone, int index, float width, float scale)
        {
            var panel = zone == CardPreviewTooltipZone.Ability ? _abilities[index] :
                zone == CardPreviewTooltipZone.Attached ? _attached[index] : _influences[index];
            panel.Title.fontSize = Mathf.Max(1, Mathf.RoundToInt(24 * scale));
            panel.Description.fontSize = Mathf.Max(1, Mathf.RoundToInt(17 * scale));
            var textWidth = Mathf.Max(1, width - 24 * scale);
            float Height(UnityEngine.UI.Text text)
            {
                var settings = text.GetGenerationSettings(new Vector2(textWidth, 0));
                return text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
            }
            var titleHeight = Height(panel.Title);
            return panel.Description.gameObject.activeSelf
                ? Mathf.Max(116 * scale, titleHeight + Height(panel.Description) + 24 * scale)
                : titleHeight + 16 * scale;
        }

        private static void SetPreviewRect(RectTransform rect, CardPreviewTooltipRect box)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(box.X, box.Y);
            rect.sizeDelta = new Vector2(box.Width, box.Height);
        }

        private void LayoutPanels(System.Collections.Generic.List<TooltipPanel> panels,
            System.Collections.Generic.IReadOnlyList<CardPreviewTooltipRect> boxes, float scale)
        {
            for (var i = 0; i < boxes.Count; i++)
            {
                var panel = panels[i];
                SetPreviewRect(panel.Root, boxes[i]);
                panel.Title.fontSize = Mathf.Max(1, Mathf.RoundToInt(24 * scale));
                panel.Description.fontSize = Mathf.Max(1, Mathf.RoundToInt(17 * scale));
                var width = Mathf.Max(1, boxes[i].Width - 24 * scale);
                var settings = panel.Title.GetGenerationSettings(new Vector2(width, 0));
                var titleHeight = panel.Title.cachedTextGeneratorForLayout.GetPreferredHeight(panel.Title.text, settings) / panel.Title.pixelsPerUnit;
                var titleRect = panel.Title.rectTransform;
                titleRect.anchorMin = new Vector2(0, 1);
                titleRect.anchorMax = Vector2.one;
                titleRect.pivot = new Vector2(0.5f, 1);
                titleRect.offsetMin = new Vector2(12 * scale, -8 * scale - titleHeight);
                titleRect.offsetMax = new Vector2(-12 * scale, -8 * scale);
                var body = panel.Description.rectTransform;
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.offsetMin = new Vector2(12 * scale, 8 * scale);
                body.offsetMax = new Vector2(-12 * scale, -14 * scale - titleHeight);
            }
        }


        private void LateUpdate()
        {
            if (_owner == null || !_owner.gameObject.activeInHierarchy)
            {
                _owner = null;
                gameObject.SetActive(false);
                return;
            }

            if ((_definition?.CardId == NuclearRules.CardId || _definition?.CardId == EarthquakeRules.CardId || _definition?.CardId == MeteorRules.CardId) && _shownUpgradeLevel != CurrentUpgradeLevel)
            {
                SetCard(_cardId, null, _font, null);
                BindTooltips();
            }
            if (_handCard != null)
            {
                if (!_handCard.HasCard || _handCard.CardId != _cardId || _handCard.RuntimeId != _handRuntimeId)
                {
                    HideFor(_owner);
                    return;
                }
                if (_shownSpellPower != _handCard.SpellPower || _shownTemporary != _handCard.IsTemporaryReplicate ||
                    _shownBattle != _battleProvider?.Invoke() || (_shownBattle != null && Time.unscaledTime >= _nextBattleRefresh))
                {
                    SetCard(_cardId, null, _font, null);
                    BindTooltips(false);
                }
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
