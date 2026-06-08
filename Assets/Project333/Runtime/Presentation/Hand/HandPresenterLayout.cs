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

    public static class HandPresenterLayout
    {
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
            var spacing = Mathf.Clamp(
                availableWidth / Mathf.Max(1f, visibleCardCount - 1f),
                minSpacing,
                maxSpacing);

            var halfSpan = spacing * (visibleCardCount - 1f) * 0.5f;
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
