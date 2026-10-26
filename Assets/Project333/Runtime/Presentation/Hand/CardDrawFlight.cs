using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Hand
{
    // Input-transparent cosmetic copy. The real card remains in its correctly laid out hand slot.
    public sealed class CardDrawFlight : MonoBehaviour
    {
        public const float Duration = .38f;
        public const float Stagger = .07f;
        public struct Pose
        {
            public Vector2 Center, Size;
            public float Angle;
            public static bool TryCapture(RectTransform source, RectTransform parent, out Pose pose)
            {
                pose = default;
                if (source == null || parent == null) return false;
                var image = source.GetComponent<UnityEngine.UI.Image>();
                var bounds = image != null ? HandCardStatOverlayLayout.CalculateRenderedSpriteRect(image) : source.rect;
                var a = Convert(source, parent, bounds.min);
                var b = Convert(source, parent, new Vector2(bounds.xMax, bounds.yMin));
                var c = Convert(source, parent, new Vector2(bounds.xMin, bounds.yMax));
                pose = new Pose { Center = (b + c) * .5f, Size = new Vector2(Vector2.Distance(a, b), Vector2.Distance(a, c)), Angle = Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg };
                return pose.Size.x > .1f && pose.Size.y > .1f;
            }
            private static Vector2 Convert(RectTransform source, RectTransform parent, Vector2 point)
            {
                var from = source.GetComponentInParent<Canvas>()?.rootCanvas;
                var to = parent.GetComponentInParent<Canvas>()?.rootCanvas;
                var screen = RectTransformUtility.WorldToScreenPoint(from != null && from.renderMode != RenderMode.ScreenSpaceOverlay ? from.worldCamera : null, source.TransformPoint(point));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,to != null && to.renderMode != RenderMode.ScreenSpaceOverlay ? to.worldCamera : null,out var local);
                return local;
            }
        }
        private RectTransform _rect, _parent, _originRect;
        private GameObject _front, _back;
        private Pose _origin;
        private CardDrawOriginCache _originCache;
        private Vector2 _nominalSize;
        private float _start;

        public static CardDrawFlight Create(RectTransform parent, Pose origin, Sprite back, UnityEngine.UI.Image artwork, float delay, RectTransform originRect = null, CardDrawOriginCache originCache = null)
        {
            if (artwork == null || artwork.overrideSprite == null || artwork.canvas == null || parent == null) return null;
            var go = new GameObject("DrawFromPlayerDeck",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            go.transform.SetParent(parent,false);
            var canvas = go.GetComponent<Canvas>(); canvas.overrideSorting = true;
            canvas.sortingLayerID = artwork.canvas.sortingLayerID; canvas.sortingOrder = artwork.canvas.sortingOrder + 50;
            var group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var flight = go.AddComponent<CardDrawFlight>();
            flight._parent = parent; flight._rect = go.GetComponent<RectTransform>();
            flight._rect.anchorMin = flight._rect.anchorMax = new Vector2(.5f,.5f);
            flight._rect.pivot = new Vector2(.5f,.5f);
            flight._nominalSize = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(artwork).size;
            flight._rect.sizeDelta = flight._nominalSize;
            flight._origin = origin; flight._originRect = originRect; flight._start = Time.unscaledTime + delay;
            flight._originCache = originCache ?? new CardDrawOriginCache();
            flight._back = flight.Face("DeckBack",back != null ? back : artwork.overrideSprite,Color.white);
            flight._front = flight.Face("DrawnCard",artwork.overrideSprite,artwork.color);
            flight.CopyNumbers(artwork);
            flight.Tick(artwork);
            return flight;
        }

        private GameObject Face(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(transform,false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.sprite = sprite; image.color = color; image.preserveAspect = true; image.raycastTarget = false;
            return go;
        }

        private void CopyNumbers(UnityEngine.UI.Image artwork)
        {
            var bounds = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(artwork);
            foreach (var original in artwork.transform.parent.GetComponentsInChildren<Text>(true))
            {
                if (!original.enabled || !original.gameObject.activeInHierarchy || string.IsNullOrEmpty(original.text)) continue;
                var go = new GameObject(original.name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
                var rect = go.GetComponent<RectTransform>(); rect.SetParent(_front.transform,false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.pivot = new Vector2(.5f,.5f);
                var originalRect = original.rectTransform;
                var corners = new Vector3[4]; originalRect.GetWorldCorners(corners);
                var a = artwork.rectTransform.InverseTransformPoint(corners[0]);
                var b = artwork.rectTransform.InverseTransformPoint(corners[3]);
                var c = artwork.rectTransform.InverseTransformPoint(corners[1]);
                rect.anchoredPosition = (Vector2)((b+c)*.5f) - bounds.center;
                rect.sizeDelta = new Vector2(Vector3.Distance(a,b),Vector3.Distance(a,c));
                rect.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
                var text = go.GetComponent<Text>(); text.text = original.text; text.font = original.font; text.fontSize = original.fontSize;
                text.fontStyle = original.fontStyle; text.color = original.color; text.alignment = original.alignment; text.raycastTarget = false;
                text.supportRichText = original.supportRichText; text.resizeTextForBestFit = original.resizeTextForBestFit;
                text.resizeTextMinSize = original.resizeTextMinSize; text.resizeTextMaxSize = original.resizeTextMaxSize;
                text.horizontalOverflow = original.horizontalOverflow; text.verticalOverflow = original.verticalOverflow;
                var outline = original.GetComponent<Outline>();
                if (outline != null) { var copy = go.AddComponent<Outline>(); copy.effectColor=outline.effectColor; copy.effectDistance=outline.effectDistance; }
            }
        }

        // Called after hand layout, so resizing/expanding/rebinding follows the current RuntimeId destination.
        public bool Tick(UnityEngine.UI.Image target)
        {
            if (!Pose.TryCapture(target == null ? null : target.rectTransform,_parent,out var destination)) return false;
            // Reuse the battle's settled origin. Only changed layout/camera metadata
            // triggers projection; the destination still follows the animated hand.
            // Keep the exact captured deck layer even if PresentCount(0) hides it.
            var origin = _origin;
            if (_originRect != null && _originCache.TryGetPose(_originRect, _parent, out var currentOrigin))
                origin = currentOrigin;
            var progress = Mathf.Clamp01((Time.unscaledTime-_start)/Duration);
            var eased = Mathf.SmoothStep(0,1,progress);
            _rect.localPosition = Vector2.Lerp(origin.Center,destination.Center,eased) + Vector2.up * (Mathf.Sin(progress*Mathf.PI)*65f);
            _rect.localRotation = Quaternion.Euler(0,0,Mathf.LerpAngle(origin.Angle,destination.Angle,eased));
            var size = Vector2.Lerp(origin.Size,destination.Size,eased);
            var flip = Mathf.Clamp01((progress-.15f)/.5f);
            _rect.localScale = new Vector3(size.x/_nominalSize.x*Mathf.Max(.025f,Mathf.Abs(1-2*flip)),size.y/_nominalSize.y,1);
            _front.SetActive(flip >= .5f); _back.SetActive(flip < .5f);
            return progress >= 1;
        }
    }
}
