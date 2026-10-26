using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Hand
{
    // Shared imported artwork for both the hand marker and held-card preview.
    public sealed class TemporaryReplicateHourglass : UnityEngine.UI.Image
    {
        private static Sprite s_icon;

        public static void Present(ref TemporaryReplicateHourglass marker, UnityEngine.UI.Image artwork, bool visible)
        {
            if (visible && marker == null && artwork != null)
            {
                var node = new GameObject("TemporaryReplicateHourglass", typeof(RectTransform), typeof(CanvasRenderer));
                node.transform.SetParent(artwork.transform, false);
                marker = node.AddComponent<TemporaryReplicateHourglass>();
                marker.raycastTarget = false;
            }
            if (marker == null) return;
            marker.gameObject.SetActive(visible && artwork != null);
            if (!visible || artwork == null) return;
            if (s_icon == null)
            {
                var sprites = Resources.LoadAll<Sprite>("Project333/UI/TemporaryReplicateHourglass");
                if (sprites.Length > 0) s_icon = sprites[0];
            }
            marker.sprite = s_icon;
            marker.preserveAspect = true;
            marker.color = Color.white;
            marker.raycastTarget = false;
            marker.enabled = s_icon != null;
            if (marker.transform.parent != artwork.transform) marker.transform.SetParent(artwork.transform, false);
            var bounds = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(artwork);
            var width = bounds.width * .095f;
            var height = width * (s_icon != null ? s_icon.rect.height / s_icon.rect.width : 1.4f);
            var rect = marker.rectTransform;
            rect.anchorMin = rect.anchorMax = artwork.rectTransform.pivot;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(bounds.xMax - bounds.width * (-.025f) - width * .5f,
                bounds.yMax - bounds.height * (-.035f) - height * .5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
        }
    }
}
