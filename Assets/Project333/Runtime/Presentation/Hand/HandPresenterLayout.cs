using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Presentation.Hand
{
    public readonly struct HandCardFanLayout
    {
        public HandCardFanLayout(Vector2 anchoredPosition, float rotationDegrees, float scale, float normalizedPosition)
        {
            AnchoredPosition = anchoredPosition;
            RotationDegrees = rotationDegrees;
            Scale = scale;
            NormalizedPosition = normalizedPosition;
        }

        public Vector2 AnchoredPosition { get; }

        public float RotationDegrees { get; }

        public float Scale { get; }

        public float NormalizedPosition { get; }
    }

    [Serializable]
    public sealed class HandCardCountSpacing
    {
        [SerializeField, Min(0f)] private float _twoCards = 250f;
        [SerializeField, Min(0f)] private float _threeCards = 225f;
        [SerializeField, Min(0f)] private float _fourCards = 200f;
        [SerializeField, Min(0f)] private float _fiveCards = 175f;
        [SerializeField, Min(0f)] private float _sixCards = 150f;
        [SerializeField, Min(0f)] private float _sevenCards = 125f;
        [SerializeField, Min(0f)] private float _eightCards = 100f;
        [SerializeField, Min(0f)] private float _nineCards = 75f;
        [SerializeField, Min(0f)] private float _tenCards = 50f;

        public float GetSpacing(int count)
        {
            var spacing = count switch
            {
                <= 1 => 0f,
                2 => _twoCards,
                3 => _threeCards,
                4 => _fourCards,
                5 => _fiveCards,
                6 => _sixCards,
                7 => _sevenCards,
                8 => _eightCards,
                9 => _nineCards,
                _ => _tenCards
            };
            return Mathf.Max(0f, spacing);
        }

        public HandCardCountSpacing Copy() => (HandCardCountSpacing)MemberwiseClone();
    }

    public static class HandPresenterLayout
    {
        public static HandCardFanLayout ApplyExpansion(HandCardFanLayout layout, bool expanded)
        {
            if (expanded) return layout;
            const float compactScale = 0.48f;
            return new HandCardFanLayout(layout.AnchoredPosition * compactScale + new Vector2(0, -24),
                layout.RotationDegrees, layout.Scale * compactScale, layout.NormalizedPosition);
        }

        public static string GetCardIdForSlot(IReadOnlyList<string> cardIds, int slotIndex)
        {
            if (slotIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Hand slot index cannot be negative.");
            }

            if (cardIds == null || slotIndex >= cardIds.Count)
            {
                return string.Empty;
            }

            return cardIds[slotIndex] ?? string.Empty;
        }

        public static HandCardFanLayout CalculateFanLayout(
            int visibleCardCount,
            int visibleCardIndex,
            float availableWidth,
            float minSpacing,
            float maxSpacing,
            float maxRotationDegrees,
            float arcHeight,
            float edgeDrop,
            float minimumScaleForLargeHands)
        {
            var spacing = Mathf.Clamp(availableWidth / Mathf.Max(1f, visibleCardCount - 1f), minSpacing, maxSpacing);
            return CalculateFanLayoutWithSpacing(visibleCardCount, visibleCardIndex, spacing,
                maxRotationDegrees, arcHeight, edgeDrop, minimumScaleForLargeHands);
        }

        // Explicit spacing for the player's hand. The legacy width-based API above
        // remains available to the opponent hand and other existing callers.
        public static HandCardFanLayout CalculateFanLayoutWithSpacing(
            int visibleCardCount, int visibleCardIndex, float spacing,
            float maxRotationDegrees, float arcHeight, float edgeDrop, float minimumScaleForLargeHands)
        {
            if (visibleCardCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleCardCount), "Visible card count must be positive.");
            }

            if (visibleCardIndex < 0 || visibleCardIndex >= visibleCardCount)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleCardIndex), "Visible card index must be inside the visible hand.");
            }

            if (visibleCardCount == 1)
            {
                return new HandCardFanLayout(
                    Vector2.zero,
                    0f,
                    1f,
                    0f);
            }

            var normalizedPosition = ((float)visibleCardIndex / (visibleCardCount - 1f) * 2f) - 1f;
            var halfSpan = Mathf.Max(0f, spacing) * (visibleCardCount - 1f) * 0.5f;
            var distanceFromCenter = Mathf.Abs(normalizedPosition);
            var curveWeight = 1f - distanceFromCenter;

            var x = normalizedPosition * halfSpan;
            var y = (curveWeight * arcHeight) - (distanceFromCenter * edgeDrop);
            var rotation = -normalizedPosition * maxRotationDegrees;

            var largeHandT = Mathf.InverseLerp(4f, 10f, visibleCardCount);
            var handScale = Mathf.Lerp(1f, minimumScaleForLargeHands, largeHandT);
            var edgeScale = Mathf.Lerp(1f, 0.96f, distanceFromCenter);

            return new HandCardFanLayout(
                new Vector2(x, y),
                rotation,
                handScale * edgeScale,
                normalizedPosition);
        }

        // Keep the largest fraction of the requested spacing whose rotated card
        // rectangles fit inside the viewport. Only x positions change, never size.
        public static float CalculateHorizontalFitFactor(
            IReadOnlyList<HandCardFanLayout> layouts, Vector2 cardSize,
            IReadOnlyList<Vector2> pivots, float viewportWidth)
        {
            if (layouts == null || pivots == null || layouts.Count != pivots.Count)
            {
                throw new ArgumentException("Each hand layout requires its actual card pivot.");
            }

            var lower = 0f;
            var upper = 1f;
            var halfWidth = Mathf.Max(0f, viewportWidth) * 0.5f;
            for (var i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                var angle = layout.RotationDegrees * Mathf.Deg2Rad;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                var center = ((0.5f - pivots[i].x) * cardSize.x * cos -
                              (0.5f - pivots[i].y) * cardSize.y * sin) * layout.Scale;
                var extent = (Mathf.Abs(cos) * cardSize.x + Mathf.Abs(sin) * cardSize.y) * layout.Scale * 0.5f;
                var minimum = center - extent;
                var maximum = center + extent;
                var x = layout.AnchoredPosition.x;
                if (Mathf.Abs(x) < 0.0001f)
                {
                    if (minimum < -halfWidth || maximum > halfWidth) return 0f;
                    continue;
                }

                var leftLimit = (-halfWidth - minimum) / x;
                var rightLimit = (halfWidth - maximum) / x;
                lower = Mathf.Max(lower, Mathf.Min(leftLimit, rightLimit));
                upper = Mathf.Min(upper, Mathf.Max(leftLimit, rightLimit));
            }

            // An individually oversized card cannot be fitted by spacing alone.
            // Collapse the spread without silently shrinking the user's artwork.
            return lower <= upper ? Mathf.Clamp01(upper) : 0f;
        }

        public static int[] GetSiblingOrderForFan(IReadOnlyList<HandCardFanLayout> layouts, int selectedVisibleIndex)
        {
            if (layouts == null)
            {
                throw new ArgumentNullException(nameof(layouts));
            }

            var orderedIndices = new List<int>(layouts.Count);
            for (var i = 0; i < layouts.Count; i++)
            {
                orderedIndices.Add(i);
            }

            orderedIndices.Sort((leftIndex, rightIndex) =>
            {
                var leftSelected = leftIndex == selectedVisibleIndex;
                var rightSelected = rightIndex == selectedVisibleIndex;
                if (leftSelected != rightSelected)
                {
                    return leftSelected ? 1 : -1;
                }

                var normalizedCompare = layouts[leftIndex].NormalizedPosition.CompareTo(layouts[rightIndex].NormalizedPosition);
                if (normalizedCompare != 0)
                {
                    return normalizedCompare;
                }

                return leftIndex.CompareTo(rightIndex);
            });

            return orderedIndices.ToArray();
        }
    }
}
