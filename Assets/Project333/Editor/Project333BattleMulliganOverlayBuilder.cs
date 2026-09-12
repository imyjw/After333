using System.Collections.Generic;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Hand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project333.Editor
{
    public static class Project333BattleMulliganOverlayBuilder
    {
        private const string BattleScenePath = "Assets/Battle_VSlice.unity";
        private const string OverlayName = "MulliganOverlayCanvas";
        private const string LegacyButtonName = "PassMulliganButton";
        private const int CardSlotCount = 3;

        private static readonly Color DimColor = new Color(0.015f, 0.025f, 0.045f, 0.92f);
        private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.96f);
        private static readonly Color CardBackingColor = new Color(0.09f, 0.11f, 0.16f, 1f);
        private static readonly Color AccentColor = new Color(0.94f, 0.76f, 0.32f, 1f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.33f, 0.56f, 1f);
        private static readonly Color SelectionColor = new Color(0.66f, 0.08f, 0.06f, 0.48f);

        [MenuItem("Tools/Project333/Battle/Create Or Update Mulligan Overlay")]
        public static void CreateOrUpdateMulliganOverlay()
        {
            var scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            var battleScreenPresenter = FindSceneComponent<BattleScreenPresenter>(scene);
            if (battleScreenPresenter == null)
            {
                throw new MissingComponentException("Battle_VSlice does not contain a BattleScreenPresenter.");
            }

            DestroySceneObjectByName(scene, OverlayName);
            DestroySceneObjectByName(scene, LegacyButtonName);

            var overlayObject = new GameObject(
                OverlayName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(BattleMulliganOverlayPresenter));
            SceneManager.MoveGameObjectToScene(overlayObject, scene);

            var overlayRect = overlayObject.GetComponent<RectTransform>();
            StretchFull(overlayRect);

            var canvas = overlayObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 260;

            var scaler = overlayObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var dimmer = CreateImage("Dimmer", overlayRect, DimColor);
            StretchFull(dimmer.rectTransform);
            dimmer.raycastTarget = true;

            var centerPanel = CreateImage("CenterPanel", overlayRect, PanelColor);
            var centerPanelRect = centerPanel.rectTransform;
            centerPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerPanelRect.pivot = new Vector2(0.5f, 0.5f);
            centerPanelRect.sizeDelta = new Vector2(1740f, 930f);
            centerPanelRect.anchoredPosition = Vector2.zero;
            centerPanel.raycastTarget = false;

            var titleText = CreateText("TitleText", centerPanelRect, 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            titleText.text = "시작 카드를 선택하세요";
            titleText.color = AccentColor;
            SetAnchoredRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(1320f, 110f));

            var statusText = CreateText("StatusText", centerPanelRect, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            statusText.text = "교체하지 않으려면 바로 확정하세요.";
            SetAnchoredRect(statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(1420f, 56f));

            var cardRowObject = new GameObject("CardRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var cardRowRect = cardRowObject.GetComponent<RectTransform>();
            cardRowRect.SetParent(centerPanelRect, false);
            SetAnchoredRect(cardRowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1560f, 570f));

            var cardRowLayout = cardRowObject.GetComponent<HorizontalLayoutGroup>();
            cardRowLayout.padding = new RectOffset(12, 12, 16, 16);
            cardRowLayout.spacing = 34f;
            cardRowLayout.childAlignment = TextAnchor.MiddleCenter;
            cardRowLayout.childControlWidth = false;
            cardRowLayout.childControlHeight = false;
            cardRowLayout.childForceExpandWidth = false;
            cardRowLayout.childForceExpandHeight = false;

            var cardButtons = new List<Button>(CardSlotCount);
            var artworkImages = new List<Image>(CardSlotCount);
            var selectionOverlays = new List<Image>(CardSlotCount);
            var fallbackTexts = new List<Text>(CardSlotCount);
            var attackValueTexts = new List<Text>(CardSlotCount);
            var hpValueTexts = new List<Text>(CardSlotCount);

            for (var slotIndex = 0; slotIndex < CardSlotCount; slotIndex++)
            {
                CreateCardSlot(
                    cardRowRect,
                    slotIndex,
                    cardButtons,
                    artworkImages,
                    selectionOverlays,
                    fallbackTexts,
                    attackValueTexts,
                    hpValueTexts);
            }

            var confirmButton = CreateButton("ConfirmMulliganButton", centerPanelRect, ButtonColor);
            SetAnchoredRect(
                confirmButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 70f),
                new Vector2(360f, 88f));

            var confirmButtonText = CreateText(
                "Label",
                confirmButton.GetComponent<RectTransform>(),
                32,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            confirmButtonText.text = "교체";
            StretchFull(confirmButtonText.rectTransform, 10f);

            var overlayPresenter = overlayObject.GetComponent<BattleMulliganOverlayPresenter>();
            AssignOverlayReferences(
                overlayPresenter,
                canvas,
                overlayObject.GetComponent<CanvasGroup>(),
                titleText,
                statusText,
                confirmButton,
                confirmButtonText,
                cardButtons,
                artworkImages,
                selectionOverlays,
                fallbackTexts,
                attackValueTexts,
                hpValueTexts);

            var screenSerializedObject = new SerializedObject(battleScreenPresenter);
            screenSerializedObject.FindProperty("_mulliganOverlayPresenter").objectReferenceValue = overlayPresenter;
            screenSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(battleScreenPresenter);

            SetLayerRecursively(overlayObject, LayerMask.NameToLayer("UI"));
            overlayObject.SetActive(false);
            EditorUtility.SetDirty(overlayPresenter);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BattleScenePath);
            Selection.activeGameObject = overlayObject;
        }

        private static void CreateCardSlot(
            RectTransform parent,
            int slotIndex,
            ICollection<Button> cardButtons,
            ICollection<Image> artworkImages,
            ICollection<Image> selectionOverlays,
            ICollection<Text> fallbackTexts,
            ICollection<Text> attackValueTexts,
            ICollection<Text> hpValueTexts)
        {
            var cardObject = new GameObject(
                $"MulliganCard_{slotIndex:00}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            var cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.SetParent(parent, false);
            cardRect.sizeDelta = new Vector2(300f, 450f);

            var layoutElement = cardObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 300f;
            layoutElement.preferredHeight = 450f;
            layoutElement.minWidth = 300f;
            layoutElement.minHeight = 450f;

            var backingImage = cardObject.GetComponent<Image>();
            backingImage.color = CardBackingColor;
            backingImage.raycastTarget = true;

            var button = cardObject.GetComponent<Button>();
            button.targetGraphic = backingImage;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.94f, 0.74f, 1f);
            colors.pressedColor = new Color(0.84f, 0.8f, 0.66f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.86f);
            button.colors = colors;

            var artworkImage = CreateImage("CardArtwork", cardRect, Color.white);
            StretchFull(artworkImage.rectTransform, 6f);
            artworkImage.preserveAspect = true;
            artworkImage.raycastTarget = false;

            var fallbackText = CreateText("FallbackCardIdText", cardRect, 25, FontStyle.Bold, TextAnchor.MiddleCenter);
            fallbackText.text = $"Card {slotIndex + 1}";
            StretchFull(fallbackText.rectTransform, 24f);

            var selectionOverlay = CreateImage("SelectionOverlay", cardRect, SelectionColor);
            StretchFull(selectionOverlay.rectTransform, 5f);
            selectionOverlay.raycastTarget = false;

            var attackValueText = CreateStatValueText(
                "AttackValueText",
                cardRect,
                HandCardStatOverlayLayout.DefaultAttackPosition);
            var hpValueText = CreateStatValueText(
                "HpValueText",
                cardRect,
                HandCardStatOverlayLayout.DefaultHpPosition);

            selectionOverlay.gameObject.SetActive(false);

            cardButtons.Add(button);
            artworkImages.Add(artworkImage);
            selectionOverlays.Add(selectionOverlay);
            fallbackTexts.Add(fallbackText);
            attackValueTexts.Add(attackValueText);
            hpValueTexts.Add(hpValueText);
        }

        private static Text CreateStatValueText(string objectName, RectTransform parent, Vector2 normalizedPosition)
        {
            var text = CreateText(objectName, parent, 34, FontStyle.Bold, TextAnchor.MiddleCenter);
            var rectTransform = text.rectTransform;
            rectTransform.anchorMin = normalizedPosition;
            rectTransform.anchorMax = normalizedPosition;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(72f, 54f);

            text.text = "0";
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            return text;
        }

        private static void AssignOverlayReferences(
            BattleMulliganOverlayPresenter presenter,
            Canvas canvas,
            CanvasGroup canvasGroup,
            Text titleText,
            Text statusText,
            Button confirmButton,
            Text confirmButtonText,
            IReadOnlyList<Button> cardButtons,
            IReadOnlyList<Image> artworkImages,
            IReadOnlyList<Image> selectionOverlays,
            IReadOnlyList<Text> fallbackTexts,
            IReadOnlyList<Text> attackValueTexts,
            IReadOnlyList<Text> hpValueTexts)
        {
            var serializedObject = new SerializedObject(presenter);
            serializedObject.FindProperty("_canvas").objectReferenceValue = canvas;
            serializedObject.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedObject.FindProperty("_titleText").objectReferenceValue = titleText;
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("_confirmButton").objectReferenceValue = confirmButton;
            serializedObject.FindProperty("_confirmButtonText").objectReferenceValue = confirmButtonText;
            AssignObjectArray(serializedObject.FindProperty("_cardButtons"), cardButtons);
            AssignObjectArray(serializedObject.FindProperty("_cardArtworkImages"), artworkImages);
            AssignObjectArray(serializedObject.FindProperty("_selectionOverlays"), selectionOverlays);
            AssignObjectArray(serializedObject.FindProperty("_fallbackCardIdTexts"), fallbackTexts);
            AssignObjectArray(serializedObject.FindProperty("_attackValueTexts"), attackValueTexts);
            AssignObjectArray(serializedObject.FindProperty("_hpValueTexts"), hpValueTexts);
            serializedObject.FindProperty("_resultDisplaySeconds").floatValue = 3f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignObjectArray<T>(SerializedProperty property, IReadOnlyList<T> values)
            where T : Object
        {
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
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

        private static void DestroySceneObjectByName(Scene scene, string objectName)
        {
            foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject != null &&
                    gameObject.scene == scene &&
                    gameObject.name == objectName)
                {
                    Object.DestroyImmediate(gameObject);
                    return;
                }
            }
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

        private static Text CreateText(
            string name,
            RectTransform parent,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(string name, RectTransform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            var image = gameObject.GetComponent<Image>();
            image.color = color;

            var button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.78f, 0.88f, 1f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.72f);
            button.colors = colors;
            return button;
        }

        private static void SetAnchoredRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static void StretchFull(RectTransform rectTransform, float padding = 0f)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(padding, padding);
            rectTransform.offsetMax = new Vector2(-padding, -padding);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null || layer < 0)
            {
                return;
            }

            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
