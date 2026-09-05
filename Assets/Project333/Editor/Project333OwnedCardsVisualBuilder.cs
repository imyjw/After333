using System.IO;
using System.Linq;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.OwnedCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333OwnedCardsVisualBuilder
    {
        private const string ScenePath = "Assets/OwnedCards_VSlice.unity";
        private const string SpritePath = "Assets/Project333/Resources/Project333/StartScene/PixelMenuButton.png";
        private const string FontPath = "Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static/NotoSansKR-Bold.ttf";
        private static readonly Color Ink = new Color(0.026f, 0.063f, 0.073f, 1f);
        private static readonly Color Panel = new Color(0.045f, 0.105f, 0.115f, 1f);
        private static readonly Color Gold = new Color(0.78f, 0.59f, 0.29f, 1f);
        private static readonly Color Cream = new Color(0.96f, 0.89f, 0.72f, 1f);
        private static readonly Color Muted = new Color(0.63f, 0.77f, 0.76f, 1f);

        [InitializeOnLoadMethod]
        private static void QueueLayout()
        {
            EditorApplication.delayCall += ApplyInitialLayout;
        }

        private static OwnedCardsSceneController FindController(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<OwnedCardsSceneController>(true)).SingleOrDefault();
        }

        private static void ApplyInitialLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath) || !File.Exists(SpritePath)) return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (FindController(scene)?.CollectionVisualVersion >= 1) return;
            }
            else if (File.ReadAllText(ScenePath).Contains("_collectionVisualVersion: 1")) return;
            ApplyPixelCollectionLayout();
        }

        [MenuItem("Tools/Project333/Owned Cards/Apply Pixel Collection Layout")]
        public static void ApplyPixelCollectionLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var sprite = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().FirstOrDefault()
                ?? Project333GameStartVisualBuilder.ImportButtonSprite();
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sprite == null || font == null)
                throw new System.InvalidOperationException("The collection button sprite and Korean font are required.");

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            try
            {
                var controller = FindController(scene);
                if (controller == null) throw new System.InvalidOperationException("Owned Cards controller was not found.");
                controller.MaterializeOwnedCardsUiForEditor();
                var serialized = new SerializedObject(controller);
                var root = (RectTransform)controller.transform;
                var background = root.Find("Background")?.GetComponent<Image>();
                if (background != null) background.color = Ink;
                if (controller.GetComponentInParent<SafeAreaFitter>() == null)
                {
                    var safeArea = controller.gameObject.AddComponent<SafeAreaFitter>();
                    safeArea.Configure(background != null ? new[] { background.rectTransform } : new RectTransform[0]);
                }

                var header = Ref<Text>(serialized, "_titleText").transform.parent as RectTransform;
                Region(header, 0f, 0.875f, 1f, 1f, 0f);
                PaintPanel(header, Panel, false);
                var title = Ref<Text>(serialized, "_titleText");
                Region(title.rectTransform, 0.025f, 0.25f, 0.35f, 0.85f, 0f);
                StyleText(title, font, 44, Cream, TextAnchor.MiddleLeft);
                title.text = "보유 카드";
                var subtitle = Label(header, "CollectionSubtitle", font, 16, Muted, "COLLECTION / AFTER333");
                Region(subtitle.rectTransform, 0.028f, 0.07f, 0.35f, 0.30f, 0f);
                var account = Ref<Text>(serialized, "_accountText");
                Region(account.rectTransform, 0.37f, 0.16f, 0.70f, 0.85f, 0f);
                StyleText(account, font, 23, Cream, TextAnchor.MiddleRight);
                account.resizeTextForBestFit = true;
                account.resizeTextMinSize = 14;
                account.resizeTextMaxSize = 23;
                var back = Ref<Button>(serialized, "_backButton");
                Place((RectTransform)back.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(235f, 65f), new Vector2(-130f, 0f));
                StyleButton(back, sprite, font, "시작 화면", 25);
                var rule = PanelObject(header, "HeaderGoldRule", Gold, false);
                Region(rule, 0.025f, 0f, 0.975f, 0f, 0f);
                rule.sizeDelta = new Vector2(0f, 2f);

                var grid = Ref<RectTransform>(serialized, "_cardGridRoot");
                var collection = (RectTransform)grid.parent;
                Region(collection, 0.025f, 0.09f, 0.627f, 0.85f, 0f);
                PaintPanel(collection, Panel);
                var oldScroll = collection.Find("Scroll View");
                if (oldScroll != null) oldScroll.gameObject.SetActive(false);
                var heading = Label(collection, "CollectionHeading", font, 24, Cream, "카드 도감");
                Region(heading.rectTransform, 0f, 1f, 1f, 1f, 0f);
                heading.rectTransform.offsetMin = new Vector2(26f, -60f);
                heading.rectTransform.offsetMax = new Vector2(-26f, -12f);
                Region(grid, 0f, 0f, 1f, 1f, 0f);
                grid.offsetMin = new Vector2(65f, 76f);
                grid.offsetMax = new Vector2(-65f, -68f);
                var layout = grid.GetComponent<GridLayoutGroup>();
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = 6;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.padding = new RectOffset();
                if (grid.GetComponent<OwnedCardsResponsiveGrid>() == null)
                    grid.gameObject.AddComponent<OwnedCardsResponsiveGrid>();
                foreach (Transform slot in grid)
                {
                    var artwork = slot.Find("Artwork") as RectTransform;
                    if (artwork == null) continue;
                    Region(artwork, 0f, 0.12f, 1f, 1f, 5f);
                    var count = slot.Find("CountLevelText")?.GetComponent<Text>();
                    if (count != null)
                    {
                        Region(count.rectTransform, 0f, 0f, 1f, 0.12f, 2f);
                        StyleText(count, font, 23, Cream, TextAnchor.MiddleCenter);
                        count.resizeTextForBestFit = true;
                        count.resizeTextMinSize = 12;
                        count.resizeTextMaxSize = 23;
                    }
                    var fallback = artwork.Find("FallbackNameText")?.GetComponent<Text>();
                    if (fallback != null) StyleText(fallback, font, 20, Cream, TextAnchor.MiddleCenter);
                }
                var previous = Ref<Button>(serialized, "_previousPageButton");
                var next = Ref<Button>(serialized, "_nextPageButton");
                StyleButton(previous, null, font, "<", 36);
                StyleButton(next, null, font, ">", 36);
                PaintPanel((RectTransform)previous.transform, Ink);
                PaintPanel((RectTransform)next.transform, Ink);
                Place((RectTransform)previous.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(44f, 92f), new Vector2(10f, 0f));
                Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(44f, 92f), new Vector2(-10f, 0f));
                var page = Ref<Text>(serialized, "_pageText");
                Region(page.rectTransform, 0.35f, 0.015f, 0.65f, 0.07f, 0f);
                StyleText(page, font, 23, Cream, TextAnchor.MiddleCenter);
                var legend = Label(collection, "UpgradeLegend", font, 16,
                    new Color(0.43f, 0.85f, 0.53f), "초록 테두리 · 강화 가능");
                Region(legend.rectTransform, 0.65f, 0.015f, 0.98f, 0.07f, 0f);
                legend.alignment = TextAnchor.MiddleRight;

                var detail = Ref<RectTransform>(serialized, "_detailPanelRoot");
                Region(detail, 0.648f, 0.09f, 0.975f, 0.85f, 0f);
                PaintPanel(detail, Panel);
                var detailTitle = Ref<Text>(serialized, "_detailTitleText");
                Region(detailTitle.rectTransform, 0.035f, 0.90f, 0.965f, 0.985f, 0f);
                StyleText(detailTitle, font, 30, Cream, TextAnchor.MiddleCenter);
                detailTitle.resizeTextForBestFit = true;
                detailTitle.resizeTextMinSize = 16;
                detailTitle.resizeTextMaxSize = 30;
                var artworkImage = Ref<Image>(serialized, "_detailArtworkImage");
                Region(artworkImage.rectTransform, 0.035f, 0.535f, 0.465f, 0.885f, 0f);
                var ownership = Ref<Text>(serialized, "_detailOwnershipText");
                Region(ownership.rectTransform, 0.5f, 0.55f, 0.965f, 0.87f, 0f);
                StyleText(ownership, font, 22, Cream, TextAnchor.MiddleLeft);
                ownership.resizeTextForBestFit = true;
                ownership.resizeTextMinSize = 15;
                ownership.resizeTextMaxSize = 22;
                CreateDetailScroll(detail, Ref<Text>(serialized, "_detailMetaText"), font);
                var upgrade = Ref<Button>(serialized, "_upgradeButton");
                Region((RectTransform)upgrade.transform, 0.19f, 0.025f, 0.81f, 0.12f, 0f);
                StyleButton(upgrade, sprite, font, "강화", 26);
                var status = Ref<Text>(serialized, "_statusText");
                Region(status.rectTransform, 0.04f, 0.012f, 0.96f, 0.065f, 0f);
                StyleText(status, font, 20, Muted, TextAnchor.MiddleCenter);

                serialized.FindProperty("_gridSlotColor").colorValue = Ink;
                serialized.FindProperty("_gridSlotEmptyArtworkColor").colorValue = new Color(0.075f, 0.15f, 0.16f);
                serialized.FindProperty("_detailPanelColor").colorValue = Panel;
                serialized.FindProperty("_ownedCountTextColor").colorValue = Cream;
                serialized.FindProperty("_detailAccentColor").colorValue = Cream;
                serialized.FindProperty("_arrowButtonColor").colorValue = Ink;
                serialized.FindProperty("_upgradeButtonColor").colorValue = Color.white;
                serialized.FindProperty("_upgradeButtonUnavailableColor").colorValue = Color.white;
                serialized.FindProperty("_collectionVisualVersion").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (closeAfter || !wasDirty) EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CreateDetailScroll(RectTransform detail, Text text, Font font)
        {
            var root = PanelObject(detail, "DetailInfoScroll", Ink, false);
            Region(root, 0.035f, 0.145f, 0.965f, 0.505f, 0f);
            var viewport = PanelObject(root, "Viewport", Color.clear, false);
            Region(viewport, 0f, 0f, 1f, 1f, 14f);
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            text.transform.SetParent(viewport, false);
            Region(text.rectTransform, 0f, 1f, 1f, 1f, 0f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            StyleText(text, font, 22, new Color(0.82f, 0.88f, 0.85f), TextAnchor.UpperLeft);
            text.supportRichText = true;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var fitter = text.GetComponent<ContentSizeFitter>() ?? text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.GetComponent<ScrollRect>() ?? root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = text.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            root.GetComponent<Image>().raycastTarget = true;
            viewport.GetComponent<Image>().raycastTarget = true;
        }

        private static T Ref<T>(SerializedObject serialized, string name) where T : Object
            => (T)serialized.FindProperty(name).objectReferenceValue;

        private static void StyleText(Text text, Font font, int size, Color color, TextAnchor alignment)
        {
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static void StyleButton(Button button, Sprite sprite, Font font, string title, int size)
        {
            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            var label = button.GetComponentInChildren<Text>(true);
            StyleText(label, font, size, Cream, TextAnchor.MiddleCenter);
            label.text = title;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = size;
            Region(label.rectTransform, 0f, 0f, 1f, 1f, 8f);
        }

        private static Text Label(RectTransform parent, string name, Font font, int size, Color color, string value)
        {
            var existing = parent.Find(name)?.GetComponent<Text>();
            var text = existing ?? new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            StyleText(text, font, size, color, TextAnchor.MiddleLeft);
            text.text = value;
            return text;
        }

        private static RectTransform PanelObject(RectTransform parent, string name, Color color, bool border = true)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }
            PaintPanel(rect, color, border);
            return rect;
        }

        private static void PaintPanel(RectTransform rect, Color color, bool border = true)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = rect.GetComponent<Button>() != null;
            if (!border) return;
            var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
        }

        private static void Region(RectTransform rect, float x0, float y0, float x1, float y1, float padding)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.one * padding;
            rect.offsetMax = -Vector2.one * padding;
            rect.localScale = Vector3.one;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }
    }
}
