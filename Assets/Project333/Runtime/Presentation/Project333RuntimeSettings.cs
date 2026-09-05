using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Project333.Runtime.Presentation.Settings;

namespace Project333.Runtime.Presentation
{
    public static class Project333RuntimeSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            UnityEngine.Application.runInBackground = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyToInitiallyLoadedScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                HandleSceneLoaded(activeScene, LoadSceneMode.Single);
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            switch (scene.name)
            {
                case "GameStart_VSlice":
                    ConfigureNamedRoot(scene, "GameStartSceneController");
                    break;
                case "Draft_VSlice":
                case "DeckBuilding_VSlice":
                    ConfigureNamedRoot(scene, "DraftSceneController");
                    break;
                case "OwnedCards_VSlice":
                    ConfigureNamedRoot(scene, "OwnedCardsSceneController");
                    break;
                case "Shop_VSlice":
                    ConfigureNamedRoot(scene, "ShopSceneController");
                    break;
                case "Battle_VSlice":
                    ConfigureBattleCanvas(scene);
                    break;
            }

            GlobalSettingsMenuController.EnsureForScene(scene);
        }

        private static void ConfigureNamedRoot(Scene scene, string rootName)
        {
            var contentRoot = FindRectTransform(scene, rootName);
            if (contentRoot == null)
            {
                return;
            }

            var background = FindDirectChild(contentRoot, "Background");
            if (!contentRoot.TryGetComponent<SafeAreaFitter>(out var fitter))
            {
                fitter = contentRoot.gameObject.AddComponent<SafeAreaFitter>();
            }

            fitter.Configure(background);
        }

        private static void ConfigureBattleCanvas(Scene scene)
        {
            var sceneRoots = scene.GetRootGameObjects();
            for (var i = 0; i < sceneRoots.Length; i++)
            {
                var scalers = sceneRoots[i].GetComponentsInChildren<CanvasScaler>(true);
                for (var j = 0; j < scalers.Length; j++)
                {
                    var scaler = scalers[j];
                    var canvas = scaler.GetComponent<Canvas>();
                    if (canvas == null ||
                        canvas.name == GlobalSettingsMenuController.RootObjectName ||
                        !canvas.isRootCanvas ||
                        scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    {
                        continue;
                    }

                    SafeAreaFitter.EnsureCanvasContentRoot(canvas.transform as RectTransform);
                    return;
                }
            }
        }

        private static RectTransform FindRectTransform(Scene scene, string objectName)
        {
            var sceneRoots = scene.GetRootGameObjects();
            for (var i = 0; i < sceneRoots.Length; i++)
            {
                var rectTransforms = sceneRoots[i].GetComponentsInChildren<RectTransform>(true);
                for (var j = 0; j < rectTransforms.Length; j++)
                {
                    if (rectTransforms[j].name == objectName)
                    {
                        return rectTransforms[j];
                    }
                }
            }

            return null;
        }

        private static RectTransform FindDirectChild(RectTransform parent, string childName)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == childName)
                {
                    return parent.GetChild(i) as RectTransform;
                }
            }

            return null;
        }
    }
}
