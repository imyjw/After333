using System.IO;
using System.Linq;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Ads;
using Project333.Runtime.Presentation.Settings;
using Project333.Runtime.Presentation.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333ShopVisualBuilder
    {
        private const string ScenePath = "Assets/Shop_VSlice.unity";
        private const string SpritePath = "Assets/Project333/Resources/Project333/StartScene/PixelMenuButton.png";
        private const string GoldSpritePath = "Assets/Project333/Resources/Project333/UI/ServerGold.png";
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

        private static ShopSceneController FindController(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ShopSceneController>(true)).SingleOrDefault();

        private static void ApplyInitialLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath) || !File.Exists(SpritePath)) return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (FindController(scene)?.ShopVisualVersion >= 2) return;
            }
            else if (File.ReadAllText(ScenePath).Contains("_shopVisualVersion: 2")) return;
            UpdateShopGoldIcon();
        }

        [MenuItem("Tools/Project333/Shop/Update Gold Icon")]
        public static void UpdateShopGoldIcon()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var sprite = LoadGoldSprite();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            try
            {
                var controller = FindController(scene);
                if (controller == null) throw new System.InvalidOperationException("Shop controller was not found.");
                if (controller.ShopVisualVersion == 0)
                {
                    ApplyPixelShopLayout();
                }
                else
                {
                    // Upgrade only the icon; keep the user's panel, font and layout edits.
                    var serialized = new SerializedObject(controller);
                    var purchase = Ref<Button>(serialized, "_purchaseTicketButton");
                    var icon = purchase.transform.parent.Find("PriceGoldIcon")?.GetComponent<Image>();
                    if (icon == null) throw new System.InvalidOperationException("Shop price gold icon was not found.");
                    icon.sprite = sprite;
                    icon.color = Color.white;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    EditorUtility.SetDirty(icon);
                    serialized.FindProperty("_shopVisualVersion").intValue = 2;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (closeAfter || !wasDirty) EditorSceneManager.SaveScene(scene);
                Debug.Log("After333 shop gold icon updated to ServerGold. Other layout settings were preserved.");
            }
            finally
            {
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Sprite LoadGoldSprite()
        {
            AssetDatabase.ImportAsset(GoldSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(GoldSpritePath) as TextureImporter;
            if (importer == null) throw new System.InvalidOperationException("Shop ServerGold texture was not found.");
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(GoldSpritePath)
                ?? throw new System.InvalidOperationException("Shop ServerGold must import as a Sprite, not a Texture.");
        }

        [MenuItem("Tools/Project333/Shop/Apply Pixel Shop Layout")]
        public static void ApplyPixelShopLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var goldSprite = LoadGoldSprite();
            var sprite = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().FirstOrDefault()
                ?? Project333GameStartVisualBuilder.ImportButtonSprite();
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sprite == null || font == null)
                throw new System.InvalidOperationException("The shop button sprite and Korean font are required.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            try
            {
                var controller = FindController(scene);
                if (controller == null) throw new System.InvalidOperationException("Shop controller was not found.");
                var root = (RectTransform)controller.transform;
                var serialized = new SerializedObject(controller);
                var background = root.Find("Background")?.GetComponent<Image>();
                if (background != null) background.color = Ink;
                if (controller.GetComponentInParent<SafeAreaFitter>() == null)
                    controller.gameObject.AddComponent<SafeAreaFitter>().Configure(
                        background != null ? new[] { background.rectTransform } : new RectTransform[0]);
                GlobalSettingsMenuController.EnsureForScene(scene);

                var title = Ref<Text>(serialized, "_titleText");
                var header = (RectTransform)title.transform.parent;
                Region(header, 0f, 0.875f, 1f, 1f);
                Box(header, "HeaderRule", Gold, 0.025f, 0f, 0.975f, 0.015f);
                header.GetComponent<Image>().color = Panel;
                Region(title.rectTransform, 0.025f, 0.25f, 0.35f, 0.85f);
                Style(title, font, 44, Cream, TextAnchor.MiddleLeft);
                title.text = "상점";
                Label(header, "ShopSubtitle", "SHOP / AFTER333", font, 16, Muted,
                    0.028f, 0.07f, 0.35f, 0.30f, TextAnchor.MiddleLeft);
                var account = Ref<Text>(serialized, "_accountText");
                Region(account.rectTransform, 0.37f, 0.16f, 0.70f, 0.85f);
                Style(account, font, 23, Cream, TextAnchor.MiddleRight);
                var back = Ref<Button>(serialized, "_backButton");
                ButtonStyle(back, sprite, font, "시작 화면", 25);
                var backRect = (RectTransform)back.transform;
                backRect.anchorMin = backRect.anchorMax = new Vector2(1f, 0.5f);
                backRect.pivot = new Vector2(1f, 0.5f);
                backRect.sizeDelta = new Vector2(235f, 65f);
                backRect.anchoredPosition = new Vector2(-130f, 0f);
                Label(root, "ShopHeading", "다음 모험을 준비하세요", font, 30, Cream,
                    0.09f, 0.79f, 0.91f, 0.855f);

                var purchase = Ref<Button>(serialized, "_purchaseTicketButton");
                var ticketPanel = (RectTransform)purchase.transform.parent;
                var adPanel = root.Find("RewardedTicketProductPanel") as RectTransform;
                if (adPanel == null) throw new System.InvalidOperationException("Move the rewarded ad to Shop before styling it.");
                Region(ticketPanel, 0.09f, 0.12f, 0.48f, 0.77f);
                Region(adPanel, 0.52f, 0.12f, 0.91f, 0.77f);
                ProductPanel(ticketPanel, font, Gold, "골드 교환", "게임 티켓",
                    "골드를 사용해 티켓을 바로 구매합니다.", "ProductTitleText", "ProductDescriptionText");
                ProductPanel(adPanel, font, new Color(0.30f, 0.65f, 0.59f), "광고 보상", "게임 티켓",
                    "광고 시청 완료 후 서버 확인을 거쳐 지급됩니다.", "RewardedProductTitleText", "RewardedProductDescriptionText");
                var price = Label(ticketPanel, "TicketPriceText", "골드 3", font, 28, Cream,
                    0.33f, 0.30f, 0.67f, 0.375f);
                serialized.FindProperty("_ticketPriceText").objectReferenceValue = price;
                price.text = $"골드 {serialized.FindProperty("_ticketPurchaseGoldCost").intValue}";
                var coin = Box(ticketPanel, "PriceGoldIcon", Color.white, 0.27f, 0.315f, 0.33f, 0.365f);
                coin.sprite = goldSprite;
                coin.preserveAspect = true;
                Label(adPanel, "AdPriceText", "광고 시청", font, 28, Cream, 0.2f, 0.30f, 0.8f, 0.375f);
                ButtonStyle(purchase, sprite, font, "티켓 구매", 30);
                Region((RectTransform)purchase.transform, 0.19f, 0.145f, 0.81f, 0.285f);
                var status = Ref<Text>(serialized, "_statusText");
                status.transform.SetParent(ticketPanel, false);
                Region(status.rectTransform, 0.045f, 0.025f, 0.955f, 0.13f);
                Style(status, font, 21, Muted);

                var ads = controller.GetComponent<RewardedTicketController>();
                ads.ConfigureShopUi(adPanel);
                var adSerialized = new SerializedObject(ads);
                var adButton = Ref<Button>(adSerialized, "_watchAdButton");
                ButtonStyle(adButton, sprite, font, "광고 보고 티켓 +1", 30);
                Region((RectTransform)adButton.transform, 0.19f, 0.145f, 0.81f, 0.285f);
                var adStatus = Ref<Text>(adSerialized, "_rewardedAdStatusText");
                Region(adStatus.rectTransform, 0.045f, 0.025f, 0.955f, 0.13f);
                Style(adStatus, font, 21, Muted);
                Label(root, "ShopFooter", "구매와 광고 보상 결과는 서버 계정에 저장됩니다.", font, 19, Muted,
                    0.09f, 0.035f, 0.91f, 0.09f);

                serialized.FindProperty("_shopVisualVersion").intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ads);
                EditorSceneManager.MarkSceneDirty(scene);
                if (closeAfter || !wasDirty) EditorSceneManager.SaveScene(scene);
                Debug.Log("After333 pixel shop layout applied. Save the scene if it already had unsaved edits.");
            }
            finally
            {
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ProductPanel(RectTransform panel, Font font, Color accent, string category,
            string title, string description, string titleObject, string descriptionObject)
        {
            var image = panel.GetComponent<Image>();
            image.color = Panel;
            image.raycastTarget = false;
            var outline = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(3f, -3f);
            Box(panel, "TopAccent", accent, 0.035f, 0.98f, 0.965f, 0.987f);
            Label(panel, "ProductCategory", category, font, 21, accent, 0.05f, 0.91f, 0.95f, 0.97f);
            Label(panel, titleObject, title, font, 38, Cream, 0.05f, 0.79f, 0.95f, 0.90f);
            Label(panel, descriptionObject, description, font, 22, Muted, 0.08f, 0.38f, 0.92f, 0.455f);
            var ticket = Box(panel, "TicketIllustration", Gold, 0.24f, 0.49f, 0.76f, 0.76f).rectTransform;
            Box(ticket, "Paper", Cream, 0.02f, 0.045f, 0.98f, 0.955f);
            // Editable UI rectangles create a pixel ticket, without a new texture dependency.
            Box(ticket, "LeftNotch", Panel, 0f, 0.40f, 0.04f, 0.60f);
            Box(ticket, "RightNotch", Panel, 0.96f, 0.40f, 1f, 0.60f);
            for (var i = 0; i < 6; i++)
                Box(ticket, $"Perforation_{i:00}", Gold, 0.20f, 0.10f + i * 0.14f, 0.21f, 0.16f + i * 0.14f);
            Label(ticket, "TicketCaption", "GAME TICKET", font, 18, Ink, 0.26f, 0.66f, 0.90f, 0.88f);
            Label(ticket, "TicketAmount", "+1", font, 76, Ink, 0.26f, 0.12f, 0.90f, 0.65f);
        }

        private static T Ref<T>(SerializedObject serialized, string name) where T : Object
            => (T)serialized.FindProperty(name).objectReferenceValue;

        private static void Style(Text text, Font font, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = size;
        }

        private static void ButtonStyle(Button button, Sprite sprite, Font font, string value, int size)
        {
            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.83f);
            colors.pressedColor = new Color(0.7f, 0.83f, 0.78f);
            colors.disabledColor = new Color(0.45f, 0.48f, 0.47f, 0.7f);
            button.colors = colors;
            var label = button.GetComponentInChildren<Text>(true);
            Region(label.rectTransform, 0.07f, 0.12f, 0.93f, 0.88f);
            Style(label, font, size, Cream);
            label.text = value;
        }

        private static Text Label(RectTransform parent, string name, string value, Font font, int size, Color color,
            float x0, float y0, float x1, float y1, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var text = parent.Find(name)?.GetComponent<Text>();
            if (text == null)
            {
                text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                text.transform.SetParent(parent, false);
            }
            Region(text.rectTransform, x0, y0, x1, y1);
            Style(text, font, size, color, anchor);
            text.text = value;
            return text;
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

        private static void Region(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
