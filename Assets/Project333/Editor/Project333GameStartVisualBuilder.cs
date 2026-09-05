using System;
using System.IO;
using System.Linq;
using Project333.Runtime.Presentation.Settings;
using Project333.Runtime.Presentation.Startup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Editor
{
    public static class Project333GameStartVisualBuilder
    {
        private const string ScenePath = "Assets/GameStart_VSlice.unity";
        private const string SpritePath = "Assets/Project333/Resources/Project333/StartScene/PixelMenuButton.png";
        private const string FontPath = "Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static/NotoSansKR-Bold.ttf";

        [InitializeOnLoadMethod]
        private static void QueueInitialLayout()
        {
            EditorApplication.delayCall += ApplyInitialLayout;
        }

        private static void ApplyInitialLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SpritePath) || !File.Exists(ScenePath))
                return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                var controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameStartSceneController>(true)).FirstOrDefault();
                if (controller != null && controller.PixelMenuLayoutVersion >= 2) return;
            }
            else if (File.ReadAllText(ScenePath).Contains("_pixelMenuLayoutVersion: 2")) return;
            ApplyReconnectPixelLayout();
        }

        [MenuItem("Tools/Project333/Start/Match Reconnect Button To Pixel Menu")]
        public static void ApplyReconnectPixelLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            var controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameStartSceneController>(true)).Single();
            if (controller.PixelMenuLayoutVersion < 1)
            {
                ApplyPixelMenuLayout();
            }
            else
            {
                var serialized = new SerializedObject(controller);
                PositionReconnectBelowCollection(serialized);
                serialized.FindProperty("_pixelMenuLayoutVersion").intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (closeAfter || !wasDirty) EditorSceneManager.SaveScene(scene);
            if (closeAfter) EditorSceneManager.CloseScene(scene, true);
        }

        [MenuItem("Tools/Project333/Start/Apply Pixel Menu Layout")]
        public static void ApplyPixelMenuLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before editing the start menu.");

            var sprite = ImportButtonSprite();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;

            var controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameStartSceneController>(true)).Single();
            var serialized = new SerializedObject(controller);
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var center = (RectTransform)controller.transform.Find("CenterPanel");
            // Position the stack below the background logo. All values are canvas units.
            Place(center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(680f, 420f), new Vector2(0f, 90f));
            center.GetComponent<Image>().raycastTarget = false;

            var buttons = new[] { "_startGameButton", "_shopButton", "_ownedCardsButton" }
                .Select(name => (Button)serialized.FindProperty(name).objectReferenceValue).ToArray();
            var labels = new[] { "게임 시작", "상점", "보유 카드" };
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                button.transform.SetParent(center, false);
                Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(420f, 114f), new Vector2(0f, -i * 132f));
                StyleButton(button, sprite, font);
                button.GetComponentInChildren<Text>(true).text = labels[i];
            }

            serialized.FindProperty("_startButtonSprite").objectReferenceValue = sprite;
            serialized.FindProperty("_applyOwnedCardsButtonLayout").boolValue = false;
            serialized.FindProperty("_ownedCardsButtonColor").colorValue = Color.white;
            serialized.FindProperty("_shopButtonColor").colorValue = Color.white;

            PositionReconnectBelowCollection(serialized, true);

            var status = (Text)serialized.FindProperty("_statusText").objectReferenceValue;
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(900f, 70f), new Vector2(0f, 12f));
            status.font = font;
            status.fontSize = 20;
            status.alignment = TextAnchor.UpperCenter;
            status.raycastTarget = false;

            var menu = GlobalSettingsMenuController.EnsureForScene(scene);
            var safeRoot = menu.transform.Find("SafeAreaRoot");
            var walletObject = safeRoot.Find("WalletPanel")?.gameObject ??
                new GameObject("WalletPanel", typeof(RectTransform), typeof(Image));
            var wallet = (RectTransform)walletObject.transform;
            wallet.SetParent(safeRoot, false);
            wallet.SetAsFirstSibling();
            Place(wallet, Vector2.one, Vector2.one, new Vector2(560f, 86f), new Vector2(-126f, -24f));
            var walletImage = wallet.GetComponent<Image>();
            walletImage.color = new Color(0.018f, 0.065f, 0.08f, 0.94f);
            walletImage.raycastTarget = false;
            var outline = wallet.GetComponent<Outline>() ?? wallet.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.43f, 0.12f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var ticket = (Text)serialized.FindProperty("_ticketText").objectReferenceValue;
            ticket.transform.SetParent(wallet, false);
            Stretch(ticket.rectTransform, new Vector2(20f, 10f));
            ticket.font = font;
            ticket.fontSize = 25;
            ticket.resizeTextForBestFit = true;
            ticket.resizeTextMinSize = 17;
            ticket.resizeTextMaxSize = 25;
            ticket.alignment = TextAnchor.MiddleCenter;
            ticket.color = new Color(1f, 0.89f, 0.59f, 1f);
            ticket.horizontalOverflow = HorizontalWrapMode.Overflow;
            ticket.verticalOverflow = VerticalWrapMode.Truncate;
            ticket.raycastTarget = false;
            ticket.text = "Server Tickets: -     GOLD: -";

            serialized.FindProperty("_pixelMenuLayoutVersion").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            // An open scene may contain the user's unsaved changes. Only save scenes we opened.
            if (closeAfter || !wasDirty)
            {
                EditorSceneManager.SaveScene(scene);
            }
            if (closeAfter)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("After333 pixel menu applied: four matching buttons and wallet to the left of settings.");
        }

        private static void PositionReconnectBelowCollection(SerializedObject serialized, bool resetStack = false)
        {
            var owned = (Button)serialized.FindProperty("_ownedCardsButton").objectReferenceValue;
            var shop = (Button)serialized.FindProperty("_shopButton").objectReferenceValue;
            var reconnect = (Button)serialized.FindProperty("_reconnectPvpButton").objectReferenceValue;
            if (owned == null || reconnect == null)
                throw new InvalidOperationException("Owned Cards and PvP Reconnect buttons must exist.");

            var ownedRect = (RectTransform)owned.transform;
            var rect = (RectTransform)reconnect.transform;
            var stride = shop != null
                ? Mathf.Abs(ownedRect.anchoredPosition.y - ((RectTransform)shop.transform).anchoredPosition.y)
                : ownedRect.rect.height + 18f;
            stride = Mathf.Max(ownedRect.rect.height + 18f, stride);

            // Reserve a fourth slot once so the reconnect button does not overlap the footer.
            if (resetStack || serialized.FindProperty("_pixelMenuLayoutVersion").intValue < 2)
            {
                var parent = (RectTransform)ownedRect.parent;
                parent.sizeDelta += new Vector2(0f, stride);
            }
            rect.SetParent(ownedRect.parent, false);
            rect.anchorMin = ownedRect.anchorMin;
            rect.anchorMax = ownedRect.anchorMax;
            rect.pivot = ownedRect.pivot;
            rect.sizeDelta = ownedRect.sizeDelta;
            rect.anchoredPosition = ownedRect.anchoredPosition + Vector2.down * stride;
            rect.localScale = ownedRect.localScale;
            rect.SetSiblingIndex(ownedRect.GetSiblingIndex() + 1);

            var sourceImage = owned.GetComponent<Image>();
            var image = reconnect.GetComponent<Image>();
            image.sprite = sourceImage.sprite;
            image.color = sourceImage.color;
            image.type = sourceImage.type;
            image.preserveAspect = sourceImage.preserveAspect;
            image.material = sourceImage.material;
            reconnect.targetGraphic = image;
            reconnect.transition = owned.transition;
            reconnect.colors = owned.colors;
            reconnect.spriteState = owned.spriteState;

            var sourceLabel = owned.GetComponentInChildren<Text>(true);
            var label = reconnect.GetComponentInChildren<Text>(true);
            Stretch(label.rectTransform, new Vector2(40f, 14f));
            label.font = sourceLabel.font;
            label.fontSize = sourceLabel.fontSize;
            label.fontStyle = sourceLabel.fontStyle;
            label.color = sourceLabel.color;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = sourceLabel.fontSize;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.text = "PVP 전투 재접속";
            var sourceShadow = sourceLabel.GetComponent<Shadow>();
            if (sourceShadow != null)
            {
                var shadow = label.GetComponent<Shadow>() ?? label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = sourceShadow.effectColor;
                shadow.effectDistance = sourceShadow.effectDistance;
            }
            serialized.FindProperty("_applyReconnectPvpButtonLayout").boolValue = false;
        }

        private static void StyleButton(Button button, Sprite sprite, Font font)
        {
            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.92f, 0.72f, 1f),
                selectedColor = Color.white,
                pressedColor = new Color(0.65f, 0.78f, 0.8f, 1f),
                disabledColor = new Color(0.45f, 0.5f, 0.52f, 0.85f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            var label = button.GetComponentInChildren<Text>(true);
            Stretch(label.rectTransform, new Vector2(40f, 14f));
            label.font = font;
            label.fontSize = 34;
            label.fontStyle = FontStyle.Normal;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 21;
            label.resizeTextMaxSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.96f, 0.82f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            var shadow = label.GetComponent<Shadow>() ?? label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.025f, 0.04f, 1f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        internal static Sprite ImportButtonSprite()
        {
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(SpritePath));
            var pixels = texture.GetPixels32();
            var left = texture.width;
            var right = 0;
            var bottom = texture.height;
            var top = 0;
            // Ignore isolated transparent-edge noise when slicing the generated button.
            for (var y = 0; y < texture.height; y++)
            {
                var count = 0;
                for (var x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 127) count++;
                if (count < texture.width / 5) continue;
                bottom = Math.Min(bottom, y);
                top = y;
            }
            for (var x = 0; x < texture.width; x++)
            {
                var count = 0;
                for (var y = bottom; y <= top; y++)
                    if (pixels[y * texture.width + x].a > 127) count++;
                if (count < (top - bottom) / 5) continue;
                left = Math.Min(left, x);
                right = x;
            }
            if (right <= left || top <= bottom)
                throw new InvalidOperationException("Generated button has no visible sprite region.");
            Object.DestroyImmediate(texture);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
#pragma warning disable 618
            importer.spritesheet = new[]
            {
                new SpriteMetaData
                {
                    name = "PixelMenuButton",
                    rect = new Rect(left, bottom, right - left + 1, top - bottom + 1),
                    alignment = 0,
                    pivot = new Vector2(0.5f, 0.5f)
                }
            };
#pragma warning restore 618
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().Single();
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, Vector2 padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = padding;
            rect.offsetMax = -padding;
            rect.localScale = Vector3.one;
        }
    }
}
