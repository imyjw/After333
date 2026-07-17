using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333OpponentHandBuilder
    {
        private const string BattleScenePath = "Assets/Battle_VSlice.unity";
        private const string CardBackAssetPath =
            "Assets/Project333/Resources/Project333/CardArtwork/OpponentCardBack.png";
        private const string OpponentHandObjectName = "OpponentHandCanvas";

        [MenuItem("Tools/Project333/Battle/Create Or Update Opponent Hand")]
        public static void CreateOrUpdateOpponentHand()
        {
            var scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            var battleScreenPresenter = FindSceneComponent<BattleScreenPresenter>(scene);
            if (battleScreenPresenter == null)
            {
                throw new MissingComponentException("Battle_VSlice does not contain a BattleScreenPresenter.");
            }

            ConfigureCardBackImporter();
            var opponentHandPresenter = FindSceneComponent<OpponentHandPresenter>(scene);
            var opponentHandObject = opponentHandPresenter == null
                ? null
                : opponentHandPresenter.gameObject;
            if (opponentHandObject == null)
            {
                opponentHandObject = new GameObject(
                    OpponentHandObjectName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(OpponentHandPresenter));
                Undo.RegisterCreatedObjectUndo(opponentHandObject, "Create Opponent Hand UI");
                SceneManager.MoveGameObjectToScene(opponentHandObject, scene);

                var rectTransform = opponentHandObject.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;

                var createdCanvas = opponentHandObject.GetComponent<Canvas>();
                createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                createdCanvas.overrideSorting = true;
                createdCanvas.sortingOrder = 45;

                var scaler = opponentHandObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;

                opponentHandPresenter = opponentHandObject.GetComponent<OpponentHandPresenter>();
            }

            var canvas = opponentHandObject.GetComponent<Canvas>();
            var presenterSerializedObject = new SerializedObject(opponentHandPresenter);
            var canvasProperty = presenterSerializedObject.FindProperty("_canvas");
            if (canvasProperty.objectReferenceValue == null)
            {
                canvasProperty.objectReferenceValue = canvas;
            }

            var cardBackSpriteProperty = presenterSerializedObject.FindProperty("_cardBackSprite");
            if (cardBackSpriteProperty.objectReferenceValue == null)
            {
                cardBackSpriteProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(CardBackAssetPath);
            }

            var cardBackTextureProperty = presenterSerializedObject.FindProperty("_cardBackTexture");
            if (cardBackTextureProperty.objectReferenceValue == null)
            {
                cardBackTextureProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(CardBackAssetPath);
            }

            presenterSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            opponentHandPresenter.EnsureEditableCardBackSlots();

            var screenSerializedObject = new SerializedObject(battleScreenPresenter);
            screenSerializedObject.FindProperty("_opponentHandPresenter").objectReferenceValue = opponentHandPresenter;
            screenSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(opponentHandObject, LayerMask.NameToLayer("UI"));
            EditorUtility.SetDirty(opponentHandPresenter);
            EditorUtility.SetDirty(battleScreenPresenter);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BattleScenePath);
            Selection.activeGameObject = opponentHandObject;
        }

        private static void ConfigureCardBackImporter()
        {
            if (!(AssetImporter.GetAtPath(CardBackAssetPath) is TextureImporter importer))
            {
                throw new MissingReferenceException($"Opponent card back image was not found at '{CardBackAssetPath}'.");
            }

            var requiresReimport = importer.textureType != TextureImporterType.Sprite ||
                                   importer.spriteImportMode != SpriteImportMode.Single ||
                                   importer.mipmapEnabled;
            if (!requiresReimport)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
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

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null || layer < 0)
            {
                return;
            }

            target.layer = layer;
            for (var i = 0; i < target.transform.childCount; i++)
            {
                SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
            }
        }
    }
}
