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
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
        };

        [MenuItem("Tools/Project333/Draft/Create Scene Draft Overlay")]
        public static void CreateSceneDraftOverlay()
        {
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
            contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.sizeDelta = new Vector2(1480f, 650f);

            var titleText = CreateText("TitleText", contentRoot, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchTop(titleText.rectTransform, 0f, 54f, 20f);
            titleText.text = "Legendary Opening Pick";

            var progressText = CreateText("ProgressText", contentRoot, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchTop(progressText.rectTransform, 62f, 38f, 20f);
            progressText.text = "Pick 1 / 33";

            var statusText = CreateText("StatusText", contentRoot, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchBottom(statusText.rectTransform, 0f, 46f, 24f);
            statusText.text = "Current Deck: 0 / 33   Unique Cards: 0";

            var bodyRow = CreateRectChild("BodyRow", contentRoot);
            bodyRow.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRow.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRow.pivot = new Vector2(0.5f, 0.5f);
            bodyRow.sizeDelta = new Vector2(1380f, 490f);
            bodyRow.anchoredPosition = new Vector2(0f, -6f);

            var bodyLayout = bodyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.childAlignment = TextAnchor.MiddleCenter;
            bodyLayout.childControlHeight = false;
            bodyLayout.childControlWidth = false;
            bodyLayout.childForceExpandHeight = false;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.spacing = 26f;

            var optionsRoot = CreateRectChild("OptionsRoot", bodyRow);
            optionsRoot.sizeDelta = new Vector2(970f, 470f);

            var optionsLayoutElement = optionsRoot.gameObject.AddComponent<LayoutElement>();
            optionsLayoutElement.preferredWidth = 970f;
            optionsLayoutElement.preferredHeight = 470f;

            var optionsLayout = optionsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            optionsLayout.childAlignment = TextAnchor.MiddleCenter;
            optionsLayout.childControlHeight = false;
            optionsLayout.childControlWidth = false;
            optionsLayout.childForceExpandHeight = false;
            optionsLayout.childForceExpandWidth = false;
            optionsLayout.spacing = 30f;

            var optionBindings = new DraftOptionReferences[3];
            for (var i = 0; i < optionBindings.Length; i++)
            {
                optionBindings[i] = CreateOptionView(optionsRoot, i);
            }

            var deckPanelReferences = CreateDeckPanel(bodyRow);

            ApplySerializedReferences(
                presenter,
                canvas,
                canvasGroup,
                titleText,
                progressText,
                statusText,
                deckPanelReferences.TitleText,
                deckPanelReferences.ListText,
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
            Text progressText,
            Text statusText,
            Text deckPanelTitleText,
            Text deckListText,
            DraftOptionReferences[] optionReferences)
        {
            var serializedObject = new SerializedObject(presenter);
            serializedObject.FindProperty("_targetCanvas").objectReferenceValue = canvas;
            serializedObject.FindProperty("_rootCanvasGroup").objectReferenceValue = canvasGroup;
            serializedObject.FindProperty("_titleText").objectReferenceValue = titleText;
            serializedObject.FindProperty("_progressText").objectReferenceValue = progressText;
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("_deckPanelTitleText").objectReferenceValue = deckPanelTitleText;
            serializedObject.FindProperty("_deckListText").objectReferenceValue = deckListText;

            var optionBindingsProperty = serializedObject.FindProperty("_optionBindings");
            optionBindingsProperty.arraySize = optionReferences.Length;

            for (var i = 0; i < optionReferences.Length; i++)
            {
                var optionProperty = optionBindingsProperty.GetArrayElementAtIndex(i);
                optionProperty.FindPropertyRelative("_button").objectReferenceValue = optionReferences[i].Button;
                optionProperty.FindPropertyRelative("_artworkImage").objectReferenceValue = optionReferences[i].ArtworkImage;
                optionProperty.FindPropertyRelative("_titleText").objectReferenceValue = optionReferences[i].TitleText;
                optionProperty.FindPropertyRelative("_subtitleText").objectReferenceValue = optionReferences[i].SubtitleText;
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
            optionRectTransform.sizeDelta = new Vector2(290f, 460f);

            var layoutElement = optionRoot.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 290f;
            layoutElement.preferredHeight = 460f;

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
            artworkRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            artworkRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            artworkRectTransform.pivot = new Vector2(0.5f, 0.5f);
            artworkRectTransform.sizeDelta = new Vector2(272f, 364f);
            artworkRectTransform.anchoredPosition = new Vector2(0f, 20f);

            var artworkImage = artworkObject.GetComponent<Image>();
            artworkImage.preserveAspect = true;
            artworkImage.raycastTarget = false;
            artworkImage.color = new Color(0.18f, 0.2f, 0.25f, 1f);

            var titleText = CreateText("OptionTitle", optionRectTransform, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchBottom(titleText.rectTransform, 44f, 48f, 12f);
            titleText.text = $"Draft Card {index + 1}";

            var subtitleText = CreateText("OptionSubtitle", optionRectTransform, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchBottom(subtitleText.rectTransform, 8f, 30f, 12f);
            subtitleText.text = "Unit  M 3";

            return new DraftOptionReferences
            {
                Button = button,
                ArtworkImage = artworkImage,
                TitleText = titleText,
                SubtitleText = subtitleText,
            };
        }

        private static DeckPanelReferences CreateDeckPanel(RectTransform parent)
        {
            var deckPanel = new GameObject("DeckPanel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(deckPanel, "Create Deck Panel");
            var deckPanelRectTransform = deckPanel.GetComponent<RectTransform>();
            deckPanelRectTransform.SetParent(parent, false);
            deckPanelRectTransform.sizeDelta = new Vector2(384f, 470f);

            var layoutElement = deckPanel.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 384f;
            layoutElement.preferredHeight = 470f;

            var panelImage = deckPanel.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.12f, 0.16f, 0.96f);
            panelImage.raycastTarget = false;

            var titleText = CreateText("DeckPanelTitle", deckPanelRectTransform, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchTop(titleText.rectTransform, 14f, 38f, 16f);
            titleText.text = "Current Deck";

            var listText = CreateText("DeckListText", deckPanelRectTransform, 20, FontStyle.Normal, TextAnchor.UpperLeft);
            listText.horizontalOverflow = HorizontalWrapMode.Wrap;
            listText.verticalOverflow = VerticalWrapMode.Overflow;
            StretchFill(listText.rectTransform, 60f, 18f, 18f, 16f);
            listText.text = "No picks yet.";

            return new DeckPanelReferences
            {
                TitleText = titleText,
                ListText = listText,
            };
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
            public Text TitleText;
            public Text SubtitleText;
        }

        private sealed class DeckPanelReferences
        {
            public Text TitleText;
            public Text ListText;
        }
    }
}
