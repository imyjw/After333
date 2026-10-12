using System;
using System.Linq;
using Project333.Runtime.Presentation.Tutorial;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Project333.Editor
{
    public static class Project333TutorialSceneBuilder
    {
        public const string ScenePath = "Assets/Tutorial_VSlice.unity";
        [MenuItem("Tools/Project333/Tutorial/Create First Tutorial Scene")]
        public static void CreateTutorialScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Tutorial scene already exists; preserve it and edit it directly.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            scene.name = TutorialSceneController.SceneName;
            var camera = new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f,.04f,.065f,1); camera.orthographic = true;
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var controller = new GameObject("TutorialSceneController",typeof(TutorialSceneController)).GetComponent<TutorialSceneController>();
            var idle = LoadFrames("Assets/Unit/GaebangChief/Idle.png");
            var attack = LoadFrames("Assets/Unit/GaebangChief/Attack.png");
            if (idle.Length == 0) throw new InvalidOperationException("GaebangChief Idle frames are required.");
            controller.Configure(idle,attack.Length > 2 ? attack[2] : null);
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");
            if (catalog == null) throw new InvalidOperationException("Tutorial practice card catalog is required.");
            controller.ConfigurePracticeCatalog(catalog);
            if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new InvalidOperationException("Could not save tutorial scene.");
            var existing = EditorBuildSettings.scenes;
            if (!existing.Any(x => x.path == ScenePath))
                EditorBuildSettings.scenes = existing.Concat(new[] { new EditorBuildSettingsScene(ScenePath,true) }).ToArray();
            AssetDatabase.SaveAssets();
        }
        private static Sprite[] LoadFrames(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(x=>FrameIndex(x.name)).ThenBy(x=>x.name,StringComparer.Ordinal).ToArray();
        }
        private static int FrameIndex(string name)
        {
            return int.TryParse(name.Substring(name.LastIndexOf('_')+1),out var index) ? index : int.MaxValue;
        }
    }
}
