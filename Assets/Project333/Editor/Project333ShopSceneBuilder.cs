using System.IO;
using Project333.Runtime.Presentation.Ads;
using Project333.Runtime.Presentation.Shop;
using Project333.Runtime.Presentation.Startup;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333ShopSceneBuilder
    {
        private const string ShopScenePath = "Assets/Shop_VSlice.unity";
        private const string StartScenePath = "Assets/GameStart_VSlice.unity";

        [InitializeOnLoadMethod]
        private static void QueueShopSceneSetup()
        {
            EditorApplication.delayCall += EnsureShopSceneSetup;
        }

        private static void EnsureShopSceneSetup()
        {
            EditorApplication.delayCall -= EnsureShopSceneSetup;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(ShopScenePath))
            {
                CreateShopSceneAndUpdateStartScene();
                return;
            }

            if (!HasCompletedRewardedAdMove())
            {
                MoveRewardedAdToShopScene();
            }
        }

        [MenuItem("Tools/Project333/Shop/Create Shop Scene And Update Start Scene")]
        public static void CreateShopSceneAndUpdateStartScene()
        {
            CreateShopScene();
            MoveRewardedAdToShopScene();
            UpdateStartSceneShopButton();
            EnsureSceneInBuildSettings(ShopScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("After333 shop scene created and GameStart shop button updated.");
        }

        private static void CreateShopScene()
        {
            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "Shop_VSlice";
            SceneManager.SetActiveScene(scene);

            CreateCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();

            var controllerObject = new GameObject(
                "ShopSceneController",
                typeof(RectTransform),
                typeof(RewardedTicketController),
                typeof(ShopSceneController));
            var controllerRect = controllerObject.GetComponent<RectTransform>();
            controllerRect.SetParent(canvas.transform, false);
            StretchFull(controllerRect);

            var background = CreateImage(
                "Background",
                controllerRect,
                new Color(0.025f, 0.055f, 0.07f, 1f));
            StretchFull(background.rectTransform);

            var header = CreateImage(
                "HeaderPanel",
                controllerRect,
                new Color(0.035f, 0.09f, 0.11f, 0.96f));
            header.rectTransform.anchorMin = new Vector2(0f, 0.78f);
            header.rectTransform.anchorMax = Vector2.one;
            header.rectTransform.offsetMin = Vector2.zero;
            header.rectTransform.offsetMax = Vector2.zero;

            var titleText = CreateText("TitleText", header.rectTransform, 58, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.text = "상점";
            titleText.color = new Color(1f, 0.83f, 0.38f, 1f);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(72f, 24f);
            titleText.rectTransform.offsetMax = new Vector2(-24f, -24f);

            var accountText = CreateText("AccountText", header.rectTransform, 27, FontStyle.Bold, TextAnchor.MiddleCenter);
            accountText.text = "계정\n티켓: -    골드: -";
            accountText.rectTransform.anchorMin = new Vector2(0.34f, 0f);
            accountText.rectTransform.anchorMax = new Vector2(0.76f, 1f);
            accountText.rectTransform.offsetMin = new Vector2(12f, 24f);
            accountText.rectTransform.offsetMax = new Vector2(-12f, -24f);

            var backButton = CreateButton("BackButton", header.rectTransform, "시작 화면으로");
            var backButtonRect = backButton.GetComponent<RectTransform>();
            backButtonRect.anchorMin = new Vector2(1f, 0.5f);
            backButtonRect.anchorMax = new Vector2(1f, 0.5f);
            backButtonRect.pivot = new Vector2(1f, 0.5f);
            backButtonRect.sizeDelta = new Vector2(260f, 76f);
            backButtonRect.anchoredPosition = new Vector2(-64f, 0f);
            backButton.GetComponent<Image>().color = new Color(0.13f, 0.22f, 0.27f, 1f);

            var productPanel = CreateImage(
                "TicketProductPanel",
                controllerRect,
                new Color(0.055f, 0.11f, 0.13f, 0.97f));
            productPanel.rectTransform.anchorMin = new Vector2(0.07f, 0.18f);
            productPanel.rectTransform.anchorMax = new Vector2(0.48f, 0.7f);
            productPanel.rectTransform.offsetMin = Vector2.zero;
            productPanel.rectTransform.offsetMax = Vector2.zero;

            var productTitle = CreateText("ProductTitleText", productPanel.rectTransform, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            productTitle.text = "게임 티켓";
            productTitle.color = new Color(1f, 0.9f, 0.56f, 1f);
            productTitle.rectTransform.anchorMin = new Vector2(0.08f, 0.72f);
            productTitle.rectTransform.anchorMax = new Vector2(0.92f, 0.94f);
            productTitle.rectTransform.offsetMin = Vector2.zero;
            productTitle.rectTransform.offsetMax = Vector2.zero;

            var descriptionText = CreateText("ProductDescriptionText", productPanel.rectTransform, 28, FontStyle.Normal, TextAnchor.MiddleCenter);
            descriptionText.text = "서버 골드 3개로 게임 티켓 1개를 구매합니다.";
            descriptionText.rectTransform.anchorMin = new Vector2(0.08f, 0.5f);
            descriptionText.rectTransform.anchorMax = new Vector2(0.92f, 0.7f);
            descriptionText.rectTransform.offsetMin = Vector2.zero;
            descriptionText.rectTransform.offsetMax = Vector2.zero;

            var purchaseButton = CreateButton("PurchaseTicketButton", productPanel.rectTransform, "게임 티켓 1개 구매\n골드 3");
            var purchaseButtonRect = purchaseButton.GetComponent<RectTransform>();
            purchaseButtonRect.anchorMin = new Vector2(0.5f, 0.36f);
            purchaseButtonRect.anchorMax = new Vector2(0.5f, 0.36f);
            purchaseButtonRect.pivot = new Vector2(0.5f, 0.5f);
            purchaseButtonRect.sizeDelta = new Vector2(430f, 118f);
            purchaseButtonRect.anchoredPosition = Vector2.zero;
            purchaseButton.GetComponent<Image>().color = new Color(0.06f, 0.42f, 0.38f, 1f);

            var statusText = CreateText("StatusText", productPanel.rectTransform, 25, FontStyle.Normal, TextAnchor.MiddleCenter);
            statusText.text = "구매할 상품을 선택해주세요.";
            statusText.rectTransform.anchorMin = new Vector2(0.08f, 0.07f);
            statusText.rectTransform.anchorMax = new Vector2(0.92f, 0.22f);
            statusText.rectTransform.offsetMin = Vector2.zero;
            statusText.rectTransform.offsetMax = Vector2.zero;

            var controller = controllerObject.GetComponent<ShopSceneController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("_titleText").objectReferenceValue = titleText;
            serializedController.FindProperty("_accountText").objectReferenceValue = accountText;
            serializedController.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedController.FindProperty("_purchaseTicketButton").objectReferenceValue = purchaseButton;
            serializedController.FindProperty("_purchaseTicketButtonLabel").objectReferenceValue =
                purchaseButton.GetComponentInChildren<Text>(true);
            serializedController.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedController.FindProperty("_backButtonLabel").objectReferenceValue =
                backButton.GetComponentInChildren<Text>(true);
            serializedController.FindProperty("_startSceneName").stringValue = "GameStart_VSlice";
            serializedController.FindProperty("_ticketPurchaseGoldCost").intValue = 3;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            var rewardedPanel = EnsureRewardedProductPanel(controllerRect);
            ConfigureRewardedTicketController(
                controllerObject.GetComponent<RewardedTicketController>(),
                rewardedPanel);

            EditorSceneManager.SaveScene(scene, ShopScenePath);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            EditorSceneManager.CloseScene(scene, true);
        }

        [MenuItem("Tools/Project333/Shop/Move Rewarded Ad To Shop Scene")]
        public static void MoveRewardedAdToShopScene()
        {
            if (!File.Exists(ShopScenePath) || !File.Exists(StartScenePath))
            {
                return;
            }

            var shopScene = SceneManager.GetSceneByPath(ShopScenePath);
            var closeShopWhenFinished = !shopScene.IsValid() || !shopScene.isLoaded;
            if (closeShopWhenFinished)
            {
                shopScene = EditorSceneManager.OpenScene(ShopScenePath, OpenSceneMode.Additive);
            }

            var startScene = SceneManager.GetSceneByPath(StartScenePath);
            var closeStartWhenFinished = !startScene.IsValid() || !startScene.isLoaded;
            if (closeStartWhenFinished)
            {
                startScene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Additive);
            }

            var shopController = FindSceneComponent<ShopSceneController>(shopScene);
            var legacyController = FindSceneComponent<RewardedTicketController>(startScene);
            if (shopController == null)
            {
                Debug.LogWarning("ShopSceneController was not found in Shop_VSlice.");
                CloseOpenedScene(shopScene, closeShopWhenFinished);
                CloseOpenedScene(startScene, closeStartWhenFinished);
                return;
            }

            var shopRewardedController = shopController.GetComponent<RewardedTicketController>() ??
                                         shopController.gameObject.AddComponent<RewardedTicketController>();
            if (legacyController != null)
            {
                CopyRewardedAdSettings(legacyController, shopRewardedController);
            }

            var shopRoot = shopController.transform as RectTransform;
            var rewardedPanel = EnsureRewardedProductPanel(shopRoot);
            ConfigureRewardedTicketController(shopRewardedController, rewardedPanel);
            RemoveLegacyRewardedAdFromStartScene(startScene, legacyController);

            SaveOrMarkScene(shopScene, closeShopWhenFinished);
            SaveOrMarkScene(startScene, closeStartWhenFinished);
            CloseOpenedScene(shopScene, closeShopWhenFinished);
            CloseOpenedScene(startScene, closeStartWhenFinished);
            Debug.Log("After333 rewarded ticket button moved from GameStart to Shop.");
        }

        private static bool HasCompletedRewardedAdMove()
        {
            var shopText = File.ReadAllText(ShopScenePath);
            var startText = File.ReadAllText(StartScenePath);
            return shopText.Contains("RewardedTicketProductPanel") &&
                   shopText.Contains("RewardedTicketController") &&
                   !startText.Contains("RewardedTicketController") &&
                   !startText.Contains("WatchRewardedAdButton");
        }

        private static RectTransform EnsureRewardedProductPanel(RectTransform shopRoot)
        {
            if (shopRoot == null)
            {
                return null;
            }

            if (shopRoot.Find("TicketProductPanel") is RectTransform ticketPanel)
            {
                ticketPanel.anchorMin = new Vector2(0.07f, 0.18f);
                ticketPanel.anchorMax = new Vector2(0.48f, 0.7f);
                ticketPanel.offsetMin = Vector2.zero;
                ticketPanel.offsetMax = Vector2.zero;
            }

            var existing = shopRoot.Find("RewardedTicketProductPanel") as RectTransform;
            if (existing != null)
            {
                return existing;
            }

            var panel = CreateImage(
                "RewardedTicketProductPanel",
                shopRoot,
                new Color(0.055f, 0.11f, 0.13f, 0.97f));
            panel.rectTransform.anchorMin = new Vector2(0.52f, 0.18f);
            panel.rectTransform.anchorMax = new Vector2(0.93f, 0.7f);
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = Vector2.zero;

            var title = CreateText(
                "RewardedProductTitleText",
                panel.rectTransform,
                42,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            title.text = "광고 보상";
            title.color = new Color(1f, 0.9f, 0.56f, 1f);
            title.rectTransform.anchorMin = new Vector2(0.08f, 0.72f);
            title.rectTransform.anchorMax = new Vector2(0.92f, 0.94f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;

            var description = CreateText(
                "RewardedProductDescriptionText",
                panel.rectTransform,
                28,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            description.text = "광고를 끝까지 시청하면 게임 티켓 1개를 받습니다.";
            description.rectTransform.anchorMin = new Vector2(0.08f, 0.5f);
            description.rectTransform.anchorMax = new Vector2(0.92f, 0.7f);
            description.rectTransform.offsetMin = Vector2.zero;
            description.rectTransform.offsetMax = Vector2.zero;
            return panel.rectTransform;
        }

        private static void ConfigureRewardedTicketController(
            RewardedTicketController controller,
            RectTransform parent)
        {
            if (controller == null || parent == null)
            {
                return;
            }

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_uiParent").objectReferenceValue = parent;
            serialized.FindProperty("_watchAdButton").objectReferenceValue = null;
            serialized.FindProperty("_watchAdButtonLabel").objectReferenceValue = null;
            serialized.FindProperty("_rewardedAdStatusText").objectReferenceValue = null;
            serialized.FindProperty("_buttonSize").vector2Value = new Vector2(430f, 118f);
            serialized.FindProperty("_buttonPosition").vector2Value = new Vector2(0f, -15f);
            serialized.FindProperty("_statusSize").vector2Value = new Vector2(500f, 96f);
            serialized.FindProperty("_statusPosition").vector2Value = new Vector2(0f, -152f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.EnsureEditableHierarchy();
            EditorUtility.SetDirty(controller);
        }

        private static void CopyRewardedAdSettings(
            RewardedTicketController source,
            RewardedTicketController destination)
        {
            var sourceSerialized = new SerializedObject(source);
            var destinationSerialized = new SerializedObject(destination);
            CopyString(sourceSerialized, destinationSerialized, "_levelPlayAppKey");
            CopyString(sourceSerialized, destinationSerialized, "_rewardedAdUnitId");
            CopyString(sourceSerialized, destinationSerialized, "_placementName");
            CopyString(sourceSerialized, destinationSerialized, "_accountServerUrl");
            CopyFloat(sourceSerialized, destinationSerialized, "_verificationTimeoutSeconds");
            CopyFloat(sourceSerialized, destinationSerialized, "_verificationPollSeconds");
            destinationSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopyString(
            SerializedObject source,
            SerializedObject destination,
            string propertyName)
        {
            destination.FindProperty(propertyName).stringValue =
                source.FindProperty(propertyName).stringValue;
        }

        private static void CopyFloat(
            SerializedObject source,
            SerializedObject destination,
            string propertyName)
        {
            destination.FindProperty(propertyName).floatValue =
                source.FindProperty(propertyName).floatValue;
        }

        private static void RemoveLegacyRewardedAdFromStartScene(
            Scene startScene,
            RewardedTicketController legacyController)
        {
            if (!startScene.IsValid())
            {
                return;
            }

            var startController = FindSceneComponent<GameStartSceneController>(startScene);
            var centerPanel = startController?.transform.Find("CenterPanel");
            DestroyChild(centerPanel, "WatchRewardedAdButton");
            DestroyChild(centerPanel, "RewardedAdStatusText");
            if (legacyController != null)
            {
                Object.DestroyImmediate(legacyController);
            }

            EditorSceneManager.MarkSceneDirty(startScene);
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            var child = parent?.Find(childName);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void SaveOrMarkScene(Scene scene, bool wasOpenedByBuilder)
        {
            if (!scene.IsValid())
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (wasOpenedByBuilder)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void CloseOpenedScene(Scene scene, bool wasOpenedByBuilder)
        {
            if (wasOpenedByBuilder && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void UpdateStartSceneShopButton()
        {
            var startScene = SceneManager.GetSceneByPath(StartScenePath);
            var closeWhenFinished = !startScene.IsValid() || !startScene.isLoaded;
            if (closeWhenFinished)
            {
                startScene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Additive);
            }

            var controller = FindSceneComponent<GameStartSceneController>(startScene);
            if (controller == null)
            {
                Debug.LogWarning("GameStartSceneController was not found in GameStart_VSlice.");
                if (closeWhenFinished)
                {
                    EditorSceneManager.CloseScene(startScene, true);
                }

                return;
            }

            var controllerRect = controller.transform as RectTransform;
            var parent = controllerRect != null
                ? controllerRect.Find("CenterPanel") as RectTransform ?? controllerRect
                : null;
            if (parent == null)
            {
                Debug.LogWarning("GameStartSceneController does not have a valid UI root.");
                return;
            }

            var buttonTransform = parent.Find("ShopButton") as RectTransform ??
                                  parent.Find("PurchaseTicketButton") as RectTransform;
            Button shopButton;
            if (buttonTransform == null)
            {
                shopButton = CreateButton("ShopButton", parent, "상점");
                buttonTransform = shopButton.GetComponent<RectTransform>();
                buttonTransform.anchorMin = new Vector2(0.5f, 0.5f);
                buttonTransform.anchorMax = new Vector2(0.5f, 0.5f);
                buttonTransform.pivot = new Vector2(0.5f, 0.5f);
                buttonTransform.sizeDelta = new Vector2(280f, 72f);
                buttonTransform.anchoredPosition = new Vector2(0f, -220f);
                shopButton.GetComponent<Image>().color = new Color(0.03f, 0.32f, 0.46f, 0.92f);
            }
            else
            {
                buttonTransform.name = "ShopButton";
                shopButton = buttonTransform.GetComponent<Button>() ??
                             buttonTransform.gameObject.AddComponent<Button>();
            }

            var label = shopButton.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                label = CreateText("ShopButtonLabel", buttonTransform, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
                StretchFull(label.rectTransform, 12f);
            }

            label.name = "ShopButtonLabel";
            label.text = "상점";
            while (shopButton.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(shopButton.onClick, 0);
            }

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("_shopButton").objectReferenceValue = shopButton;
            serializedController.FindProperty("_shopButtonLabel").objectReferenceValue = label;
            serializedController.FindProperty("_shopSceneName").stringValue = "Shop_VSlice";
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(shopButton);
            EditorUtility.SetDirty(label);
            EditorSceneManager.MarkSceneDirty(startScene);
            EditorSceneManager.SaveScene(startScene);
            if (closeWhenFinished)
            {
                EditorSceneManager.CloseScene(startScene, true);
            }
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes;
                    return;
                }
            }

            var nextScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            for (var i = 0; i < scenes.Length; i++)
            {
                nextScenes[i] = scenes[i];
            }

            nextScenes[nextScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = nextScenes;
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.055f, 0.07f, 1f);
            camera.tag = "MainCamera";
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
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Button CreateButton(string name, RectTransform parent, string labelText)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();

            var label = CreateText("Label", rect, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.text = labelText;
            StretchFull(label.rectTransform, 10f);
            return button;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        private static Font ResolveFont()
        {
            try
            {
                var dynamicFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Arial Unicode MS" },
                    32);
                if (dynamicFont != null)
                {
                    return dynamicFont;
                }
            }
            catch
            {
                // Fall back to Unity's built-in font when no Korean OS font is available.
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void StretchFull(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
