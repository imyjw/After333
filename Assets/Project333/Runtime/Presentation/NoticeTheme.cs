using UnityEngine;

namespace Project333.Runtime.Presentation
{
    /// <summary>Shared appearance for notices, independent of their action/lifetime.</summary>
    public static class NoticeTheme
    {
        public static readonly Color PanelColor = new Color(.045f, .061f, .086f, .94f);
        private static Font _font;
        private static Sprite _rounded;
        public static Font Font => _font != null ? _font : (_font = Resources.Load<Font>("Project333/UI/NeoDunggeunmoPro-Regular"));

        private static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "NoticeRounded", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(8.5f - (x + .5f), x + .5f - 23.5f, 0);
                    var dy = Mathf.Max(8.5f - (y + .5f), y + .5f - 23.5f, 0);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(8.5f - Mathf.Sqrt(dx * dx + dy * dy)));
                }
                texture.SetPixels(pixels); texture.Apply(false, true);
                _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f,.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(10,10,10,10));
                return _rounded;
            }
        }

        public static void Text(UnityEngine.UI.Text text, int size = 28)
        {
            if (text == null) return;
            if (Font != null) text.font = Font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = new Color(.96f,.97f,.99f,1);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            foreach (var effect in text.GetComponents<UnityEngine.UI.Shadow>()) effect.enabled = false;
        }

        public static void Panel(UnityEngine.UI.Image image, bool interactive)
        {
            if (image == null) return;
            image.overrideSprite = null;
            image.preserveAspect = false;
            image.sprite = Rounded; image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = PanelColor; image.raycastTarget = interactive;
            var outline = image.GetComponent<UnityEngine.UI.Outline>() ?? image.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.enabled = true;
            outline.effectColor = new Color(.58f,.64f,.72f,.55f);
            outline.effectDistance = new Vector2(1,-1);
            outline.useGraphicAlpha = true;
        }

        public static void Button(UnityEngine.UI.Button button)
        {
            if (button == null) return;
            Panel(button.GetComponent<UnityEngine.UI.Image>(), true);
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(.72f,.80f,.91f,1);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.55f,.67f,.82f,1);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            foreach (var text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true)) Text(text, 26);
        }

        public static void Toast(RectTransform panel, UnityEngine.UI.Text text)
        {
            if (panel == null || text == null) return;
            Panel(panel.GetComponent<UnityEngine.UI.Image>(), false);
            Text(text);
            var parent = panel.parent as RectTransform;
            var available = parent == null || parent.rect.width <= 0 ? 960 : Mathf.Max(160, parent.rect.width - 64);
            var width = Mathf.Min(960, available, Mathf.Max(360, text.preferredWidth + 64));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f,.86f);
            panel.pivot = new Vector2(.5f,.5f); panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(width, 80);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(32,18); rect.offsetMax = new Vector2(-32,-18);
            var settings = text.GetGenerationSettings(new Vector2(width-64,0));
            var height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
            panel.sizeDelta = new Vector2(width, Mathf.Max(80, height+36));
        }

        // Timers and deck-count notices keep their existing HUD location and size.
        public static void Inline(RectTransform panel, UnityEngine.UI.Text text)
        {
            if (panel == null || text == null) return;
            Panel(panel.GetComponent<UnityEngine.UI.Image>(), false);
            Text(text);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(20, 8);
            rect.offsetMax = new Vector2(-20, -8);
            var width = Mathf.Max(1, panel.rect.width - 40);
            var settings = text.GetGenerationSettings(new Vector2(width, 0));
            var height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(panel.rect.height, height + 16));
        }

        public static void Dialog(RectTransform panel)
        {
            if (panel == null) return;
            Panel(panel.GetComponent<UnityEngine.UI.Image>(), true);
            foreach (var text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true)) Text(text);
            foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true)) Button(button);
        }
    }
}
