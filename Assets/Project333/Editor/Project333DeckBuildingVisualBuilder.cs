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
    public static class Project333DeckBuildingVisualBuilder
    {
        private const string ScenePath = "Assets/DeckBuilding_VSlice.unity";
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

        private static DraftOverlayPresenter FindPresenter(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<DraftOverlayPresenter>(true)).SingleOrDefault();

        private static void ApplyInitialLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath) || !File.Exists(ButtonPath)) return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (FindPresenter(scene)?.DeckBuildingVisualVersion >= 1) return;
            }
            else if (File.ReadAllText(ScenePath).Contains("_deckBuildingVisualVersion: 1")) return;
            ApplyPixelDeckBuildingLayout();
        }

        [MenuItem("Tools/Project333/Deck Building/Apply Pixel Deck Building Layout")]
        public static void ApplyPixelDeckBuildingLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var sprite = AssetDatabase.LoadAllAssetsAtPath(ButtonPath).OfType<Sprite>().FirstOrDefault()
                ?? Project333GameStartVisualBuilder.ImportButtonSprite();
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sprite == null || font == null)
                throw new System.InvalidOperationException("Deck building requires the pixel menu sprite and Korean font.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            try
            {
                var presenter = FindPresenter(scene);
                if (presenter == null) throw new System.InvalidOperationException("Draft overlay presenter was not found.");
                presenter.RefreshEditableSceneUi();
                var serialized = new SerializedObject(presenter);
                var root = (RectTransform)Ref<CanvasGroup>(serialized, "_rootCanvasGroup").transform;
                var background = root.GetComponent<Image>();
                background.color = Ink;
                // Fit this overlay only, without moving the Draft controller or other canvases.
                if (root.GetComponentInParent<SafeAreaFitter>() == null)
                    root.gameObject.AddComponent<SafeAreaFitter>().Configure();

                var header = Box(root, "DeckBuildingHeader", Panel, 0f, 0.86f, 1f, 1f);
                header.transform.SetAsFirstSibling();
                Box(header.rectTransform, "GoldRule", Gold, 0.02f, 0f, 0.98f, 0.012f);
                Box(root, "BottomRule", new Color(0.22f, 0.31f, 0.30f), 0.02f, 0.09f, 0.98f, 0.092f);
                Label(root, "DeckBuildingFooter", "카드 한 장을 선택해 덱을 완성하세요.", font, 22, Muted,
                    0.04f, 0.025f, 0.75f, 0.075f);

                var title = Ref<Text>(serialized, "_titleText");
                title.transform.SetParent(root, false);
                Region(title.rectTransform, 0.20f, 0.93f, 0.75f, 0.99f);
                Style(title, font, 38, Cream, TextAnchor.MiddleCenter);
                var status = Ref<Text>(serialized, "_statusText");
                status.transform.SetParent(root, false);
                Region(status.rectTransform, 0.20f, 0.875f, 0.75f, 0.93f);
                Style(status, font, 23, Muted, TextAnchor.MiddleCenter);
                // Keep both live title/status strings and the user's stat anchors unchanged.
                var options = serialized.FindProperty("_optionBindings");
                for (var i = 0; i < options.arraySize; i++)
                {
                    var binding = options.GetArrayElementAtIndex(i);
                    var button = (Button)binding.FindPropertyRelative("_button").objectReferenceValue;
                    var face = button.GetComponent<Image>();
                    face.color = Panel;
                    face.raycastTarget = true;
                    button.targetGraphic = face;
                    var outline = face.GetComponent<Outline>() ?? face.gameObject.AddComponent<Outline>();
                    outline.effectColor = Gold;
                    outline.effectDistance = new Vector2(4f, -4f);
                    outline.useGraphicAlpha = false;
                    var colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1f, 0.95f, 0.74f);
                    colors.pressedColor = new Color(0.65f, 0.85f, 0.78f);
                    colors.selectedColor = Color.white;
                    button.colors = colors;
                    button.transition = Selectable.Transition.ColorTint;
                }

                var deck = Ref<RectTransform>(serialized, "_deckPanelRectTransform");
                deck.GetComponent<Image>().color = Panel;
                var deckOutline = deck.GetComponent<Outline>() ?? deck.gameObject.AddComponent<Outline>();
                deckOutline.effectColor = Gold;
                deckOutline.effectDistance = new Vector2(2f, -2f);
                var headingBar = Box(deck, "DeckHeadingBar", new Color(0.065f, 0.15f, 0.16f), 0f, 0.885f, 1f, 1f);
                headingBar.transform.SetAsFirstSibling();
                Box(deck, "DeckHeadingRule", Gold, 0.04f, 0.885f, 0.96f, 0.888f);
                var deckTitle = Ref<Text>(serialized, "_deckPanelTitleText");
                Region(deckTitle.rectTransform, 0.05f, 0.90f, 0.95f, 0.99f);
                Style(deckTitle, font, 27, Cream, TextAnchor.MiddleCenter);
                var list = Ref<Text>(serialized, "_deckListText");
                Style(list, font, 22, new Color(0.85f, 0.91f, 0.87f), TextAnchor.UpperLeft);
                list.resizeTextForBestFit = false;
                list.verticalOverflow = VerticalWrapMode.Overflow;
                list.lineSpacing = 1.15f;
                var scroll = Ref<ScrollRect>(serialized, "_deckListScrollRect");
                Region((RectTransform)scroll.transform, 0.065f, 0.085f, 0.935f, 0.855f);
                Label(deck, "DeckScrollHint", "스크롤해서 전체 목록 보기", font, 16, Muted,
                    0.04f, 0.015f, 0.96f, 0.07f);

                var back = root.Find("BackToStartButton").GetComponent<Button>();
                Region((RectTransform)back.transform, 0.02f, 0.905f, 0.18f, 0.98f);
                back.GetComponent<Image>().sprite = sprite;
                back.GetComponent<Image>().color = Color.white;
                var backLabel = back.GetComponentInChildren<Text>(true);
                Region(backLabel.rectTransform, 0.07f, 0.12f, 0.93f, 0.88f);
                Style(backLabel, font, 25, Cream, TextAnchor.MiddleCenter);
                backLabel.text = "시작 화면";

                StyleWallet(scene, font);
                serialized.FindProperty("_interfaceFont").objectReferenceValue = font;
                serialized.FindProperty("_overlayColor").colorValue = Ink;
                serialized.FindProperty("_titleColor").colorValue = Cream;
                serialized.FindProperty("_bodyColor").colorValue = Muted;
                serialized.FindProperty("_backButtonLabel").stringValue = "시작 화면";
                serialized.FindProperty("_backButtonColor").colorValue = Color.white;
                serialized.FindProperty("_backButtonTextColor").colorValue = Cream;
                serialized.FindProperty("_deckBuildingVisualVersion").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                presenter.RefreshEditableSceneUi();
                EditorSceneManager.MarkSceneDirty(scene);
                if (closeAfter || !wasDirty) EditorSceneManager.SaveScene(scene);
                Debug.Log("After333 pixel DeckBuilding layout applied. Card spacing, stats and draft state were preserved.");
            }
            finally
            {
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void StyleWallet(Scene scene, Font font)
        {
            var controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DraftSceneController>(true)).SingleOrDefault();
            if (controller == null) return;
            var serialized = new SerializedObject(controller);
            var text = Ref<Text>(serialized, "_serverWalletText");
            if (text == null) return;
            var panel = (RectTransform)text.transform.parent;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.sizeDelta = new Vector2(280f, 86f);
            panel.anchoredPosition = new Vector2(-126f, -24f);
            panel.GetComponent<Image>().color = Panel;
            Region(text.rectTransform, 0.06f, 0.06f, 0.94f, 0.94f);
            Style(text, font, 22, Cream, TextAnchor.MiddleRight);
            serialized.FindProperty("_serverWalletPanelColor").colorValue = Panel;
            serialized.FindProperty("_serverWalletTextColor").colorValue = Cream;
            serialized.FindProperty("_serverWalletOffset").vector2Value = panel.anchoredPosition;
            serialized.FindProperty("_serverWalletSize").vector2Value = panel.sizeDelta;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Ref<T>(SerializedObject serialized, string name) where T : Object
            => (T)serialized.FindProperty(name).objectReferenceValue;

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
            float x0, float y0, float x1, float y1)
        {
            var text = parent.Find(name)?.GetComponent<Text>();
            if (text == null)
            {
                text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                text.transform.SetParent(parent, false);
            }
            Region(text.rectTransform, x0, y0, x1, y1);
            Style(text, font, size, color, TextAnchor.MiddleCenter);
            text.text = value;
        }
    }
}
