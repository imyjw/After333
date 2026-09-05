using Project333.Runtime.Presentation.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project333.Editor
{
    [InitializeOnLoad]
    public static class Project333GlobalSettingsMenuBuilder
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/GameStart_VSlice.unity",
            "Assets/Draft_VSlice.unity",
            "Assets/DeckBuilding_VSlice.unity",
            "Assets/Battle_VSlice.unity",
            "Assets/OwnedCards_VSlice.unity"
        };

        private static bool s_isMaterializing;

        static Project333GlobalSettingsMenuBuilder()
        {
            EditorSceneManager.sceneOpened -= HandleSceneOpened;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
            EditorApplication.delayCall += MaterializeLoadedSupportedScenes;
        }

        [MenuItem("Tools/Project333/UI/Materialize Settings Menu In All Scenes")]
        public static void MaterializeAllSettingsMenus()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before materializing After333 settings menus.");
                return;
            }

            s_isMaterializing = true;
            GlobalSettingsMenuController lastController = null;
            try
            {
                for (var i = 0; i < ScenePaths.Length; i++)
                {
                    var path = ScenePaths[i];
                    var scene = SceneManager.GetSceneByPath(path);
                    var openedForBuild = !scene.IsValid() || !scene.isLoaded;
                    if (openedForBuild)
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    }

                    lastController = GlobalSettingsMenuController.EnsureForScene(scene);
                    EditorSceneManager.MarkSceneDirty(scene);

                    if (openedForBuild)
                    {
                        EditorSceneManager.SaveScene(scene, path);
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            finally
            {
                s_isMaterializing = false;
            }

            if (lastController != null && lastController.gameObject.scene.isLoaded)
            {
                Selection.activeGameObject = lastController.gameObject;
            }

            Debug.Log("After333 settings menus were materialized in all build scenes.");
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (GlobalSettingsMenuController.IsSupportedScene(scene.name))
            {
                EditorApplication.delayCall += MaterializeLoadedSupportedScenes;
            }
        }

        private static void MaterializeLoadedSupportedScenes()
        {
            if (s_isMaterializing || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            s_isMaterializing = true;
            try
            {
                for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                {
                    var scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.IsValid() ||
                        !scene.isLoaded ||
                        !GlobalSettingsMenuController.IsSupportedScene(scene.name) ||
                        FindSceneComponent<GlobalSettingsMenuController>(scene) != null)
                    {
                        continue;
                    }

                    GlobalSettingsMenuController.EnsureForScene(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
            finally
            {
                s_isMaterializing = false;
            }
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            var components = Resources.FindObjectsOfTypeAll<T>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject.scene == scene)
                {
                    return components[i];
                }
            }

            return null;
        }
    }
}
