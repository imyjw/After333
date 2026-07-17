using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class CombatLogTextView : MonoBehaviour
    {
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Noto Sans CJK KR",
            "Noto Sans KR",
            "Arial Unicode MS",
        };

        private static TMP_FontAsset s_runtimeKoreanFontAsset;
        private static bool s_runtimeFontResolutionAttempted;

        [SerializeField] private TMP_Text _text;
        [SerializeField] private TMP_FontAsset _koreanFontAsset;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField, Min(0f)] private float _contentBottomPadding = 12f;
        [TextArea(4, 12)]
        [SerializeField] private string _fallbackText = "전투 기록이 없습니다.";

        private Coroutine _scrollToBottomCoroutine;

        private void Awake()
        {
            EnsureScrollArea();
            ApplyKoreanFont();
        }

        private void OnEnable()
        {
            EnsureScrollArea();
            ApplyKoreanFont();
        }

        public void SetLog(string logText)
        {
            if (_text == null)
            {
                return;
            }

            ApplyKoreanFont();
            _text.text = BattleRichTextStyler.StyleCombatLog(logText, _fallbackText);
            QueueScrollToBottom();
        }

        [ContextMenu("Ensure Editable Combat Log Scroll Area")]
        private void EnsureScrollArea()
        {
            if (_text == null)
            {
                return;
            }

            if (_scrollRect == null)
            {
                _scrollRect = _text.GetComponentInParent<ScrollRect>(true);
            }

            if (_scrollRect == null)
            {
                _scrollRect = CreateScrollAreaAroundText(_text);
            }

            if (_scrollRect != null)
            {
                _scrollRect.horizontal = false;
                _scrollRect.vertical = true;
                _scrollRect.movementType = ScrollRect.MovementType.Clamped;
                _scrollRect.scrollSensitivity = 32f;
            }
        }

        private static ScrollRect CreateScrollAreaAroundText(TMP_Text text)
        {
            var textRect = text.rectTransform;
            var originalParent = textRect.parent;
            if (originalParent == null)
            {
                return null;
            }

            var originalSiblingIndex = textRect.GetSiblingIndex();
            var originalAnchorMin = textRect.anchorMin;
            var originalAnchorMax = textRect.anchorMax;
            var originalPivot = textRect.pivot;
            var originalAnchoredPosition = textRect.anchoredPosition;
            var originalSizeDelta = textRect.sizeDelta;
            var originalLocalRotation = textRect.localRotation;
            var originalLocalScale = textRect.localScale;

            var scrollObject = new GameObject(
                "CombatLogScrollArea",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            scrollObject.layer = text.gameObject.layer;

            var scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.SetParent(originalParent, false);
            scrollRectTransform.SetSiblingIndex(originalSiblingIndex);
            scrollRectTransform.anchorMin = originalAnchorMin;
            scrollRectTransform.anchorMax = originalAnchorMax;
            scrollRectTransform.pivot = originalPivot;
            scrollRectTransform.anchoredPosition = originalAnchoredPosition;
            scrollRectTransform.sizeDelta = originalSizeDelta;
            scrollRectTransform.localRotation = originalLocalRotation;
            scrollRectTransform.localScale = originalLocalScale;

            var viewportImage = scrollObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;

            textRect.SetParent(scrollRectTransform, false);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(0f, Mathf.Max(1f, originalSizeDelta.y));
            textRect.localRotation = Quaternion.identity;
            textRect.localScale = Vector3.one;

            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.textWrappingMode = TextWrappingModes.Normal;

            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.content = textRect;
            scrollRect.viewport = scrollRectTransform;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 32f;
            return scrollRect;
        }

        private void QueueScrollToBottom()
        {
            if (_scrollRect == null || !isActiveAndEnabled)
            {
                return;
            }

            if (_scrollToBottomCoroutine != null)
            {
                StopCoroutine(_scrollToBottomCoroutine);
            }

            _scrollToBottomCoroutine = StartCoroutine(ScrollToBottomAfterLayout());
        }

        private IEnumerator ScrollToBottomAfterLayout()
        {
            yield return null;

            if (_text == null || _scrollRect == null)
            {
                _scrollToBottomCoroutine = null;
                yield break;
            }

            var contentRect = _scrollRect.content != null
                ? _scrollRect.content
                : _text.rectTransform;
            var viewportRect = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;
            var availableWidth = Mathf.Max(1f, viewportRect.rect.width);

            _text.ForceMeshUpdate();
            var preferredHeight = _text.GetPreferredValues(
                _text.text,
                availableWidth,
                Mathf.Infinity).y + _contentBottomPadding;
            contentRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(viewportRect.rect.height, preferredHeight));

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();
            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = 0f;
            _scrollToBottomCoroutine = null;
        }

        private void ApplyKoreanFont()
        {
            if (_text == null)
            {
                return;
            }

            var fontAsset = _koreanFontAsset != null
                ? _koreanFontAsset
                : ResolveRuntimeKoreanFontAsset();
            if (fontAsset != null && _text.font != fontAsset)
            {
                _text.font = fontAsset;
            }
        }

        internal static TMP_FontAsset ResolveRuntimeKoreanFontAsset()
        {
            if (s_runtimeKoreanFontAsset != null || s_runtimeFontResolutionAttempted)
            {
                return s_runtimeKoreanFontAsset;
            }

            s_runtimeFontResolutionAttempted = true;
            try
            {
                var sourceFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
                if (sourceFont == null)
                {
                    return null;
                }

                sourceFont.hideFlags = HideFlags.HideAndDontSave;
                s_runtimeKoreanFontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (s_runtimeKoreanFontAsset != null)
                {
                    s_runtimeKoreanFontAsset.name = "After333 Runtime Korean Combat Log Font";
                    s_runtimeKoreanFontAsset.hideFlags = HideFlags.HideAndDontSave;
                    s_runtimeKoreanFontAsset.isMultiAtlasTexturesEnabled = true;
                }
            }
            catch
            {
                s_runtimeKoreanFontAsset = null;
            }

            return s_runtimeKoreanFontAsset;
        }
    }
}
