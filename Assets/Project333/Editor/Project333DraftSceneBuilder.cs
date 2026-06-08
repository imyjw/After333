using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Draft;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333DraftSceneBuilder
    {
        private const string StartScenePath = "Assets/GameStart_VSlice.unity";
        private const string DraftScenePath = "Assets/Draft_VSlice.unity";
        private const string BattleScenePath = "Assets/Battle_VSlice.unity";
        private const string StarterTenCatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        private static readonly Color BackgroundColor = new Color(0.06f, 0.07f, 0.1f, 1f);
        private static readonly Color PanelColor = new Color(0.1f, 0.12f, 0.16f, 0.94f);
        private static readonly Color AccentColor = new Color(0.84f, 0.74f, 0.42f, 1f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.28f, 0.46f, 1f);

        [MenuItem("Tools/Project333/Draft/Create Draft Scene")]
        public static void CreateDraftScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Draft_VSlice";

            CreateCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();

            var controllerObject = new GameObject("DraftSceneController", typeof(RectTransform), typeof(DraftSceneController));
            var controllerRect = controllerObject.GetComponent<RectTransform>();
            controllerRect.SetParent(canvas.transform, false);
            controllerRect.anchorMin = Vector2.zero;
            controllerRect.anchorMax = Vector2.one;
            controllerRect.offsetMin = Vector2.zero;
            controllerRect.offsetMax = Vector2.zero;

            var background = CreateImage("Background", controllerRect, BackgroundColor);
            StretchFull(background.rectTransform);

            var headerPanel = CreateImage("HeaderPanel", controllerRect, PanelColor);
            headerPanel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            headerPanel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            headerPanel.rectTransform.pivot = new Vector2(0.5f, 1f);
            headerPanel.rectTransform.sizeDelta = new Vector2(1200f, 160f);
            headerPanel.rectTransform.anchoredPosition = new Vector2(0f, -24f);

            var titleText = CreateText("SceneTitle", headerPanel.rectTransform, 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            titleText.text = "Project 333 Draft";
            titleText.color = AccentColor;
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(20f, -64f);
            titleText.rectTransform.offsetMax = new Vector2(-20f, -10f);

            var recordText = CreateText("RecordText", headerPanel.rectTransform, 26, FontStyle.Bold, TextAnchor.MiddleLeft);
            recordText.text = "Wins: 0   Losses: 0";
            recordText.rectTransform.anchorMin = new Vector2(0f, 0f);
            recordText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            recordText.rectTransform.pivot = new Vector2(0f, 0f);
            recordText.rectTransform.offsetMin = new Vector2(28f, 18f);
            recordText.rectTransform.offsetMax = new Vector2(-10f, 54f);

            var lastResultText = CreateText("LastResultText", headerPanel.rectTransform, 24, FontStyle.Normal, TextAnchor.MiddleRight);
            lastResultText.text = "Last Battle: -";
            lastResultText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            lastResultText.rectTransform.anchorMax = new Vector2(1f, 0f);
            lastResultText.rectTransform.pivot = new Vector2(1f, 0f);
            lastResultText.rectTransform.offsetMin = new Vector2(10f, 18f);
            lastResultText.rectTransform.offsetMax = new Vector2(-28f, 54f);

            var leftPanel = CreateImage("ControlPanel", controllerRect, PanelColor);
            leftPanel.rectTransform.anchorMin = new Vector2(0f, 0f);
            leftPanel.rectTransform.anchorMax = new Vector2(0f, 1f);
            leftPanel.rectTransform.pivot = new Vector2(0f, 0.5f);
            leftPanel.rectTransform.sizeDelta = new Vector2(420f, 0f);
            leftPanel.rectTransform.offsetMin = new Vector2(24f, 24f);
            leftPanel.rectTransform.offsetMax = new Vector2(444f, -208f);

            var startDraftButton = CreateButton("StartDraftButton", leftPanel.rectTransform, "Start Draft");
            startDraftButton.GetComponent<Image>().color = ButtonColor;
            startDraftButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1f);
            startDraftButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
            startDraftButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            startDraftButton.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 72f);
            startDraftButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -28f);

            var startBattleButton = CreateButton("StartBattleButton", leftPanel.rectTransform, "Start Battle (0/33)");
            startBattleButton.GetComponent<Image>().color = AccentColor;
            startBattleButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1f);
            startBattleButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
            startBattleButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            startBattleButton.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 72f);
            startBattleButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -116f);

            var returnToStartButton = CreateButton("ReturnToStartButton", leftPanel.rectTransform, "Back To Start");
            returnToStartButton.GetComponent<Image>().color = new Color(0.24f, 0.24f, 0.28f, 1f);
            returnToStartButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1f);
            returnToStartButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
            returnToStartButton.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            returnToStartButton.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 72f);
            returnToStartButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -204f);

            var statusTitleText = CreateText("StatusTitleText", leftPanel.rectTransform, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            statusTitleText.text = "Status";
            statusTitleText.color = AccentColor;
            statusTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            statusTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            statusTitleText.rectTransform.offsetMin = new Vector2(28f, -302f);
            statusTitleText.rectTransform.offsetMax = new Vector2(-28f, -264f);

            var statusText = CreateText("StatusText", leftPanel.rectTransform, 22, FontStyle.Normal, TextAnchor.UpperLeft);
            statusText.text = "Ready.";
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusText.rectTransform.offsetMin = new Vector2(28f, 24f);
            statusText.rectTransform.offsetMax = new Vector2(-28f, -306f);

            var rightPanel = CreateImage("DeckPanel", controllerRect, PanelColor);
            rightPanel.rectTransform.anchorMin = new Vector2(1f, 0f);
            rightPanel.rectTransform.anchorMax = new Vector2(1f, 1f);
            rightPanel.rectTransform.pivot = new Vector2(1f, 0.5f);
            rightPanel.rectTransform.sizeDelta = new Vector2(420f, 0f);
            rightPanel.rectTransform.offsetMin = new Vector2(-444f, 24f);
            rightPanel.rectTransform.offsetMax = new Vector2(-24f, -208f);

            var deckTitleText = CreateText("DeckTitleText", rightPanel.rectTransform, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            deckTitleText.text = "Current Draft Deck";
            deckTitleText.color = AccentColor;
            deckTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            deckTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            deckTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            deckTitleText.rectTransform.offsetMin = new Vector2(20f, -54f);
            deckTitleText.rectTransform.offsetMax = new Vector2(-20f, -12f);

            var deckScrollView = CreateScrollView("DeckScrollView", rightPanel.rectTransform);
            deckScrollView.RectTransform.anchorMin = new Vector2(0f, 0f);
            deckScrollView.RectTransform.anchorMax = new Vector2(1f, 1f);
            deckScrollView.RectTransform.offsetMin = new Vector2(20f, 20f);
            deckScrollView.RectTransform.offsetMax = new Vector2(-20f, -68f);

            var deckListText = CreateText("DeckListText", deckScrollView.Content, 22, FontStyle.Normal, TextAnchor.UpperLeft);
            deckListText.text = "No drafted deck yet.";
            deckListText.horizontalOverflow = HorizontalWrapMode.Wrap;
            deckListText.verticalOverflow = VerticalWrapMode.Overflow;
            deckListText.rectTransform.anchorMin = new Vector2(0f, 1f);
            deckListText.rectTransform.anchorMax = new Vector2(1f, 1f);
            deckListText.rectTransform.pivot = new Vector2(0.5f, 1f);
            deckListText.rectTransform.offsetMin = new Vector2(12f, 0f);
            deckListText.rectTransform.offsetMax = new Vector2(-12f, 0f);
            deckListText.rectTransform.anchoredPosition = Vector2.zero;
            deckListText.rectTransform.sizeDelta = new Vector2(0f, 1200f);

            Project333DraftOverlaySceneBuilder.CreateSceneDraftOverlay();
            var overlayPresenter = UnityEngine.Object.FindFirstObjectByType<DraftOverlayPresenter>();

            var controller = controllerObject.GetComponent<DraftSceneController>();
            AssignControllerReferences(
                controller,
                overlayPresenter,
                recordText,
                lastResultText,
                statusText,
                deckListText,
                deckScrollView.ScrollRect,
                startDraftButton,
                startBattleButton,
                returnToStartButton,
                startBattleButton.GetComponentInChildren<Text>(),
                AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(StarterTenCatalogPath));

            UnityEventTools.AddPersistentListener(startDraftButton.onClick, controller.BeginDraftFromUi);
            UnityEventTools.AddPersistentListener(startBattleButton.onClick, controller.StartBattleFromUi);
            UnityEventTools.AddPersistentListener(returnToStartButton.onClick, controller.ReturnToStartSceneFromUi);

            SaveScene(scene);
            EnsureSceneInBuildSettings(StartScenePath);
            EnsureSceneInBuildSettings(DraftScenePath);
            EnsureSceneInBuildSettings(BattleScenePath);
            Selection.activeGameObject = controller.gameObject;
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.SaveScene(scene, DraftScenePath);
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            var existingScenes = EditorBuildSettings.scenes;
            foreach (var scene in existingScenes)
            {
                if (scene.path == scenePath)
                {
                    return;
                }
            }

            var nextScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
            for (var i = 0; i < existingScenes.Length; i++)
            {
                nextScenes[i] = existingScenes[i];
            }

            nextScenes[nextScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = nextScenes;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.tag = "MainCamera";
            return camera;
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(null, false);
        }

        private static void AssignControllerReferences(
            DraftSceneController controller,
            DraftOverlayPresenter overlayPresenter,
            Text recordText,
            Text lastResultText,
            Text statusText,
            Text deckListText,
            ScrollRect deckListScrollRect,
            Button startDraftButton,
            Button startBattleButton,
            Button returnToStartButton,
            Text startBattleButtonLabel,
            CardDefinitionCatalogAsset cardCatalogAsset)
        {
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("_cardCatalogAsset").objectReferenceValue = cardCatalogAsset;
            serializedObject.FindProperty("_draftOverlayPresenter").objectReferenceValue = overlayPresenter;
            serializedObject.FindProperty("_recordText").objectReferenceValue = recordText;
            serializedObject.FindProperty("_lastResultText").objectReferenceValue = lastResultText;
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("_deckListText").objectReferenceValue = deckListText;
            serializedObject.FindProperty("_deckListScrollRect").objectReferenceValue = deckListScrollRect;
            serializedObject.FindProperty("_startDraftButton").objectReferenceValue = startDraftButton;
            serializedObject.FindProperty("_startBattleButton").objectReferenceValue = startBattleButton;
            serializedObject.FindProperty("_returnToStartButton").objectReferenceValue = returnToStartButton;
            serializedObject.FindProperty("_startBattleButtonLabel").objectReferenceValue = startBattleButtonLabel;
            serializedObject.FindProperty("_startSceneName").stringValue = "GameStart_VSlice";
            serializedObject.FindProperty("_draftSceneName").stringValue = "Draft_VSlice";
            serializedObject.FindProperty("_battleSceneName").stringValue = "Battle_VSlice";
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Button CreateButton(string name, RectTransform parent, string label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var labelText = CreateText("Label", rectTransform, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(labelText.rectTransform, 12f);
            labelText.text = label;
            labelText.color = Color.white;
            return button;
        }

        private static ScrollViewReferences CreateScrollView(string name, RectTransform parent)
        {
            var scrollObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.SetParent(parent, false);

            var background = scrollObject.GetComponent<Image>();
            background.color = new Color(0.06f, 0.08f, 0.11f, 0.96f);

            var mask = scrollObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.SetParent(scrollRectTransform, false);
            StretchFull(viewportRect);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 1200f);

            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            return new ScrollViewReferences
            {
                RectTransform = scrollRectTransform,
                Content = contentRect,
                ScrollRect = scrollRect,
            };
        }

        private static Text CreateText(string name, RectTransform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            return text;
        }

        private static Font ResolveFont()
        {
            try
            {
                var dynamicFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial Unicode MS" }, 32);
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

        private static void StretchFull(RectTransform rectTransform, float padding = 0f)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(padding, padding);
            rectTransform.offsetMax = new Vector2(-padding, -padding);
        }

        private sealed class ScrollViewReferences
        {
            public RectTransform RectTransform;
            public RectTransform Content;
            public ScrollRect ScrollRect;
        }
    }
}
