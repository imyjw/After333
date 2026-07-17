using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project333.Runtime.Presentation.Draft
{
    [DisallowMultipleComponent]
    public sealed class DraftPvpMatchmakingOverlayView : MonoBehaviour
    {
        private const string RootName = "DraftPvpMatchmakingOverlayCanvas";

        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasScaler _canvasScaler;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _dimRect;
        [SerializeField] private Image _dimImage;
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private Image _panelImage;
        [SerializeField] private Outline _panelOutline;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Text _cancelButtonLabel;
        [SerializeField] private bool _layoutInitialized;

        private UnityAction _boundCancelAction;

        public static DraftPvpMatchmakingOverlayView GetOrCreate(Scene scene)
        {
            var views = Resources.FindObjectsOfTypeAll<DraftPvpMatchmakingOverlayView>();
            for (var i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.scene == scene)
                {
                    views[i].EnsureEditableHierarchy();
                    return views[i];
                }
            }

            var rootObject = new GameObject(
                RootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(DraftPvpMatchmakingOverlayView));
            SceneManager.MoveGameObjectToScene(rootObject, scene);
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Draft PVP Matchmaking Overlay");
            }
#endif
            var view = rootObject.GetComponent<DraftPvpMatchmakingOverlayView>();
            view.EnsureEditableHierarchy();
            view.Hide();
            return view;
        }

        [ContextMenu("Ensure Editable Matchmaking Overlay Hierarchy")]
        public void EnsureEditableHierarchy()
        {
            _canvas = GetComponent<Canvas>();
            _canvasScaler = GetComponent<CanvasScaler>();
            _canvasGroup = GetComponent<CanvasGroup>();

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 20010;
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            _canvasScaler.matchWidthOrHeight = 1f;

            _dimRect = ResolveOrCreateImage("Dim", transform, out _dimImage);
            _panelRect = ResolveOrCreatePanel("Panel", transform, out _panelImage, out _panelOutline);
            _statusText = ResolveOrCreateText("StatusText", _panelRect);
            _cancelButton = ResolveOrCreateButton("CancelButton", _panelRect, out _cancelButtonLabel);

            if (!_layoutInitialized)
            {
                ApplyDefaultLayout();
                ApplyDefaultVisuals();
                _layoutInitialized = true;
            }

            EnsureRuntimeFonts();
        }

        public void BindCancel(UnityAction action)
        {
            EnsureEditableHierarchy();
            if (_boundCancelAction != null)
            {
                _cancelButton.onClick.RemoveListener(_boundCancelAction);
            }

            _boundCancelAction = action;
            if (_boundCancelAction != null)
            {
                _cancelButton.onClick.AddListener(_boundCancelAction);
            }
        }

        public void ShowWaiting(bool isReconnect)
        {
            Show(isReconnect ? "전투에 재접속하는 중..." : "상대를 찾는 중입니다...", "취소", true);
        }

        public void ShowCancelling()
        {
            Show("매칭을 취소하는 중입니다...", "취소 중", false);
        }

        public void ShowFailure(string message)
        {
            Show(
                string.IsNullOrWhiteSpace(message)
                    ? "서버 연결에 실패했습니다. 나중에 다시 시도해주세요."
                    : message,
                "돌아가기",
                true);
        }

        public void Hide()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        private void Show(string message, string buttonLabel, bool buttonInteractable)
        {
            EnsureEditableHierarchy();
            gameObject.SetActive(true);
            _statusText.text = message ?? string.Empty;
            _cancelButtonLabel.text = buttonLabel ?? string.Empty;
            _cancelButton.interactable = buttonInteractable;
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        private void ApplyDefaultLayout()
        {
            _dimRect.anchorMin = Vector2.zero;
            _dimRect.anchorMax = Vector2.one;
            _dimRect.offsetMin = Vector2.zero;
            _dimRect.offsetMax = Vector2.zero;

            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.anchoredPosition = Vector2.zero;
            _panelRect.sizeDelta = new Vector2(680f, 300f);

            var statusRect = _statusText.rectTransform;
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(42f, 72f);
            statusRect.offsetMax = new Vector2(-42f, 0f);

            var cancelRect = _cancelButton.transform as RectTransform;
            cancelRect.anchorMin = new Vector2(0.5f, 0f);
            cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(0f, 42f);
            cancelRect.sizeDelta = new Vector2(220f, 64f);
        }

        private void ApplyDefaultVisuals()
        {
            _dimImage.color = new Color(0.01f, 0.015f, 0.025f, 0.72f);
            _dimImage.raycastTarget = true;
            _panelImage.color = new Color(0.045f, 0.075f, 0.12f, 0.96f);
            _panelOutline.effectColor = new Color(0.65f, 0.78f, 1f, 0.52f);
            _panelOutline.effectDistance = new Vector2(2f, -2f);

            var font = ResolveKoreanFont();
            _statusText.font = font;
            _statusText.fontSize = 30;
            _statusText.fontStyle = FontStyle.Bold;
            _statusText.alignment = TextAnchor.MiddleCenter;
            _statusText.color = new Color(0.94f, 0.98f, 1f, 1f);
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Truncate;
            _statusText.raycastTarget = false;

            var buttonImage = _cancelButton.GetComponent<Image>();
            buttonImage.color = new Color(0.22f, 0.23f, 0.28f, 1f);
            _cancelButtonLabel.font = font;
            _cancelButtonLabel.fontSize = 24;
            _cancelButtonLabel.fontStyle = FontStyle.Bold;
            _cancelButtonLabel.alignment = TextAnchor.MiddleCenter;
            _cancelButtonLabel.color = new Color(0.94f, 0.98f, 1f, 1f);
            _cancelButtonLabel.raycastTarget = false;
        }

        private void EnsureRuntimeFonts()
        {
            if (_statusText.font != null && _cancelButtonLabel.font != null)
            {
                return;
            }

            var font = ResolveKoreanFont();
            if (_statusText.font == null)
            {
                _statusText.font = font;
            }

            if (_cancelButtonLabel.font == null)
            {
                _cancelButtonLabel.font = font;
            }
        }

        private static RectTransform ResolveOrCreateImage(string name, Transform parent, out Image image)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                var childObject = new GameObject(name, typeof(RectTransform), typeof(Image));
                childObject.transform.SetParent(parent, false);
                RegisterCreatedObject(childObject);
                child = childObject.GetComponent<RectTransform>();
            }

            image = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
            return child;
        }

        private static RectTransform ResolveOrCreatePanel(
            string name,
            Transform parent,
            out Image image,
            out Outline outline)
        {
            var rect = ResolveOrCreateImage(name, parent, out image);
            outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
            return rect;
        }

        private static Text ResolveOrCreateText(string name, Transform parent)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                var childObject = new GameObject(name, typeof(RectTransform), typeof(Text));
                childObject.transform.SetParent(parent, false);
                RegisterCreatedObject(childObject);
                child = childObject.GetComponent<RectTransform>();
            }

            return child.GetComponent<Text>() ?? child.gameObject.AddComponent<Text>();
        }

        private static Button ResolveOrCreateButton(string name, Transform parent, out Text label)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                var childObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                childObject.transform.SetParent(parent, false);
                RegisterCreatedObject(childObject);
                child = childObject.GetComponent<RectTransform>();
            }

            var button = child.GetComponent<Button>() ?? child.gameObject.AddComponent<Button>();
            label = ResolveOrCreateText("Label", child);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static Font ResolveKoreanFont()
        {
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Noto Sans KR" },
                    32);
                if (font != null)
                {
                    return font;
                }
            }
            catch (Exception)
            {
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void RegisterCreatedObject(GameObject createdObject)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && createdObject != null)
            {
                Undo.RegisterCreatedObjectUndo(createdObject, "Create Draft PVP Matchmaking UI");
            }
#endif
        }
    }
}
