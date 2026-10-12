using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Battle
{
    // A white silhouette of the tile, leaving the original art and input surface intact.
    public sealed class TileTargetPulse : MonoBehaviour
    {
        private Image _image;
        private Material _material;

        public static TileTargetPulse Create(Image background)
        {
            var root = new GameObject("CardTargetPulse", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(background.transform, false);
            root.transform.SetAsFirstSibling();
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var pulse = root.AddComponent<TileTargetPulse>();
            pulse._image = root.GetComponent<Image>();
            pulse._image.raycastTarget = false;
            pulse._image.maskable = background.maskable;
            pulse._material = new Material(Resources.Load<Shader>("Project333/TileTargetPulse"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            pulse._image.material = pulse._material;
            return pulse;
        }

        public void Refresh(Image background, bool visible, float unscaledTime)
        {
            _image.enabled = visible;
            if (!visible) return;
            _image.sprite = background.overrideSprite;
            _image.type = background.type;
            _image.preserveAspect = background.preserveAspect;
            _image.fillCenter = background.fillCenter;
            _image.fillMethod = background.fillMethod;
            _image.fillOrigin = background.fillOrigin;
            _image.fillClockwise = background.fillClockwise;
            _image.fillAmount = background.fillAmount;
            _image.pixelsPerUnitMultiplier = background.pixelsPerUnitMultiplier;
            // Shared unscaled phase avoids restarting when highlights are cleared/rebuilt each frame.
            var wave = 0.5f - 0.5f * Mathf.Cos(unscaledTime * (2f * Mathf.PI / 1.2f));
            _image.color = new Color(1f, 1f, 1f, 0.65f * wave);
        }

        private void OnDestroy()
        {
            if (_material == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
        }
    }
}
