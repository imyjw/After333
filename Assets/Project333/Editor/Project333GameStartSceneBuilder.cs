using Project333.Runtime.Presentation.Startup;
using Project333.Runtime.Presentation.Ads;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333GameStartSceneBuilder
    {
        private const string StartScenePath = "Assets/GameStart_VSlice.unity";
        private const string DraftScenePath = "Assets/Draft_VSlice.unity";
        private const string DeckBuildingScenePath = "Assets/DeckBuilding_VSlice.unity";
        private const string BattleScenePath = "Assets/Battle_VSlice.unity";
        private const string OwnedCardsScenePath = "Assets/OwnedCards_VSlice.unity";
        private const string StartBackgroundAssetPath = "Assets/Project333/Resources/Project333/StartScene/GameStartBackground.png";
        private const string StartButtonAssetPath = "Assets/Project333/Resources/Project333/StartScene/GameStartButton.png";

        private static readonly Color BackgroundColor = new Color(0.05f, 0.07f, 0.11f, 1f);
        private static readonly Color PanelColor = new Color(0.11f, 0.13f, 0.18f, 0.95f);
        private static readonly Color AccentColor = new Color(0.86f, 0.74f, 0.34f, 1f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.33f, 0.58f, 1f);

        [MenuItem("Tools/Project333/Start/Create Start Scene")]
        public static void CreateStartScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GameStart_VSlice";

            CreateCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();

            var controllerObject = new GameObject(
                "GameStartSceneController",
                typeof(RectTransform),
                typeof(GameStartSceneController),
                typeof(RewardedTicketController));
            var controllerRect = controllerObject.GetComponent<RectTransform>();
            controllerRect.SetParent(canvas.transform, false);
            StretchFull(controllerRect);

            var background = CreateImage("Background", controllerRect, BackgroundColor);
            StretchFull(background.rectTransform);

            var accountInfoPanel = CreateImage("AccountInfoPanel", controllerRect, new Color(0f, 0f, 0f, 0.62f));
            accountInfoPanel.raycastTarget = false;
            accountInfoPanel.rectTransform.anchorMin = new Vector2(0f, 1f);
            accountInfoPanel.rectTransform.anchorMax = new Vector2(0f, 1f);
            accountInfoPanel.rectTransform.pivot = new Vector2(0f, 1f);
            accountInfoPanel.rectTransform.sizeDelta = new Vector2(560f, 116f);
            accountInfoPanel.rectTransform.anchoredPosition = new Vector2(32f, -32f);

            var accountInfoText = CreateText("AccountInfoText", accountInfoPanel.rectTransform, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            accountInfoText.text = "서버 계정 로그인 중...\nServer Tickets: -   Gold: -";
            accountInfoText.raycastTarget = false;
            StretchFull(accountInfoText.rectTransform, 18f);

            var centerPanel = CreateImage("CenterPanel", controllerRect, PanelColor);
            centerPanel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            centerPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            centerPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            centerPanel.rectTransform.sizeDelta = new Vector2(860f, 420f);
            centerPanel.rectTransform.anchoredPosition = Vector2.zero;

            var titleText = CreateText("TitleText", centerPanel.rectTransform, 56, FontStyle.Bold, TextAnchor.MiddleCenter);
            titleText.text = "After333";
            titleText.color = AccentColor;
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(24f, -96f);
            titleText.rectTransform.offsetMax = new Vector2(-24f, -20f);

            var ticketText = CreateText("TicketText", centerPanel.rectTransform, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            ticketText.text = "Tickets: 9";
            ticketText.rectTransform.anchorMin = new Vector2(0f, 1f);
            ticketText.rectTransform.anchorMax = new Vector2(1f, 1f);
            ticketText.rectTransform.pivot = new Vector2(0.5f, 1f);
            ticketText.rectTransform.offsetMin = new Vector2(24f, -160f);
            ticketText.rectTransform.offsetMax = new Vector2(-24f, -112f);

            var startButton = CreateButton("StartGameButton", centerPanel.rectTransform, "게임 시작 (-3 Tickets)");
            var startButtonRect = startButton.GetComponent<RectTransform>();
            startButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
            startButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
            startButtonRect.pivot = new Vector2(0.5f, 0.5f);
            startButtonRect.sizeDelta = new Vector2(480f, 112f);
            startButtonRect.anchoredPosition = new Vector2(0f, -8f);
            startButton.GetComponent<Image>().color = ButtonColor;

            var ownedCardsButton = CreateButton("OwnedCardsButton", centerPanel.rectTransform, "보유 카드");
            var ownedCardsButtonRect = ownedCardsButton.GetComponent<RectTransform>();
            ownedCardsButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
            ownedCardsButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
            ownedCardsButtonRect.pivot = new Vector2(0.5f, 0.5f);
            ownedCardsButtonRect.sizeDelta = new Vector2(280f, 64f);
            ownedCardsButtonRect.anchoredPosition = new Vector2(0f, -304f);
            ownedCardsButton.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.32f, 0.94f);

            var statusText = CreateText("StatusText", centerPanel.rectTransform, 26, FontStyle.Normal, TextAnchor.UpperCenter);
            statusText.text = "게임을 시작하면 티켓 3개를 차감하고 드래프트로 이동합니다.";
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            statusText.rectTransform.offsetMin = new Vector2(36f, 40f);
            statusText.rectTransform.offsetMax = new Vector2(-36f, 136f);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;

            var controller = controllerObject.GetComponent<GameStartSceneController>();
            AssignControllerReferences(
                controller,
                background,
                startButton.GetComponent<Image>(),
                ticketText,
                statusText,
                accountInfoPanel.rectTransform,
                accountInfoText,
                startButton,
                startButton.GetComponentInChildren<Text>(),
                ownedCardsButton,
                ownedCardsButton.GetComponentInChildren<Text>());

            UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGameFromUi);
            controllerObject.GetComponent<RewardedTicketController>()?.EnsureEditableHierarchy();

            SaveScene(scene);
            EnsureSceneInBuildSettings(StartScenePath, insertAtFront: true);
            EnsureSceneInBuildSettings(OwnedCardsScenePath, insertAtFront: false);
            EnsureSceneInBuildSettings(DraftScenePath, insertAtFront: false);
            EnsureSceneInBuildSettings(DeckBuildingScenePath, insertAtFront: false);
            EnsureSceneInBuildSettings(BattleScenePath, insertAtFront: false);
            Selection.activeGameObject = controller.gameObject;
        }

        [MenuItem("Tools/Project333/Start/Add Owned Cards Button To Start Scene")]
        public static void AddOwnedCardsButtonToStartScene()
        {
            var scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<GameStartSceneController>();
            if (controller == null)
            {
                Debug.LogWarning("GameStartSceneController was not found in GameStart_VSlice.");
                return;
            }

            var controllerRect = controller.transform as RectTransform;
            if (controllerRect == null)
            {
                Debug.LogWarning("GameStartSceneController does not have a RectTransform.");
                return;
            }

            var parent = controllerRect.Find("CenterPanel") as RectTransform ?? controllerRect;
            var ownedCardsButtonTransform = parent.Find("OwnedCardsButton") as RectTransform;
            Button ownedCardsButton;
            if (ownedCardsButtonTransform == null)
            {
                ownedCardsButton = CreateButton("OwnedCardsButton", parent, "보유 카드");
                ownedCardsButtonTransform = ownedCardsButton.GetComponent<RectTransform>();
            }
            else
            {
                ownedCardsButton = ownedCardsButtonTransform.GetComponent<Button>();
                if (ownedCardsButton == null)
                {
                    ownedCardsButton = ownedCardsButtonTransform.gameObject.AddComponent<Button>();
                }
            }

            ownedCardsButtonTransform.anchorMin = new Vector2(0.5f, 0.5f);
            ownedCardsButtonTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ownedCardsButtonTransform.pivot = new Vector2(0.5f, 0.5f);
            ownedCardsButtonTransform.sizeDelta = new Vector2(280f, 64f);
            ownedCardsButtonTransform.anchoredPosition = new Vector2(0f, -304f);

            var ownedCardsButtonImage = ownedCardsButton.GetComponent<Image>() ??
                                        ownedCardsButton.gameObject.AddComponent<Image>();
            ownedCardsButtonImage.color = new Color(0.16f, 0.2f, 0.32f, 0.94f);

            var label = ownedCardsButton.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                label = CreateText("Label", ownedCardsButtonTransform, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
                StretchFull(label.rectTransform, 10f);
            }

            label.text = "보유 카드";
            AssignOwnedCardsButtonReferences(controller, ownedCardsButton, label);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EnsureSceneInBuildSettings(StartScenePath, insertAtFront: true);
            EnsureSceneInBuildSettings(OwnedCardsScenePath, insertAtFront: false);
            Selection.activeGameObject = ownedCardsButton.gameObject;
        }

        private static void AssignControllerReferences(
            GameStartSceneController controller,
            Image backgroundImage,
            Image startButtonImage,
            Text ticketText,
            Text statusText,
            RectTransform accountInfoPanel,
            Text accountInfoText,
            Button startButton,
            Text startButtonLabel,
            Button ownedCardsButton,
            Text ownedCardsButtonLabel)
        {
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("_backgroundImage").objectReferenceValue = backgroundImage;
            serializedObject.FindProperty("_backgroundSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(StartBackgroundAssetPath);
            serializedObject.FindProperty("_backgroundResourcePath").stringValue = "Project333/StartScene/GameStartBackground";
            serializedObject.FindProperty("_startButtonImage").objectReferenceValue = startButtonImage;
            serializedObject.FindProperty("_startButtonSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(StartButtonAssetPath);
            serializedObject.FindProperty("_startButtonResourcePath").stringValue = "Project333/StartScene/GameStartButton";
            serializedObject.FindProperty("_ticketText").objectReferenceValue = ticketText;
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("_accountInfoPanel").objectReferenceValue = accountInfoPanel;
            serializedObject.FindProperty("_accountInfoText").objectReferenceValue = accountInfoText;
            serializedObject.FindProperty("_accountInfoPanelSize").vector2Value = new Vector2(560f, 116f);
            serializedObject.FindProperty("_accountInfoPanelPosition").vector2Value = new Vector2(32f, -32f);
            serializedObject.FindProperty("_accountInfoPanelColor").colorValue = new Color(0f, 0f, 0f, 0.62f);
            serializedObject.FindProperty("_accountInfoTextColor").colorValue = Color.white;
            serializedObject.FindProperty("_accountInfoFontSize").intValue = 24;
            serializedObject.FindProperty("_startGameButton").objectReferenceValue = startButton;
            serializedObject.FindProperty("_startGameButtonLabel").objectReferenceValue = startButtonLabel;
            serializedObject.FindProperty("_ownedCardsButton").objectReferenceValue = ownedCardsButton;
            serializedObject.FindProperty("_ownedCardsButtonLabel").objectReferenceValue = ownedCardsButtonLabel;
            serializedObject.FindProperty("_applyOwnedCardsButtonLayout").boolValue = true;
            serializedObject.FindProperty("_ownedCardsButtonSize").vector2Value = new Vector2(280f, 64f);
            serializedObject.FindProperty("_ownedCardsButtonPosition").vector2Value = new Vector2(0f, -304f);
            serializedObject.FindProperty("_accountServerUrl").stringValue = "http://127.0.0.1:7333";
            serializedObject.FindProperty("_clientVersion").stringValue =
                Project333.Runtime.Presentation.Project333ClientBuildInfo.CurrentClientVersion;
            serializedObject.FindProperty("_useServerAccount").boolValue = true;
            serializedObject.FindProperty("_draftSceneName").stringValue = "Draft_VSlice";
            serializedObject.FindProperty("_deckBuildingSceneName").stringValue = "DeckBuilding_VSlice";
            serializedObject.FindProperty("_battleSceneName").stringValue = "Battle_VSlice";
            serializedObject.FindProperty("_ownedCardsSceneName").stringValue = "OwnedCards_VSlice";
            serializedObject.FindProperty("_ticketCost").intValue = 3;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void AssignOwnedCardsButtonReferences(
            GameStartSceneController controller,
            Button ownedCardsButton,
            Text ownedCardsButtonLabel)
        {
            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("_ownedCardsButton").objectReferenceValue = ownedCardsButton;
            serializedObject.FindProperty("_ownedCardsButtonLabel").objectReferenceValue = ownedCardsButtonLabel;
            serializedObject.FindProperty("_applyOwnedCardsButtonLayout").boolValue = true;
            serializedObject.FindProperty("_ownedCardsButtonSize").vector2Value = new Vector2(280f, 64f);
            serializedObject.FindProperty("_ownedCardsButtonPosition").vector2Value = new Vector2(0f, -304f);
            serializedObject.FindProperty("_ownedCardsSceneName").stringValue = "OwnedCards_VSlice";
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.SaveScene(scene, StartScenePath);
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
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(null, false);
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

            var labelText = CreateText("Label", rectTransform, 32, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(labelText.rectTransform, 12f);
            labelText.text = label;
            labelText.color = Color.white;
            return button;
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

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void StretchFull(RectTransform rectTransform, float padding = 0f)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(padding, padding);
            rectTransform.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
