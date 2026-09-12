using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Hand
{
    public static class HandCardStatOverlayLayout
    {
        public static readonly Vector2 DefaultAttackPosition = new Vector2(0.09f, 0.055f);
        public static readonly Vector2 DefaultHpPosition = new Vector2(0.92f, 0.055f);

        public static Rect CalculateRenderedSpriteRect(Image image)
        {
            var rect = image.GetPixelAdjustedRect();
            var sprite = image.overrideSprite;
            if (sprite == null || !image.preserveAspect || rect.width <= 0f || rect.height <= 0f)
                return rect;

            // Match Unity Image's aspect-fit placement, including non-centered pivots.
            var ratio = sprite.rect.width / sprite.rect.height;
            if (ratio > rect.width / rect.height)
            {
                var height = rect.width / ratio;
                rect.y += (rect.height - height) * image.rectTransform.pivot.y;
                rect.height = height;
            }
            else
            {
                var width = rect.height * ratio;
                rect.x += (rect.width - width) * image.rectTransform.pivot.x;
                rect.width = width;
            }
            return rect;
        }

        public static void PositionStatText(RectTransform text, RectTransform artwork,
            Rect renderedSpriteRect, Vector2 normalizedPosition)
        {
            if (text == null || artwork == null || !(text.parent is RectTransform parent))
                return;

            var worldPoint = artwork.TransformPoint(CalculateRenderedSpritePoint(renderedSpriteRect, normalizedPosition));
            var localPoint = parent.InverseTransformPoint(worldPoint);
            // Do not clamp to the label parent: a legacy stat band may not cover the artwork's footer.
            text.anchorMin = text.anchorMax = parent.pivot;
            text.pivot = new Vector2(0.5f, 0.5f);
            text.anchoredPosition3D = localPoint;
            text.localScale = Vector3.one;
            text.localRotation = Quaternion.identity;
        }

        public static Rect CalculateRenderedSpriteRect(
            Rect containerRect,
            Vector2 spriteSize,
            bool preserveAspect)
        {
            if (!preserveAspect ||
                containerRect.width <= 0f ||
                containerRect.height <= 0f ||
                spriteSize.x <= 0f ||
                spriteSize.y <= 0f)
            {
                return containerRect;
            }

            var spriteAspect = spriteSize.x / spriteSize.y;
            var containerAspect = containerRect.width / containerRect.height;
            if (spriteAspect > containerAspect)
            {
                var renderedHeight = containerRect.width / spriteAspect;
                return new Rect(
                    containerRect.xMin,
                    containerRect.center.y - (renderedHeight * 0.5f),
                    containerRect.width,
                    renderedHeight);
            }

            var renderedWidth = containerRect.height * spriteAspect;
            return new Rect(
                containerRect.center.x - (renderedWidth * 0.5f),
                containerRect.yMin,
                renderedWidth,
                containerRect.height);
        }

        public static Vector2 CalculateRenderedSpritePoint(
            Rect renderedSpriteRect,
            Vector2 normalizedSpritePosition)
        {
            var clampedPosition = new Vector2(
                Mathf.Clamp01(normalizedSpritePosition.x),
                Mathf.Clamp01(normalizedSpritePosition.y));
            return new Vector2(
                Mathf.Lerp(renderedSpriteRect.xMin, renderedSpriteRect.xMax, clampedPosition.x),
                Mathf.Lerp(renderedSpriteRect.yMin, renderedSpriteRect.yMax, clampedPosition.y));
        }

        public static Vector2 CalculateContainerAnchor(Rect containerRect, Vector2 localPoint)
        {
            if (containerRect.width <= 0f || containerRect.height <= 0f)
            {
                return new Vector2(0.5f, 0.5f);
            }

            return new Vector2(
                Mathf.InverseLerp(containerRect.xMin, containerRect.xMax, localPoint.x),
                Mathf.InverseLerp(containerRect.yMin, containerRect.yMax, localPoint.y));
        }

        public static int CalculateScaledFontSize(
            int templateFontSize,
            float renderedCardHeight,
            float referenceCardHeight)
        {
            var safeTemplateFontSize = Mathf.Max(1, templateFontSize);
            if (renderedCardHeight <= 0f || referenceCardHeight <= 0f)
            {
                return safeTemplateFontSize;
            }

            return Mathf.Max(
                1,
                Mathf.RoundToInt(safeTemplateFontSize * renderedCardHeight / referenceCardHeight));
        }
    }
}
