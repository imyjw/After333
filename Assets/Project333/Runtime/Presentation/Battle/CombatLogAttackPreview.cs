using System.Collections.Generic;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Hand;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class CombatLogAttackPreview : MonoBehaviour
    {
        public const float MarkerSeconds = 3f;
        public const int MaxTargetColumns = 5;
        private const float CardWidth = 384f, CardHeight = 528f, Gap = 128f;
        public static readonly Color IncreasedColor = new Color32(86, 240, 111, 255);
        public static readonly Color DecreasedColor = new Color32(255, 91, 91, 255);
        private sealed class CardPanel
        {
            internal RectTransform Root;
            internal Image Artwork;
            internal TMP_Text Attack, Hp, Name, Role, Damage;
            internal CombatLogSymbolGraphic Marker;
        }
        private RectTransform _root, _pair, _targetGrid;
        private CardPanel _left, _right;
        private readonly List<CardPanel> _gridPanels = new List<CardPanel>();
        private CombatLogSymbolGraphic _swords;
        private TMP_FontAsset _font;
        private float _shownAt;
        private float _markersExpireAt;
        private Vector2 _lastSize;
        private Rect _lastSafe;
        public bool IsVisible => gameObject.activeSelf;
        public static Color StatColor(int current, int original) => current > original ? IncreasedColor :
            current < original ? DecreasedColor : Color.white;

        public static float GetTargetCardScaleForCount(int targetCount)
        {
            var tier = Mathf.Max(1, Mathf.CeilToInt(targetCount / (float)MaxTargetColumns));
            if (tier == 1) return 0.52f;
            if (tier == 2) return 0.44f;
            if (tier == 3) return 0.36f;
            return Mathf.Max(0.24f, 0.30f - (tier - 4) * 0.04f);
        }

        public static CombatLogAttackPreview Create(Scene scene, TMP_FontAsset font)
        {
            var go = new GameObject("CombatLogAttackPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(go, scene);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31200;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;
            var group = go.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            var preview = go.AddComponent<CombatLogAttackPreview>(); preview._root = (RectTransform)go.transform; preview._font = font;
            var shade = Node(go.transform, "Backdrop").gameObject.AddComponent<Image>();
            shade.color = new Color(.015f, .025f, .035f, .76f); shade.raycastTarget = false;
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            preview._pair = Node(go.transform, "CombatPair");
            preview._pair.sizeDelta = new Vector2(CardWidth * 2 + Gap, CardHeight + 72);
            preview._left = preview.MakePanel(preview._pair, "Attacker", -CardWidth / 2 - Gap / 2, font);
            preview._right = preview.MakePanel(preview._pair, "Defender", CardWidth / 2 + Gap / 2, font);
            preview._targetGrid = Node(preview._pair, "TargetGrid");
            preview._targetGrid.gameObject.SetActive(false);
            var swords = Node(preview._pair, "CrossedSwords").gameObject.AddComponent<CombatLogSymbolGraphic>();
            swords.Kind = CombatLogSymbolGraphic.Symbol.Swords; swords.rectTransform.sizeDelta = new Vector2(140, 140);
            preview._swords = swords;
            go.SetActive(false); return preview;
        }
        public void Show(BattleCombatLogCardSnapshotDto attacker, BattleCombatLogCardSnapshotDto defender, PlayerId attackerOwner, PlayerId defenderOwner)
        {
            if (attacker == null || defender == null) return;
            gameObject.SetActive(true); _shownAt = Time.unscaledTime;
            _markersExpireAt = _shownAt + MarkerSeconds;
            HideTargetGrid();
            _pair.sizeDelta = new Vector2(CardWidth * 2 + Gap, CardHeight + 72);
            _left.Root.localScale = Vector3.one;
            _left.Root.anchoredPosition = new Vector2(-CardWidth / 2 - Gap / 2, 20);
            _right.Root.localScale = Vector3.one;
            _right.Root.anchoredPosition = new Vector2(CardWidth / 2 + Gap / 2, 20);
            _right.Root.gameObject.SetActive(true);
            if (_swords != null)
            {
                _swords.Kind = CombatLogSymbolGraphic.Symbol.Swords;
                _swords.rectTransform.anchoredPosition = Vector2.zero;
                _swords.SetVerticesDirty(); _swords.SetMaterialDirty();
                _swords.gameObject.SetActive(true);
            }
            Bind(_left, attacker, "공격자 · " + Owner(attackerOwner));
            Bind(_right, defender, "피격자 · " + Owner(defenderOwner));
            Canvas.ForceUpdateCanvases(); RefreshLayout();
        }
        public void ShowSingle(BattleCombatLogCardSnapshotDto card, PlayerId owner)
        {
            if (card == null) return;
            gameObject.SetActive(true); _shownAt = Time.unscaledTime;
            _markersExpireAt = _shownAt;
            HideTargetGrid();
            _pair.sizeDelta = new Vector2(CardWidth * 2 + Gap, CardHeight + 72);
            _left.Root.localScale = Vector3.one;
            _left.Root.anchoredPosition = new Vector2(0, 20);
            _right.Root.gameObject.SetActive(false);
            if (_swords != null) _swords.gameObject.SetActive(false);
            Bind(_left, card.WithOutcome(0, false), Owner(owner));
            Canvas.ForceUpdateCanvases(); RefreshLayout();
        }
        public void ShowDirected(BattleCombatLogCardSnapshotDto source, PlayerId sourceOwner, IReadOnlyList<BattleCombatLogCardSnapshotDto> targets)
        {
            if (source == null || targets == null || targets.Count == 0) return;
            gameObject.SetActive(true); _shownAt = Time.unscaledTime;
            _markersExpireAt = _shownAt + MarkerSeconds;
            if (_swords != null)
            {
                _swords.Kind = CombatLogSymbolGraphic.Symbol.RightTriangle;
                _swords.SetVerticesDirty(); _swords.SetMaterialDirty();
                _swords.gameObject.SetActive(true);
            }
            Bind(_left, source.WithOutcome(0, false), Owner(sourceOwner));
            _left.Root.localScale = Vector3.one;
            if (targets.Count == 1)
            {
                HideTargetGrid();
                _pair.sizeDelta = new Vector2(CardWidth * 2 + Gap, CardHeight + 72);
                _left.Root.anchoredPosition = new Vector2(-CardWidth / 2 - Gap / 2, 20);
                if (_swords != null) _swords.rectTransform.anchoredPosition = Vector2.zero;
                _right.Root.localScale = Vector3.one;
                _right.Root.anchoredPosition = new Vector2(CardWidth / 2 + Gap / 2, 20);
                _right.Root.gameObject.SetActive(true);
                var singleTarget = targets[0];
                Bind(_right, singleTarget, Owner(singleTarget.OwnerId));
            }
            else
            {
                _right.Root.gameObject.SetActive(false);
                _targetGrid.gameObject.SetActive(true);
                var count = targets.Count;
                var rowCount = Mathf.CeilToInt(count / (float)MaxTargetColumns);
                var colCount = Mathf.Min(count, MaxTargetColumns);
                var targetScale = GetTargetCardScaleForCount(count);
                var cellW = CardWidth * targetScale;
                var cellH = (CardHeight + 56f) * targetScale;
                var hGap = 20f * targetScale;
                var vGap = 22f * targetScale;
                var gridW = colCount * cellW + (colCount - 1) * hGap;
                var gridH = rowCount * cellH + (rowCount - 1) * vGap;
                var totalW = CardWidth + Gap + gridW;
                var totalH = Mathf.Max(CardHeight + 72f, gridH + 40f);
                _pair.sizeDelta = new Vector2(totalW, totalH);
                _left.Root.anchoredPosition = new Vector2(-totalW * .5f + CardWidth * .5f, 20f);
                if (_swords != null) _swords.rectTransform.anchoredPosition = new Vector2(-totalW * .5f + CardWidth + Gap * .5f, 20f);
                _targetGrid.sizeDelta = new Vector2(gridW, gridH);
                _targetGrid.anchoredPosition = new Vector2(-totalW * .5f + CardWidth + Gap + gridW * .5f, 20f);
                var startX = -gridW * .5f + cellW * .5f;
                var startY = gridH * .5f - cellH * .5f;
                while (_gridPanels.Count < count)
                {
                    _gridPanels.Add(MakePanel(_targetGrid, "Target_" + _gridPanels.Count, 0f, _font));
                }
                for (var i = 0; i < _gridPanels.Count; i++)
                {
                    var panel = _gridPanels[i];
                    if (i >= count)
                    {
                        panel.Root.gameObject.SetActive(false);
                        continue;
                    }
                    var row = i / MaxTargetColumns;
                    var col = i % MaxTargetColumns;
                    panel.Root.localScale = Vector3.one * targetScale;
                    panel.Root.anchoredPosition = new Vector2(startX + col * (cellW + hGap), startY - row * (cellH + vGap));
                    panel.Root.gameObject.SetActive(true);
                    Bind(panel, targets[i], Owner(targets[i].OwnerId));
                }
            }
            Canvas.ForceUpdateCanvases(); RefreshLayout();
        }
        public void Show(BattleCombatLogEntryDto entry)
        {
            if (entry == null) return;
            if (entry.EntryType == BattleCombatLogEntryType.Attack)
            {
                if (entry.SourcePreview == null || entry.TargetPreview == null) return;
                Show(entry.SourcePreview.Clone(), entry.TargetPreview.Clone(), entry.SourceOwnerId, entry.TargetOwnerId);
                return;
            }
            if (entry.TryGetDirectedPreview(out var source, out var targets))
            {
                var clonedTargets = new List<BattleCombatLogCardSnapshotDto>(targets.Count);
                for (var i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null) clonedTargets.Add(targets[i].Clone());
                }
                ShowDirected(source.Clone(), entry.SourceOwnerId, clonedTargets);
                return;
            }
            if (entry.TryGetSingleCardPreview(out var singleCard, out var ownerId)) ShowSingle(singleCard, ownerId);
        }
        public void Hide() => gameObject.SetActive(false);
        private void HideTargetGrid()
        {
            if (_targetGrid == null) return;
            _targetGrid.gameObject.SetActive(false);
            for (var i = 0; i < _gridPanels.Count; i++) _gridPanels[i].Root.gameObject.SetActive(false);
        }
        private static string Owner(PlayerId owner) => owner == PlayerId.Player ? "플레이어" : "상대방";
        private static RectTransform Node(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); return rect;
        }
        private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size)
        {
            var text = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size; text.fontStyle = FontStyles.Bold; text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.outlineColor = Color.black; text.outlineWidth = .18f;
            text.rectTransform.sizeDelta = new Vector2(130, 70); return text;
        }
        private CardPanel MakePanel(Transform parent, string name, float x, TMP_FontAsset font)
        {
            var p = new CardPanel(); p.Root = Node(parent, name); p.Root.sizeDelta = new Vector2(CardWidth, CardHeight);
            p.Root.anchoredPosition = new Vector2(x, 20);
            p.Artwork = Node(p.Root, "CardArtwork").gameObject.AddComponent<Image>(); p.Artwork.raycastTarget = false;
            p.Artwork.preserveAspect = true; p.Artwork.rectTransform.sizeDelta = new Vector2(CardWidth, CardHeight);
            p.Name = Label(p.Root, "FallbackName", font, 32); p.Name.rectTransform.sizeDelta = new Vector2(CardWidth - 20, 160);
            var statFont = Resources.Load<TMP_FontAsset>(
                "Project333/Fonts/ThaleahFat_TTF SDF");

            if (statFont == null)
            {
                statFont = font;
            }

            p.Attack = Label(p.Root, "Attack", statFont, 452);
            p.Hp = Label(p.Root, "Hp", statFont, 52);
            p.Role = Label(p.Root, "Role", font, 26); p.Role.rectTransform.sizeDelta = new Vector2(CardWidth + 40, 42);
            p.Role.rectTransform.anchoredPosition = new Vector2(0, -CardHeight / 2 - 26);
            p.Marker = Node(p.Root, "ResultMarker").gameObject.AddComponent<CombatLogSymbolGraphic>();
            p.Marker.rectTransform.sizeDelta = new Vector2(166, 166); p.Marker.rectTransform.anchoredPosition = new Vector2(0, -48);
            p.Damage = Label(p.Marker.transform, "Damage", statFont, 80); p.Damage.rectTransform.sizeDelta = new Vector2(200, 110);
            return p;
        }
        private static void Bind(CardPanel p, BattleCombatLogCardSnapshotDto snapshot, string role)
        {
            var hasArt = CardArtworkLibrary.TryGetArtwork(snapshot.CardId, out var sprite);
            p.Artwork.sprite = hasArt ? sprite : null; p.Artwork.color = hasArt ? Color.white : new Color(.04f, .07f, .1f);
            p.Name.text = snapshot.CardId; p.Name.gameObject.SetActive(!hasArt);
            p.Attack.gameObject.SetActive(snapshot.HasCombatStats);
            p.Hp.gameObject.SetActive(snapshot.HasCombatStats);
            if (snapshot.HasCombatStats)
            {
                p.Attack.text = snapshot.Attack.ToString(); p.Attack.color = StatColor(snapshot.Attack, snapshot.OriginalAttack);
                p.Hp.text = snapshot.Hp.ToString(); p.Hp.color = StatColor(snapshot.Hp, snapshot.OriginalMaxHp);
            }
            p.Role.text = role;
            if (snapshot.Died)
            {
                p.Marker.Kind = CombatLogSymbolGraphic.Symbol.Skull;
                p.Marker.gameObject.SetActive(true);
                p.Damage.gameObject.SetActive(false);
            }
            else if (snapshot.DamageTaken > 0)
            {
                p.Marker.Kind = CombatLogSymbolGraphic.Symbol.Damage;
                p.Marker.gameObject.SetActive(true);
                p.Damage.text = "-" + Mathf.Max(0, snapshot.DamageTaken);
                p.Damage.gameObject.SetActive(true);
            }
            else if (snapshot.HealAmount > 0)
            {
                p.Marker.Kind = CombatLogSymbolGraphic.Symbol.Heal;
                p.Marker.gameObject.SetActive(true);
                p.Damage.text = "+" + Mathf.Max(0, snapshot.HealAmount);
                p.Damage.gameObject.SetActive(true);
            }
            else
            {
                p.Marker.Kind = CombatLogSymbolGraphic.Symbol.Damage;
                p.Marker.gameObject.SetActive(false);
                p.Damage.gameObject.SetActive(false);
            }
            p.Marker.SetVerticesDirty();
            p.Marker.SetMaterialDirty();
        }
        private void Update()
        {
            UpdateMarkerVisibility(Time.unscaledTime);
            if (_root.rect.size != _lastSize || Screen.safeArea != _lastSafe) RefreshLayout();
        }
        internal void UpdateMarkerVisibility(float now)
        {
            if (now < _markersExpireAt) return;
            _left.Marker.gameObject.SetActive(false); _right.Marker.gameObject.SetActive(false);
            for (var i = 0; i < _gridPanels.Count; i++) _gridPanels[i].Marker.gameObject.SetActive(false);
        }
        private void RefreshLayout()
        {
            _lastSize = _root.rect.size; _lastSafe = Screen.safeArea;
            var safeWidth = _lastSize.x * (_lastSafe.width / Mathf.Max(1, Screen.width));
            var safeHeight = _lastSize.y * (_lastSafe.height / Mathf.Max(1, Screen.height));
            var pairWidth = Mathf.Max(2 * CardWidth + Gap, _pair.sizeDelta.x);
            var pairHeight = Mathf.Max(CardHeight + 72, _pair.sizeDelta.y);
            var scale = Mathf.Min(1.2f, Mathf.Max(1, safeWidth - 64) / pairWidth, Mathf.Max(1, safeHeight - 100) / pairHeight);
            _pair.localScale = Vector3.one * scale;
            _pair.anchoredPosition = new Vector2((_lastSafe.center.x / Mathf.Max(1, Screen.width) - .5f) * _lastSize.x,
                (_lastSafe.center.y / Mathf.Max(1, Screen.height) - .5f) * _lastSize.y);
            LayoutStats(_left); LayoutStats(_right);
            for (var i = 0; i < _gridPanels.Count; i++)
            {
                if (_gridPanels[i].Root.gameObject.activeSelf) LayoutStats(_gridPanels[i]);
            }
        }
        private static void LayoutStats(CardPanel panel)
        {
            var rendered = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(panel.Artwork);
            HandCardStatOverlayLayout.PositionStatText(panel.Attack.rectTransform, panel.Artwork.rectTransform, rendered, HandCardStatOverlayLayout.DefaultAttackPosition);
            HandCardStatOverlayLayout.PositionStatText(panel.Hp.rectTransform, panel.Artwork.rectTransform, rendered, HandCardStatOverlayLayout.DefaultHpPosition);
            panel.Attack.fontSize = panel.Hp.fontSize = HandCardStatOverlayLayout.CalculateScaledFontSize(48, rendered.height, CardHeight);
        }
    }
}
