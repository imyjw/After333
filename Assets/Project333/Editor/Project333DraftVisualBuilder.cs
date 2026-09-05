using System.IO;
using System.Linq;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Draft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333DraftVisualBuilder
    {
        private const string ScenePath = "Assets/Draft_VSlice.unity";
        private const string ButtonPath = "Assets/Project333/Resources/Project333/StartScene/PixelMenuButton.png";
        private const string FontPath = "Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static/NotoSansKR-Bold.ttf";
        private static readonly Color Ink = new Color(0.026f, 0.063f, 0.073f, 1f);
        private static readonly Color Panel = new Color(0.045f, 0.105f, 0.115f, 1f);
        private static readonly Color Gold = new Color(0.78f, 0.59f, 0.29f, 1f);
        private static readonly Color Cream = new Color(0.96f, 0.89f, 0.72f, 1f);
        private static readonly Color Muted = new Color(0.63f, 0.77f, 0.76f, 1f);

        [InitializeOnLoadMethod]
        private static void QueueInitialLayout()
        {
            EditorApplication.delayCall += ApplyInitialLayout;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += ApplyInitialLayout;
            };
        }

        private static DraftSceneController FindController(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<DraftSceneController>(true)).SingleOrDefault();

        private static void ApplyInitialLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath) || !File.Exists(ButtonPath)) return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (FindController(scene)?.DraftVisualVersion >= 1) return;
            }
            else if (File.ReadAllText(ScenePath).Contains("_draftVisualVersion: 1")) return;
            ApplyPixelDraftLayout();
        }

        [MenuItem("Tools/Project333/Draft/Apply Pixel Draft Layout")]
        public static void ApplyPixelDraftLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var sprite = AssetDatabase.LoadAllAssetsAtPath(ButtonPath).OfType<Sprite>().FirstOrDefault()
                ?? Project333GameStartVisualBuilder.ImportButtonSprite();
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sprite == null || font == null)
                throw new System.InvalidOperationException("Draft requires the shared pixel button sprite and Korean font.");

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            try
            {
                var controller = FindController(scene);
                if (controller == null) throw new System.InvalidOperationException("Draft scene controller was not found.");
                controller.MaterializeDraftUiForEditor();
                var serialized = new SerializedObject(controller);
                var root = (RectTransform)controller.transform;
                var canvas = controller.GetComponentInParent<Canvas>();
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;

                var backdrop = Box(root, "DraftPixelBackground", Ink, 0f, 0f, 1f, 1f);
                backdrop.transform.SetAsFirstSibling();
                var safeArea = root.GetComponent<SafeAreaFitter>() ?? root.gameObject.AddComponent<SafeAreaFitter>();
                safeArea.Configure(backdrop.rectTransform);
                Box(root, "LeftEdgeAccent", new Color(0.06f, 0.17f, 0.18f), 0.013f, 0.08f, 0.017f, 0.82f);

                var header = (RectTransform)root.Find("HeaderPanel");
                Region(header, 0f, 0.87f, 1f, 1f);
                Paint(header, Panel, false);
                Box(header, "HeaderGoldRule", Gold, 0.04f, 0f, 0.96f, 0.014f);
                var title = header.Find("SceneTitle").GetComponent<Text>();
                Region(title.rectTransform, 0.04f, 0.23f, 0.47f, 0.83f);
                Style(title, font, 42, Cream, TextAnchor.MiddleLeft);
                title.text = "전투 준비";
                Label(header, "DraftHeaderCaption", "DRAFT / AFTER333", font, 16, Muted,
                    0.043f, 0.055f, 0.45f, 0.27f, TextAnchor.MiddleLeft);

                var recordPanel = Box(root, "RunStatusPanel", Panel, 0.535f, 0.62f, 0.96f, 0.835f).rectTransform;
                Paint(recordPanel, Panel, true);
                Label(recordPanel, "RunStatusHeading", "이번 런", font, 22, Gold,
                    0.045f, 0.79f, 0.955f, 0.96f, TextAnchor.MiddleLeft);
                var record = Ref<Text>(serialized, "_recordText");
                record.transform.SetParent(recordPanel, false);
                Region(record.rectTransform, 0.045f, 0.43f, 0.955f, 0.79f);
                Style(record, font, 38, Cream, TextAnchor.MiddleLeft);
                var result = Ref<Text>(serialized, "_lastResultText");
                result.transform.SetParent(recordPanel, false);
                Region(result.rectTransform, 0.045f, 0.07f, 0.955f, 0.39f);
                Style(result, font, 23, Muted, TextAnchor.MiddleLeft);

                var control = (RectTransform)root.Find("ControlPanel");
                Region(control, 0.535f, 0.12f, 0.96f, 0.59f);
                Paint(control, Panel, true);
                Label(control, "BattleModeHeading", "다음 전투", font, 23, Gold,
                    0.055f, 0.85f, 0.945f, 0.97f, TextAnchor.MiddleLeft);
                StyleButton(Ref<Button>(serialized, "_startPveBattleButton"), sprite, font, 32,
                    0.055f, 0.555f, 0.945f, 0.815f);
                StyleButton(Ref<Button>(serialized, "_startPvpBattleButton"), sprite, font, 32,
                    0.055f, 0.27f, 0.945f, 0.53f);
                StyleButton(Ref<Button>(serialized, "_startDraftButton"), sprite, font, 25,
                    0.17f, 0.05f, 0.83f, 0.225f);

                // Keep the existing live status, including connection/save failures.
                var status = Ref<Text>(serialized, "_statusText");
                status.transform.SetParent(root, false);
                Region(status.rectTransform, 0.545f, 0.025f, 0.95f, 0.105f);
                Style(status, font, 20, Muted, TextAnchor.UpperLeft);
                var statusTitle = control.Find("StatusTitleText");
                if (statusTitle != null) statusTitle.gameObject.SetActive(false);

                var back = Ref<Button>(serialized, "_returnToStartButton");
                back.transform.SetParent(header, false);
                StyleButton(back, sprite, font, 25, 0.53f, 0.23f, 0.69f, 0.86f);
                back.GetComponentInChildren<Text>(true).text = "시작 화면";
                serialized.FindProperty("_returnToStartButtonLabel").stringValue = "시작 화면";

                StyleDeck(serialized, root, font);
                StyleWallet(serialized, font);
                // The claim button stays in its own centered canvas; no reward flow changes.
                var claim = Ref<Button>(serialized, "_claimRewardsButton");
                if (claim != null) StyleButtonVisual(claim, sprite, font, 28);

                serialized.FindProperty("_applyRuntimeDraftUiLayout").boolValue = false;
                serialized.FindProperty("_draftVisualVersion").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                controller.MaterializeDraftUiForEditor();
                Canvas.ForceUpdateCanvases();
                EditorSceneManager.MarkSceneDirty(scene);
                if (closeAfter || !wasDirty) EditorSceneManager.SaveScene(scene);
                Debug.Log("After333 pixel Draft layout applied. Battle, reconnect, rewards and deck state were preserved.");
            }
            finally
            {
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void StyleDeck(SerializedObject serialized, RectTransform root, Font font)
        {
            var deck = (RectTransform)root.Find("DeckPanel");
            Region(deck, 0.04f, 0.07f, 0.50f, 0.835f);
            Paint(deck, Panel, true);
            var title = deck.Find("DeckTitleText").GetComponent<Text>();
            Region(title.rectTransform, 0.045f, 0.895f, 0.955f, 0.98f);
            Style(title, font, 30, Cream, TextAnchor.MiddleLeft);
            title.text = "현재 덱";
            Box(deck, "DeckTitleRule", Gold, 0.04f, 0.885f, 0.96f, 0.888f);
            var surface = Ref<RectTransform>(serialized, "_runtimeDeckPanelSurface");
            Region(surface, 0.025f, 0.065f, 0.975f, 0.865f);
            Paint(surface, Panel, false);
            var bar = (RectTransform)surface.Find("RuntimeDeckSummaryBar");
            Region(bar, 0.02f, 0.91f, 0.98f, 1f);
            Paint(bar, new Color(0.065f, 0.15f, 0.16f), false);
            var summary = Ref<Text>(serialized, "_runtimeDeckSummaryText");
            Region(summary.rectTransform, 0.035f, 0.05f, 0.965f, 0.95f);
            Style(summary, font, 23, Cream, TextAnchor.MiddleLeft);
            var list = Ref<Text>(serialized, "_runtimeDeckPanelText");
            Style(list, font, 26, new Color(0.85f, 0.91f, 0.87f), TextAnchor.UpperLeft);
            list.resizeTextForBestFit = false;
            list.verticalOverflow = VerticalWrapMode.Overflow;
            list.lineSpacing = 1.18f;
            var scroll = Ref<ScrollRect>(serialized, "_runtimeDeckPanelScrollRect");
            Region((RectTransform)scroll.transform, 0.035f, 0.01f, 0.965f, 0.875f);
            Label(deck, "DeckScrollHint", "스크롤해서 전체 목록 보기", font, 17, Muted,
                0.04f, 0.013f, 0.96f, 0.052f, TextAnchor.MiddleCenter);
        }

        private static void StyleWallet(SerializedObject serialized, Font font)
        {
            var text = Ref<Text>(serialized, "_serverWalletText");
            if (text == null) return;
            var canvas = text.GetComponentInParent<Canvas>();
            SafeAreaFitter.EnsureCanvasContentRoot((RectTransform)canvas.transform, "DraftServerWalletSafeArea");
            var panel = (RectTransform)text.transform.parent;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-126f, -24f);
            panel.sizeDelta = new Vector2(280f, 86f);
            Paint(panel, Panel, false);
            Region(text.rectTransform, 0.06f, 0.06f, 0.94f, 0.94f);
            Style(text, font, 22, Cream, TextAnchor.MiddleRight);
        }

        private static T Ref<T>(SerializedObject serialized, string name) where T : Object
            => (T)serialized.FindProperty(name).objectReferenceValue;

        private static void StyleButton(Button button, Sprite sprite, Font font, int size,
            float x0, float y0, float x1, float y1)
        {
            if (button == null) return;
            Region((RectTransform)button.transform, x0, y0, x1, y1);
            StyleButtonVisual(button, sprite, font, size);
        }

        private static void StyleButtonVisual(Button button, Sprite sprite, Font font, int size)
        {
            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.75f);
            colors.pressedColor = new Color(0.65f, 0.83f, 0.78f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.48f, 0.54f, 0.53f, 0.7f);
            button.colors = colors;
            var label = button.GetComponentInChildren<Text>(true);
            if (label == null) return;
            Region(label.rectTransform, 0.06f, 0.12f, 0.94f, 0.88f);
            Style(label, font, size, Cream, TextAnchor.MiddleCenter);
        }

        private static void Style(Text text, Font font, int size, Color color, TextAnchor alignment)
        {
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = size;
        }

        private static void Region(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Paint(RectTransform rect, Color color, bool border)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = null;
            image.color = color;
            image.raycastTarget = false;
            var outline = rect.GetComponent<Outline>();
            if (!border)
            {
                if (outline != null) outline.enabled = false;
                return;
            }
            outline = outline ?? rect.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
        }

        private static Image Box(RectTransform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var image = parent.Find(name)?.GetComponent<Image>();
            if (image == null)
            {
                image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(parent, false);
            }
            Region(image.rectTransform, x0, y0, x1, y1);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Label(RectTransform parent, string name, string value, Font font, int size, Color color,
            float x0, float y0, float x1, float y1, TextAnchor alignment)
        {
            var text = parent.Find(name)?.GetComponent<Text>();
            if (text == null)
            {
                text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                text.transform.SetParent(parent, false);
            }
            Region(text.rectTransform, x0, y0, x1, y1);
            Style(text, font, size, color, alignment);
            text.text = value;
        }
    }
}
