using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project333.Editor
{
    [InitializeOnLoad]
    public static class Project333BattlePersistentUiBuilder
    {
        private const string BattleScenePath = "Assets/Battle_VSlice.unity";
        private static bool s_isMaterializing;

        static Project333BattlePersistentUiBuilder()
        {
            EditorSceneManager.sceneOpened -= HandleSceneOpened;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
            EditorApplication.delayCall += MaterializeLoadedBattleScene;
        }

        [MenuItem("Tools/Project333/Battle/Materialize Missing Persistent UI")]
        public static void MaterializeMissingPersistentUi()
        {
            var scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            MaterializeScene(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BattleScenePath);
            var presenter = FindSceneComponent<BattleScreenPresenter>(scene);
            Selection.activeGameObject = presenter == null ? null : presenter.gameObject;
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == BattleScenePath)
            {
                EditorApplication.delayCall += MaterializeLoadedBattleScene;
            }
        }

        private static void MaterializeLoadedBattleScene()
        {
            if (s_isMaterializing || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.IsValid() && scene.isLoaded && scene.path == BattleScenePath)
                {
                    MaterializeScene(scene);
                }
            }
        }

        private static void MaterializeScene(Scene scene)
        {
            if (s_isMaterializing ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                HasPersistentBattleUi(scene))
            {
                return;
            }

            s_isMaterializing = true;
            try
            {
                var battleScreenPresenter = FindSceneComponent<BattleScreenPresenter>(scene);
                var handPresenter = FindSceneComponent<HandPresenter>(scene);
                var battleBootstrapper = FindSceneComponent<BattleBootstrapper>(scene);
                var battlePlayerInputController = FindSceneComponent<BattlePlayerInputController>(scene);

                battleScreenPresenter?.MaterializePersistentBattleUiForEditor();
                handPresenter?.MaterializePersistentHandUiForEditor();
                battleBootstrapper?.MaterializePersistentBattleUiForEditor();
                battlePlayerInputController?.MaterializeRobotFusionSelectionUiForEditor();

                EditorSceneManager.MarkSceneDirty(scene);
            }
            finally
            {
                s_isMaterializing = false;
            }
        }

        private static bool HasPersistentBattleUi(Scene scene)
        {
            return FindSceneGameObject(scene, "OpponentHandCanvas") != null &&
                   FindSceneGameObject(scene, "OpponentDeckCanvas") != null &&
                   FindSceneGameObject(scene, "PlayerDeckCanvas") != null &&
                   FindSceneGameObject(scene, "RuntimeHandCanvas") != null &&
                   FindSceneGameObject(scene, "OpponentPlayedCardRevealCanvas") != null &&
                   FindSceneGameObject(scene, "OnlineErrorToastCanvas") != null &&
                   FindSceneGameObject(scene, "OnlineTurnTimerCanvas") != null &&
                   FindSceneGameObject(scene, "PvpMatchmakingOverlayCanvas") != null &&
                   FindSceneGameObject(scene, "BattleSceneBackgroundCanvas") != null &&
                   FindSceneGameObject(scene, "RobotFusionSelectionCanvas") != null;
        }

        private static GameObject FindSceneGameObject(Scene scene, string objectName)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var found = FindDescendant(rootObject.transform, objectName);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform current, string objectName)
        {
            if (current.name == objectName)
            {
                return current;
            }

            for (var childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                var found = FindDescendant(current.GetChild(childIndex), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (var component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (component != null && component.gameObject.scene == scene)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
