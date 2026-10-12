using System.Globalization;
using TMPro;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    // Public information only: a marker never receives a trap identity, level, cost or trigger condition.
    internal sealed class TrapStatusMarker : MonoBehaviour
    {
        private RectTransform _root;
        private TextMeshProUGUI _questionGlyph;
        private TextMeshProUGUI _countText;

        internal void Present(int count, TMP_FontAsset font)
        {
            if (count <= 0)
            {
                gameObject.SetActive(false);
                return;
            }
            EnsureUi();
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(.5f, .5f);
            _root.anchoredPosition = new Vector2(-42f, 24f);
            _root.sizeDelta = new Vector2(count > 1 ? 58f : 24f, 24f);
            _root.localScale = Vector3.one;
            _root.localRotation = Quaternion.identity;
            _countText.gameObject.SetActive(count > 1);
            _countText.text = count > 1 ? "X" + count.ToString(CultureInfo.InvariantCulture) : string.Empty;
            if (font != null) { _questionGlyph.font = font; _countText.font = font; }
            gameObject.SetActive(true);
        }

        private void EnsureUi()
        {
            if (_root != null) return;
            _root = (RectTransform)transform;
            _questionGlyph = CreateText("QuestionGlyph", 26f);
            _questionGlyph.text = "?";
            PlaceLeft(_questionGlyph.rectTransform, 0f, 24f);
            _countText = CreateText("Count", 16f);
            _countText.enableAutoSizing = true;
            _countText.fontSizeMin = 10f;
            _countText.fontSizeMax = 16f;
            PlaceLeft(_countText.rectTransform, 25f, 33f);
        }

        private TextMeshProUGUI CreateText(string name, float size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(_root, false);
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = new Color(1f, .83f, .34f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.richText = false;
            return text;
        }

        private static void PlaceLeft(RectTransform rect, float offset, float width)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(offset, 0f);
            rect.sizeDelta = new Vector2(width, 24f);
            rect.localScale = Vector3.one;
        }
    }
}
