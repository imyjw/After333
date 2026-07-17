using Project333.Runtime.Presentation.OwnedCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333OwnedCardsSceneBuilder
    {
        private const string OwnedCardsScenePath = "Assets/OwnedCards_VSlice.unity";
        private const string StartScenePath = "Assets/GameStart_VSlice.unity";
        private const string CardCatalogAssetPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";

        private static readonly Color BackgroundColor = new Color(0.035f, 0.045f, 0.07f, 1f);
        private static readonly Color HeaderColor = new Color(0.08f, 0.12f, 0.18f, 0.96f);
        private static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.12f, 0.94f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.34f, 1f);
        private static readonly Color AccentColor = new Color(0.88f, 0.76f, 0.42f, 1f);

        [MenuItem("Tools/Project333/Owned Cards/Create Owned Cards Scene")]
        public static void CreateOwnedCardsScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "OwnedCards_VSlice";

            CreateCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();

            var controllerObject = new GameObject("OwnedCardsSceneController", typeof(RectTransform), typeof(OwnedCardsSceneController));
            var controllerRect = controllerObject.GetComponent<RectTransform>();
            controllerRect.SetParent(canvas.transform, false);
            StretchFull(controllerRect);

            var background = CreateImage("Background", controllerRect, BackgroundColor);
            StretchFull(background.rectTransform);

            var headerPanel = CreateImage("HeaderPanel", controllerRect, HeaderColor);
            headerPanel.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerPanel.rectTransform.anchorMax = new Vector2(1f, 1f);
            headerPanel.rectTransform.pivot = new Vector2(0.5f, 1f);
            headerPanel.rectTransform.offsetMin = new Vector2(0f, -150f);
            headerPanel.rectTransform.offsetMax = Vector2.zero;

            var titleText = CreateText("TitleText", headerPanel.rectTransform, 44, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.text = "Owned Cards";
            titleText.color = AccentColor;
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(48f, 16f);
            titleText.rectTransform.offsetMax = new Vector2(-16f, -16f);

            var accountText = CreateText("AccountText", headerPanel.rectTransform, 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            accountText.text = "Server Account: -\nTickets: -   Gold: -";
            accountText.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            accountText.rectTransform.anchorMax = new Vector2(0.78f, 1f);
            accountText.rectTransform.offsetMin = new Vector2(16f, 18f);
            accountText.rectTransform.offsetMax = new Vector2(-16f, -18f);

            var backButton = CreateButton("BackButton", headerPanel.rectTransform, "Back To Start");
            var backButtonRect = backButton.GetComponent<RectTransform>();
            backButtonRect.anchorMin = new Vector2(1f, 0.5f);
            backButtonRect.anchorMax = new Vector2(1f, 0.5f);
            backButtonRect.pivot = new Vector2(1f, 0.5f);
            backButtonRect.sizeDelta = new Vector2(260f, 72f);
            backButtonRect.anchoredPosition = new Vector2(-44f, 0f);
            backButton.GetComponent<Image>().color = ButtonColor;

            var ownedCardsPanel = CreateImage("OwnedCardsPanel", controllerRect, PanelColor);
            ownedCardsPanel.rectTransform.anchorMin = new Vector2(0f, 0f);
            ownedCardsPanel.rectTransform.anchorMax = new Vector2(1f, 1f);
            ownedCardsPanel.rectTransform.offsetMin = new Vector2(56f, 96f);
            ownedCardsPanel.rectTransform.offsetMax = new Vector2(-56f, -184f);

            var scrollView = CreateScrollView(ownedCardsPanel.rectTransform, out var ownedCardsText);
            StretchFull(scrollView.GetComponent<RectTransform>(), 24f);

            var statusText = CreateText("StatusText", controllerRect, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            statusText.text = "보유 카드 정보를 준비 중입니다...";
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            statusText.rectTransform.offsetMin = new Vector2(56f, 30f);
            statusText.rectTransform.offsetMax = new Vector2(-56f, 78f);

            var controller = controllerObject.GetComponent<OwnedCardsSceneController>();
            AssignControllerReferences(
                controller,
                titleText,
                accountText,
                ownedCardsText,
                statusText,
                backButton,
                backButton.GetComponentInChildren<Text>());
            controller.MaterializeOwnedCardsUiForEditor();

            EditorSceneManager.SaveScene(scene, OwnedCardsScenePath);
            EnsureSceneInBuildSettings(StartScenePath, insertAtFront: true);
            EnsureSceneInBuildSettings(OwnedCardsScenePath, insertAtFront: false);
            Selection.activeGameObject = controllerObject;
        }

        private static void AssignControllerReferences(
            OwnedCardsSceneController controller,
            Text titleText,
            Text accountText,
            Text ownedCardsText,
            Text statusText,
            Button backButton,
            Text backButtonLabel)
        {
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("_cardCatalogAsset").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Object>(CardCatalogAssetPath);
            serializedObject.FindProperty("_titleText").objectReferenceValue = titleText;
            serializedObject.FindProperty("_accountText").objectReferenceValue = accountText;
            serializedObject.FindProperty("_ownedCardsText").objectReferenceValue = ownedCardsText;
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedObject.FindProperty("_backButtonLabel").objectReferenceValue = backButtonLabel;
            serializedObject.FindProperty("_accountServerUrl").stringValue = "http://127.0.0.1:7333";
            serializedObject.FindProperty("_clientVersion").stringValue =
                Project333.Runtime.Presentation.Project333ClientBuildInfo.CurrentClientVersion;
            serializedObject.FindProperty("_startSceneName").stringValue = "GameStart_VSlice";
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static ScrollRect CreateScrollView(RectTransform parent, out Text ownedCardsText)
        {
            var scrollObject = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.SetParent(parent, false);

            var scrollImage = scrollObject.GetComponent<Image>();
            scrollImage.color = new Color(0.025f, 0.035f, 0.055f, 0.9f);
            scrollImage.raycastTarget = true;

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.SetParent(scrollRectTransform, false);
            StretchFull(viewportRect, 18f);

            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;

            var mask = viewportObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(28, 28, 24, 24);

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ownedCardsText = CreateText("OwnedCardsText", contentRect, 28, FontStyle.Normal, TextAnchor.UpperLeft);
            ownedCardsText.text = "보유 카드 목록을 불러오는 중...";
            ownedCardsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            ownedCardsText.verticalOverflow = VerticalWrapMode.Overflow;

            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            return scrollRect;
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
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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

        private static Text CreateText(
            string name,
            RectTransform parent,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, RectTransform parent, string label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 0.9f);
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.62f);
            button.colors = colors;

            var labelText = CreateText("Label", buttonRect, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            labelText.text = label;
            StretchFull(labelText.rectTransform, 10f);
            return button;
        }

        private static void StretchFull(RectTransform rectTransform, float padding = 0f)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(padding, padding);
            rectTransform.offsetMax = new Vector2(-padding, -padding);
        }

        private static void EnsureSceneInBuildSettings(string scenePath, bool insertAtFront)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            var existingScenes = EditorBuildSettings.scenes;
            for (var i = 0; i < existingScenes.Length; i++)
            {
                if (existingScenes[i].path != scenePath)
                {
                    continue;
                }

                if (insertAtFront && i > 0)
                {
                    var scene = existingScenes[i];
                    for (var moveIndex = i; moveIndex > 0; moveIndex--)
                    {
                        existingScenes[moveIndex] = existingScenes[moveIndex - 1];
                    }

                    existingScenes[0] = scene;
                    EditorBuildSettings.scenes = existingScenes;
                }

                return;
            }

            var nextScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
            if (insertAtFront)
            {
                nextScenes[0] = new EditorBuildSettingsScene(scenePath, true);
                for (var i = 0; i < existingScenes.Length; i++)
                {
                    nextScenes[i + 1] = existingScenes[i];
                }
            }
            else
            {
                for (var i = 0; i < existingScenes.Length; i++)
                {
                    nextScenes[i] = existingScenes[i];
                }

                nextScenes[nextScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            }

            EditorBuildSettings.scenes = nextScenes;
        }
    }
}
