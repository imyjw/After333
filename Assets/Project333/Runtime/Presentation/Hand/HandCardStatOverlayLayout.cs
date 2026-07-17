using UnityEngine;

namespace Project333.Runtime.Presentation.Hand
{
    public static class HandCardStatOverlayLayout
    {
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
