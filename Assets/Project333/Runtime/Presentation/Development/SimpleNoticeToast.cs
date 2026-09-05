using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Development
{
    public sealed class SimpleNoticeToast : MonoBehaviour
    {
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
            "Noto Sans CJK KR",
            "Noto Sans KR"
        };

        private static Font s_runtimeKoreanFont;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _backdropRect;
        private RectTransform _panelRect;
        private RectTransform _messageRect;
        private RectTransform _buttonRect;
        private Image _backdropImage;
        private Image _panelImage;
        private Image _buttonImage;
        private Button _confirmButton;
        private Text _messageText;
        private Text _buttonText;
        private Coroutine _releaseInputBlockCoroutine;

        public static void Show(
            string objectName,
            string message,
            int sortingOrder = 5200,
            float visibleSeconds = 2.8f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var dialog = GetOrCreate(objectName, sortingOrder);
            dialog.ShowInternal(message);
        }

        private static SimpleNoticeToast GetOrCreate(string objectName, int sortingOrder)
        {
            var existingObject = GameObject.Find(objectName);
            if (existingObject != null &&
                existingObject.TryGetComponent<SimpleNoticeToast>(out var existingDialog))
            {
                existingDialog.EnsureVisualObjects();
                existingDialog.SetSortingOrder(sortingOrder);
                return existingDialog;
            }

            var dialogObject = new GameObject(
                objectName,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(SimpleNoticeToast));
            var dialog = dialogObject.GetComponent<SimpleNoticeToast>();
            dialog.EnsureVisualObjects();
            dialog.SetSortingOrder(sortingOrder);
            return dialog;
        }

        private void ShowInternal(string message)
        {
            EnsureVisualObjects();
            if (_canvasGroup == null || _messageText == null)
            {
                return;
            }

            if (_releaseInputBlockCoroutine != null)
            {
                StopCoroutine(_releaseInputBlockCoroutine);
                _releaseInputBlockCoroutine = null;
            }

            _messageText.text = message;
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        private void Hide()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void EnsureVisualObjects()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
                if (_canvas != null)
                {
                    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                    _canvasGroup.interactable = false;
                    _canvasGroup.blocksRaycasts = false;
                }
            }

            if (TryGetComponent<CanvasScaler>(out var scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
            }

            if (!TryGetComponent<GraphicRaycaster>(out _))
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (_backdropRect == null)
            {
                var backdropObject = new GameObject("NoticeBackdrop", typeof(RectTransform), typeof(Image));
                backdropObject.transform.SetParent(transform, false);
                _backdropRect = backdropObject.GetComponent<RectTransform>();
                _backdropImage = backdropObject.GetComponent<Image>();
            }

            if (_panelRect == null)
            {
                var panelObject = new GameObject("NoticePanel", typeof(RectTransform), typeof(Image));
                panelObject.transform.SetParent(transform, false);
                _panelRect = panelObject.GetComponent<RectTransform>();
                _panelImage = panelObject.GetComponent<Image>();
            }

            if (_messageText == null)
            {
                var textObject = new GameObject("NoticeText", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(_panelRect, false);
                _messageRect = textObject.GetComponent<RectTransform>();
                _messageText = textObject.GetComponent<Text>();
                _messageText.alignment = TextAnchor.MiddleCenter;
                _messageText.fontStyle = FontStyle.Bold;
                _messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _messageText.verticalOverflow = VerticalWrapMode.Overflow;
                _messageText.supportRichText = false;
                _messageText.raycastTarget = false;
            }

            if (_confirmButton == null)
            {
                var buttonObject = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(_panelRect, false);
                _buttonRect = buttonObject.GetComponent<RectTransform>();
                _buttonImage = buttonObject.GetComponent<Image>();
                _confirmButton = buttonObject.GetComponent<Button>();
                _confirmButton.onClick.RemoveListener(ConfirmAndHide);
                _confirmButton.onClick.AddListener(ConfirmAndHide);
            }

            if (_buttonText == null)
            {
                var textObject = new GameObject("ConfirmButtonText", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(_confirmButton.transform, false);
                _buttonText = textObject.GetComponent<Text>();
                _buttonText.alignment = TextAnchor.MiddleCenter;
                _buttonText.fontStyle = FontStyle.Bold;
                _buttonText.horizontalOverflow = HorizontalWrapMode.Overflow;
                _buttonText.verticalOverflow = VerticalWrapMode.Overflow;
                _buttonText.supportRichText = false;
                _buttonText.raycastTarget = false;
            }

            ApplyVisualSettings();
        }

        private void ConfirmAndHide()
        {
            if (_releaseInputBlockCoroutine != null)
            {
                StopCoroutine(_releaseInputBlockCoroutine);
            }

            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = true;
            _releaseInputBlockCoroutine = StartCoroutine(ReleaseInputBlockNextFrame());
        }

        private IEnumerator ReleaseInputBlockNextFrame()
        {
            yield return null;

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
            }

            _releaseInputBlockCoroutine = null;
        }

        private void ApplyVisualSettings()
        {
            if (_backdropRect != null)
            {
                _backdropRect.SetAsFirstSibling();
                _backdropRect.anchorMin = Vector2.zero;
                _backdropRect.anchorMax = Vector2.one;
                _backdropRect.offsetMin = Vector2.zero;
                _backdropRect.offsetMax = Vector2.zero;
            }

            if (_backdropImage != null)
            {
                _backdropImage.color = new Color(0f, 0f, 0f, 0.48f);
                _backdropImage.raycastTarget = true;
            }

            if (_panelRect != null)
            {
                _panelRect.SetAsLastSibling();
                var anchor = new Vector2(0.5f, 0.5f);
                _panelRect.anchorMin = anchor;
                _panelRect.anchorMax = anchor;
                _panelRect.pivot = new Vector2(0.5f, 0.5f);
                _panelRect.anchoredPosition = Vector2.zero;
                _panelRect.sizeDelta = new Vector2(920f, 360f);
            }

            if (_panelImage != null)
            {
                _panelImage.color = new Color(0.055f, 0.07f, 0.1f, 0.98f);
                _panelImage.raycastTarget = true;
            }

            if (_messageText != null)
            {
                if (_messageRect == null)
                {
                    _messageRect = _messageText.rectTransform;
                }

                _messageRect.anchorMin = new Vector2(0f, 0.36f);
                _messageRect.anchorMax = new Vector2(1f, 1f);
                _messageRect.offsetMin = new Vector2(52f, 8f);
                _messageRect.offsetMax = new Vector2(-52f, -34f);
                _messageText.font = ResolveRuntimeFont();
                _messageText.fontSize = 34;
                _messageText.color = new Color(1f, 0.94f, 0.84f, 1f);
            }

            if (_buttonRect != null)
            {
                var anchor = new Vector2(0.5f, 0.19f);
                _buttonRect.anchorMin = anchor;
                _buttonRect.anchorMax = anchor;
                _buttonRect.pivot = new Vector2(0.5f, 0.5f);
                _buttonRect.anchoredPosition = Vector2.zero;
                _buttonRect.sizeDelta = new Vector2(240f, 70f);
            }

            if (_buttonImage != null)
            {
                _buttonImage.color = new Color(0.72f, 0.48f, 0.15f, 1f);
                _buttonImage.raycastTarget = true;
            }

            if (_buttonText != null)
            {
                var textRect = _buttonText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(12f, 8f);
                textRect.offsetMax = new Vector2(-12f, -8f);
                _buttonText.font = ResolveRuntimeFont();
                _buttonText.fontSize = 30;
                _buttonText.color = new Color(1f, 0.96f, 0.82f, 1f);
                _buttonText.text = "확인";
            }
        }

        private void SetSortingOrder(int sortingOrder)
        {
            EnsureVisualObjects();
            if (_canvas != null)
            {
                _canvas.sortingOrder = sortingOrder;
            }
        }

        private static Font ResolveRuntimeFont()
        {
            if (s_runtimeKoreanFont != null)
            {
                return s_runtimeKoreanFont;
            }

            for (var i = 0; i < KoreanFontCandidates.Length; i++)
            {
                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates[i], 30);
                    if (font != null)
                    {
                        s_runtimeKoreanFont = font;
                        return s_runtimeKoreanFont;
                    }
                }
                catch
                {
                    // Some build targets cannot resolve every OS font name.
                }
            }

            s_runtimeKoreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return s_runtimeKoreanFont;
        }
    }
}
