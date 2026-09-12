using System;
using System.IO;
using System.Linq;
using Project333.Runtime.Presentation;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.OwnedCards;
using Project333.Runtime.Presentation.Shop;
using Project333.Runtime.Presentation.Startup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333AccountWalletBuilder
    {
        private const string ViewGuid = "9daf78aa7f1b4285b081e82ee12cd233";
        private const string HeaderLayoutGuid = "d40f1d8456a84669a7390529d3e732ac";
        private static readonly string[] ScenePaths =
        {
            "Assets/GameStart_VSlice.unity",
            "Assets/DeckBuilding_VSlice.unity",
            "Assets/Draft_VSlice.unity",
            "Assets/Shop_VSlice.unity",
            "Assets/OwnedCards_VSlice.unity"
        };

        [InitializeOnLoadMethod]
        private static void QueueMigration()
        {
            if (!UnityEngine.Application.isBatchMode)
                EditorApplication.delayCall += ApplyMissingLayouts;
        }

        private static void ApplyMissingLayouts()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var missing = ScenePaths.Where(File.Exists).Where(path => !HasSavedOrLoadedWallet(path)).ToArray();
            if (missing.Length == 0) return;
            ImportIcons();
            foreach (var path in missing) ApplyScene(path);
        }

        private static bool HasSavedOrLoadedWallet(string path)
        {
            var needsHeader = path == "Assets/Shop_VSlice.unity" || path == "Assets/OwnedCards_VSlice.unity";
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded)
                return scene.GetRootGameObjects()
                    .Any(root => root.GetComponentsInChildren<AccountWalletView>(true).Length > 0 &&
                        (!needsHeader || (root.GetComponentsInChildren<AccountWalletHeaderLayout>(true).Length > 0 &&
                            root.GetComponentsInChildren<AccountWalletView>(true)
                                .All(view => view.transform.Find("AccountNameText") == null))));
            var content = File.ReadAllText(path);
            return content.Contains(ViewGuid) && (!needsHeader ||
                (content.Contains(HeaderLayoutGuid) && !content.Contains("m_Name: AccountNameText")));
        }

        [MenuItem("Tools/Project333/UI/Align Shop And Collection Wallets")]
        public static void AlignHeaderWallets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before editing header wallets.");
            ApplyScene("Assets/Shop_VSlice.unity");
            ApplyScene("Assets/OwnedCards_VSlice.unity");
        }

        [MenuItem("Tools/Project333/UI/Apply Account Wallet Icons")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying wallet icons.");
            ImportIcons();
            foreach (var path in ScenePaths)
            {
                if (!File.Exists(path)) continue;
                ApplyScene(path);
            }
            Debug.Log("After333 account wallet icons applied to all five scenes. Existing panel positions were preserved.");
        }

        private static void ImportIcons()
        {
            foreach (var name in new[] { "ServerTicketIcon128", "ServerGoldIcon128", "ServerTicketIcon64", "ServerGoldIcon64" })
            {
                var path = "Assets/Project333/Resources/Project333/UI/" + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Wallet icon missing: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 128;
                importer.SaveAndReimport();
            }
        }

        private static void ApplyScene(string path)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            try
            {
                var components = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                MonoBehaviour controller = components.FirstOrDefault(component =>
                    component is GameStartSceneController || component is DraftSceneController ||
                    component is ShopSceneController || component is OwnedCardsSceneController);
                if (controller == null) throw new InvalidOperationException("Wallet controller missing: " + path);
                var field = controller is GameStartSceneController ? "_ticketText" :
                    controller is DraftSceneController ? "_serverWalletText" : "_accountText";
                var serialized = new SerializedObject(controller);
                var host = serialized.FindProperty(field)?.objectReferenceValue as Text;
                if (host == null) throw new InvalidOperationException("Wallet Text missing: " + path);
                var hasHeader = controller is ShopSceneController || controller is OwnedCardsSceneController;
                var view = AccountWalletView.Attach(host, controller is DraftSceneController, false);
                if (hasHeader)
                {
                    var nameText = host.transform.Find("AccountNameText");
                    if (nameText != null) UnityEngine.Object.DestroyImmediate(nameText.gameObject);
                    var back = serialized.FindProperty("_backButton").objectReferenceValue as Button;
                    var title = serialized.FindProperty("_titleText").objectReferenceValue as Text;
                    var layout = host.GetComponent<AccountWalletHeaderLayout>() ??
                        host.gameObject.AddComponent<AccountWalletHeaderLayout>();
                    layout.Configure(back != null ? (RectTransform)back.transform : null,
                        title != null ? title.rectTransform : null);
                    EditorUtility.SetDirty(layout);
                }
                view.Render(false, 0, 0);
                EditorUtility.SetDirty(view);
                EditorUtility.SetDirty(host);
                EditorSceneManager.MarkSceneDirty(scene);
                // Do not save unrelated, unsaved edits from an already open scene.
                if (closeAfter || !wasDirty) EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
