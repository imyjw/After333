using System;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Draft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333DraftOverlaySceneBuilder
    {
        private const string DeckBuildingSceneName = "DeckBuilding_VSlice";
        private const float OptionSpacing = 50f;
        private const float OptionAreaLeftPadding = 24f;
        private const float OptionVerticalOffset = -40f;
        private const float DefaultEditorOptionScale = 0.85f;
        private static readonly Vector2 OptionCardSize = new Vector2(550f, 733f);

        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
        };

        [MenuItem("Tools/Project333/Draft/Create Scene Draft Overlay")]
        public static void CreateSceneDraftOverlay()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, DeckBuildingSceneName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"Draft offer UI belongs only in {DeckBuildingSceneName}. Open that scene before creating the overlay.");
                return;
            }

            var canvas = GetOrCreateCanvas();
            if (canvas == null)
            {
                Debug.LogError("Could not create or resolve a Canvas for the draft overlay.");
                return;
            }

            var existingPresenter = UnityEngine.Object.FindFirstObjectByType<DraftOverlayPresenter>();
            if (existingPresenter != null && existingPresenter.transform.childCount > 0)
            {
                Selection.activeGameObject = existingPresenter.gameObject;
                Debug.LogWarning("An existing DraftOverlayPresenter with scene UI already exists. Edit that object directly, or delete it before creating a new one.");
                return;
            }

            var root = existingPresenter != null
                ? existingPresenter.gameObject
                : CreateRootGameObject(canvas.transform);

            Undo.RegisterFullObjectHierarchyUndo(root, "Create Scene Draft Overlay");

            var rectTransform = root.GetComponent<RectTransform>();
            var canvasGroup = root.GetComponent<CanvasGroup>();
            var rootImage = root.GetComponent<Image>();
            var presenter = root.GetComponent<DraftOverlayPresenter>();

            ConfigureRoot(rectTransform, rootImage);

            if (existingPresenter != null)
            {
                ClearChildren(root.transform);
            }

            var contentRoot = CreateRectChild("ContentRoot", rectTransform);
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;

            var titleText = CreateText("TitleText", contentRoot, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchTop(titleText.rectTransform, 16f, 54f, 20f);
            titleText.text = "Legendary Opening Pick";

            var statusText = CreateText("StatusText", contentRoot, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchTop(statusText.rectTransform, 78f, 46f, 24f);
            statusText.text = "Current Deck: 0 / 33   Unique Cards: 0";

            var optionsRoot = CreateRectChild("OptionsRoot", rectTransform);
            optionsRoot.anchorMin = new Vector2(0f, 0.5f);
            optionsRoot.anchorMax = new Vector2(0f, 0.5f);
            optionsRoot.pivot = new Vector2(0f, 0.5f);
            optionsRoot.anchoredPosition = new Vector2(OptionAreaLeftPadding, OptionVerticalOffset);
            optionsRoot.sizeDelta = new Vector2(
                (OptionCardSize.x * 3f) + (OptionSpacing * 2f),
                OptionCardSize.y);
            optionsRoot.localScale = new Vector3(
                DefaultEditorOptionScale,
                DefaultEditorOptionScale,
                1f);

            var optionsLayoutElement = optionsRoot.gameObject.AddComponent<LayoutElement>();
            optionsLayoutElement.ignoreLayout = true;
            optionsLayoutElement.preferredWidth = optionsRoot.sizeDelta.x;
            optionsLayoutElement.preferredHeight = OptionCardSize.y;

            var optionsLayout = optionsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            optionsLayout.childAlignment = TextAnchor.MiddleCenter;
            optionsLayout.childControlHeight = false;
            optionsLayout.childControlWidth = false;
            optionsLayout.childForceExpandHeight = false;
            optionsLayout.childForceExpandWidth = false;
            optionsLayout.spacing = OptionSpacing;

            var optionBindings = new DraftOptionReferences[3];
            for (var i = 0; i < optionBindings.Length; i++)
            {
                optionBindings[i] = CreateOptionView(optionsRoot, i);
            }

            var deckPanelReferences = CreateDeckPanel(rectTransform);

            ApplySerializedReferences(
                presenter,
                canvas,
                canvasGroup,
                titleText,
                statusText,
                deckPanelReferences.TitleText,
                deckPanelReferences.ListText,
                deckPanelReferences.ScrollRect,
                optionsRoot,
                deckPanelReferences.RectTransform,
                optionBindings);

            AssignBootstrapperReference(presenter);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("Created a scene-based DraftOverlayPresenter. You can now edit its child UI objects directly in the Hierarchy.");
        }

        private static GameObject CreateRootGameObject(Transform canvasTransform)
        {
            var root = new GameObject("DraftOverlayPresenter", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(DraftOverlayPresenter));
            Undo.RegisterCreatedObjectUndo(root, "Create Draft Overlay Presenter");
            root.transform.SetParent(canvasTransform, false);
            return root;
        }

        private static void ConfigureRoot(RectTransform rectTransform, Image rootImage)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.SetAsLastSibling();

            rootImage.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            rootImage.raycastTarget = true;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static Canvas GetOrCreateCanvas()
        {
            var existingCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                return existingCanvas;
            }

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            return canvas;
        }

        private static void AssignBootstrapperReference(DraftOverlayPresenter presenter)
        {
            var bootstrapper = UnityEngine.Object.FindFirstObjectByType<BattleBootstrapper>();
            if (bootstrapper == null)
            {
                return;
            }

            var bootstrapperSerializedObject = new SerializedObject(bootstrapper);
            bootstrapperSerializedObject.FindProperty("_draftOverlayPresenter").objectReferenceValue = presenter;
            bootstrapperSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrapper);
        }

        private static void ApplySerializedReferences(
            DraftOverlayPresenter presenter,
            Canvas canvas,
            CanvasGroup canvasGroup,
            Text titleText,
            Text statusText,
            Text deckPanelTitleText,
            Text deckListText,
            ScrollRect deckListScrollRect,
            RectTransform optionsRoot,
            RectTransform deckPanelRectTransform,
            DraftOptionReferences[] optionReferences)
        {
            var serializedObject = new SerializedObject(presenter);
            serializedObject.FindProperty("_targetCanvas").objectReferenceValue = canvas;
            serializedObject.FindProperty("_rootCanvasGroup").objectReferenceValue = canvasGroup;
            serializedObject.FindProperty("_titleText").objectReferenceValue = titleText;
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("_deckPanelTitleText").objectReferenceValue = deckPanelTitleText;
            serializedObject.FindProperty("_deckListText").objectReferenceValue = deckListText;
            serializedObject.FindProperty("_deckListScrollRect").objectReferenceValue = deckListScrollRect;
            serializedObject.FindProperty("_optionsRoot").objectReferenceValue = optionsRoot;
            serializedObject.FindProperty("_deckPanelRectTransform").objectReferenceValue = deckPanelRectTransform;

            var optionBindingsProperty = serializedObject.FindProperty("_optionBindings");
            optionBindingsProperty.arraySize = optionReferences.Length;

            for (var i = 0; i < optionReferences.Length; i++)
            {
                var optionProperty = optionBindingsProperty.GetArrayElementAtIndex(i);
                optionProperty.FindPropertyRelative("_button").objectReferenceValue = optionReferences[i].Button;
                optionProperty.FindPropertyRelative("_artworkImage").objectReferenceValue = optionReferences[i].ArtworkImage;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }

        private static DraftOptionReferences CreateOptionView(RectTransform parent, int index)
        {
            var optionRoot = new GameObject($"DraftOption_{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(optionRoot, "Create Draft Option");
            var optionRectTransform = optionRoot.GetComponent<RectTransform>();
            optionRectTransform.SetParent(parent, false);
            optionRectTransform.sizeDelta = OptionCardSize;

            var layoutElement = optionRoot.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = OptionCardSize.x;
            layoutElement.preferredHeight = OptionCardSize.y;

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
            Undo.RegisterCreatedObjectUndo(artworkObject, "Create Draft Artwork");
            var artworkRectTransform = artworkObject.GetComponent<RectTransform>();
            artworkRectTransform.SetParent(optionRectTransform, false);
            artworkRectTransform.anchorMin = Vector2.zero;
            artworkRectTransform.anchorMax = Vector2.one;
            artworkRectTransform.pivot = new Vector2(0.5f, 0.5f);
            artworkRectTransform.offsetMin = Vector2.zero;
            artworkRectTransform.offsetMax = Vector2.zero;

            var artworkImage = artworkObject.GetComponent<Image>();
            artworkImage.preserveAspect = true;
            artworkImage.raycastTarget = false;
            artworkImage.color = new Color(0.18f, 0.2f, 0.25f, 1f);

            return new DraftOptionReferences
            {
                Button = button,
                ArtworkImage = artworkImage,
            };
        }

        private static DeckPanelReferences CreateDeckPanel(RectTransform parent)
        {
            var deckPanel = new GameObject("DeckPanel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(deckPanel, "Create Deck Panel");
            var deckPanelRectTransform = deckPanel.GetComponent<RectTransform>();
            deckPanelRectTransform.SetParent(parent, false);
            deckPanelRectTransform.anchorMin = new Vector2(1f, 0.5f);
            deckPanelRectTransform.anchorMax = new Vector2(1f, 0.5f);
            deckPanelRectTransform.pivot = new Vector2(1f, 0.5f);
            deckPanelRectTransform.anchoredPosition = new Vector2(-24f, 0f);
            deckPanelRectTransform.sizeDelta = new Vector2(384f, 700f);

            var layoutElement = deckPanel.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            layoutElement.preferredWidth = 384f;
            layoutElement.preferredHeight = 700f;

            var panelImage = deckPanel.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.12f, 0.16f, 0.96f);
            panelImage.raycastTarget = false;

            var titleText = CreateText("DeckPanelTitle", deckPanelRectTransform, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchTop(titleText.rectTransform, 14f, 38f, 16f);
            titleText.text = "Current Deck";

            var scrollObject = new GameObject(
                "DeckListScrollView",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            Undo.RegisterCreatedObjectUndo(scrollObject, "Create Deck List Scroll View");
            var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.SetParent(deckPanelRectTransform, false);
            StretchFill(scrollRectTransform, 60f, 18f, 18f, 16f);

            var inputImage = scrollObject.GetComponent<Image>();
            inputImage.color = Color.clear;
            inputImage.raycastTarget = true;

            var contentObject = new GameObject("Content", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(contentObject, "Create Deck List Content");
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.SetParent(scrollRectTransform, false);
            ConfigureTopAnchoredRect(contentRect, 1f);

            var listText = CreateText("DeckListText", contentRect, 16, FontStyle.Normal, TextAnchor.UpperLeft);
            listText.horizontalOverflow = HorizontalWrapMode.Wrap;
            listText.verticalOverflow = VerticalWrapMode.Overflow;
            listText.raycastTarget = false;
            ConfigureTopAnchoredRect(listText.rectTransform, 1f);
            listText.text = "No picks yet.";

            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = scrollRectTransform;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 30f;

            return new DeckPanelReferences
            {
                RectTransform = deckPanelRectTransform,
                TitleText = titleText,
                ListText = listText,
                ScrollRect = scrollRect,
            };
        }

        private static void ConfigureTopAnchoredRect(RectTransform rectTransform, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(0f, -Mathf.Max(1f, height));
            rectTransform.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, RectTransform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            Undo.RegisterCreatedObjectUndo(textObject, "Create Draft Text");
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = new Color(0.9f, 0.93f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static Font ResolveFont()
        {
            try
            {
                var dynamicFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
                if (dynamicFont != null)
                {
                    return dynamicFont;
                }
            }
            catch
            {
                // Ignore and fall back.
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static RectTransform CreateRectChild(string name, RectTransform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(child, "Create Draft Layout");
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

        private sealed class DraftOptionReferences
        {
            public Button Button;
            public Image ArtworkImage;
        }

        private sealed class DeckPanelReferences
        {
            public RectTransform RectTransform;
            public Text TitleText;
            public Text ListText;
            public ScrollRect ScrollRect;
        }
    }
}
