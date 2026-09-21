using System;
using System.Collections.Generic;

namespace Project333.Runtime.Presentation.Battle
{
    public enum CardPreviewTooltipZone { Ability, Attached, Influence }

    public readonly struct CardPreviewTooltipRect
    {
        public CardPreviewTooltipRect(float x, float y, float width, float height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float Left => X - Width * 0.5f;
        public float Right => X + Width * 0.5f;
        public float Bottom => Y - Height * 0.5f;
        public float Top => Y + Height * 0.5f;
        internal CardPreviewTooltipRect Scaled(float scale) =>
            new CardPreviewTooltipRect(X * scale, Y * scale, Width * scale, Height * scale);
    }

    public sealed class CardPreviewTooltipLayoutResult
    {
        internal CardPreviewTooltipLayoutResult(CardPreviewTooltipRect card,
            CardPreviewTooltipRect[] abilities, CardPreviewTooltipRect[] attached,
            CardPreviewTooltipRect[] influences, float textScale)
        {
            Card = card; Abilities = abilities; Attached = attached;
            Influences = influences; TextScale = textScale;
        }
        public CardPreviewTooltipRect Card { get; }
        public IReadOnlyList<CardPreviewTooltipRect> Abilities { get; }
        public IReadOnlyList<CardPreviewTooltipRect> Attached { get; }
        public IReadOnlyList<CardPreviewTooltipRect> Influences { get; }
        public float TextScale { get; }
    }

    // Uses canvas coordinates, never screen pixels. Top/bottom bands own the full
    // available width; ability columns only occupy the separate middle band.
    public static class CardPreviewTooltipLayout
    {
        public static CardPreviewTooltipLayoutResult Calculate(
            float width, float height, float cardWidth, float cardHeight,
            int abilityCount, int attachedCount, int influenceCount,
            Func<CardPreviewTooltipZone, int, float, float, float> measureHeight = null,
            float preferredPanelWidth = 340f, float preferredPanelHeight = 116f,
            float edgePadding = 24f, float sideGap = 28f, float gap = 12f)
        {
            width = Math.Max(1f, width); height = Math.Max(1f, height);
            cardWidth = Math.Max(1f, cardWidth); cardHeight = Math.Max(1f, cardHeight);
            abilityCount = Math.Max(0, abilityCount);
            attachedCount = Math.Max(0, attachedCount);
            influenceCount = Math.Max(0, influenceCount);
            var edge = Math.Min(Math.Max(0f, edgePadding), Math.Min(width, height) * 0.04f);
            var availableWidth = Math.Max(1f, width - 2f * edge);
            var availableHeight = Math.Max(1f, height - 2f * edge);
            gap = Math.Max(0f, gap); sideGap = Math.Max(0f, sideGap);
            preferredPanelWidth = Math.Max(120f, preferredPanelWidth);
            preferredPanelHeight = Math.Max(60f, preferredPanelHeight);
            var cache = new Dictionary<string, float>();
            float Measure(CardPreviewTooltipZone zone, int index, float panelWidth, float textScale)
            {
                var key = $"{(int)zone}/{index}/{panelWidth:R}/{textScale:R}";
                if (cache.TryGetValue(key, out var cached)) return cached;
                var measured = measureHeight?.Invoke(zone, index, panelWidth, textScale)
                    ?? preferredPanelHeight * textScale;
                var value = Math.Max(60f * textScale, measured);
                cache[key] = value;
                return value;
            }

            var minimumPanelWidth = Math.Min(210f, preferredPanelWidth);
            var minimumColumns = Math.Max(1, (int)Math.Ceiling(
                (availableWidth + gap) / (preferredPanelWidth + gap)));
            var maximumColumns = Math.Max(minimumColumns, (int)Math.Floor(
                (availableWidth + gap) / (minimumPanelWidth + gap)));
            // Preserve the readable text size first. Reduce the card before text.
            foreach (var textScale in new[] { 1f, 0.94f, 0.88f, 14f / 17f })
            {
                for (var cardStep = 0; cardStep <= 15; cardStep++)
                {
                    var scale = 1f - cardStep * 0.05f;
                    var resolvedCardWidth = Math.Min(cardWidth * scale, availableWidth);
                    var resolvedCardHeight = cardHeight * (resolvedCardWidth / cardWidth);
                    Candidate best = null;
                    for (var bandColumns = minimumColumns; bandColumns <= maximumColumns; bandColumns++)
                    {
                        for (var sideColumns = 1; sideColumns <= 2; sideColumns++)
                        {
                            var candidate = CreateCandidate(availableWidth, resolvedCardWidth, resolvedCardHeight,
                                abilityCount, attachedCount, influenceCount, bandColumns, sideColumns,
                                minimumPanelWidth, preferredPanelWidth, sideGap, gap, textScale, Measure);
                            if (candidate == null || candidate.Height > availableHeight + 0.01f) continue;
                            if (best == null || candidate.Score > best.Score) best = candidate;
                        }
                    }
                    if (best != null) return best.ToResult(1f);
                }
            }

            // Exceptional tiny canvases or future catalogs with far more effects:
            // keep every entry in bounds, without hidden pages or clipped text.
            var fallback = CreateCandidate(availableWidth,
                Math.Min(cardWidth * 0.25f, availableWidth * 0.30f),
                cardHeight * Math.Min(0.25f, availableWidth * 0.30f / cardWidth),
                abilityCount, attachedCount, influenceCount, maximumColumns, 1,
                0f, preferredPanelWidth, Math.Min(sideGap, availableWidth * 0.03f),
                Math.Min(gap, availableWidth * 0.02f), 14f / 17f, Measure);
            return fallback.ToResult(Math.Min(1f, availableHeight / Math.Max(1f, fallback.Height)));
        }

        private static Candidate CreateCandidate(float width, float cardWidth, float cardHeight,
            int abilityCount, int attachedCount, int influenceCount, int bandColumns,
            int sideColumns, float minimumPanelWidth, float preferredPanelWidth,
            float sideGap, float gap, float textScale,
            Func<CardPreviewTooltipZone, int, float, float, float> measure)
        {
            var leftCount = (abilityCount + 1) / 2;
            var rightCount = abilityCount - leftCount;
            sideColumns = Math.Max(1, Math.Min(sideColumns, Math.Max(1, leftCount)));
            var sideWidth = Math.Min(preferredPanelWidth,
                (width - cardWidth - 2f * sideGap - 2f * gap * (sideColumns - 1)) /
                (2f * sideColumns));
            if (abilityCount > 0 && sideWidth < minimumPanelWidth) return null;
            var above = MakeGrid(CardPreviewTooltipZone.Influence, influenceCount, 0,
                bandColumns, width, preferredPanelWidth, gap, textScale, measure);
            var below = MakeGrid(CardPreviewTooltipZone.Attached, attachedCount, 0,
                bandColumns, width, preferredPanelWidth, gap, textScale, measure);
            var left = MakeGrid(CardPreviewTooltipZone.Ability, leftCount, 0,
                sideColumns, Math.Max(1f, sideColumns * sideWidth + (sideColumns - 1) * gap),
                sideWidth, gap, textScale, measure);
            var right = MakeGrid(CardPreviewTooltipZone.Ability, rightCount, leftCount,
                sideColumns, Math.Max(1f, sideColumns * sideWidth + (sideColumns - 1) * gap),
                sideWidth, gap, textScale, measure);
            var middleHeight = Math.Max(cardHeight, Math.Max(left.Height, right.Height));
            var topGap = influenceCount > 0 ? gap : 0f;
            var bottomGap = attachedCount > 0 ? gap : 0f;
            var height = above.Height + topGap + middleHeight + bottomGap + below.Height;
            var middleY = (below.Height + bottomGap - above.Height - topGap) * 0.5f;
            var sides = new CardPreviewTooltipRect[abilityCount];
            CopyPlaced(left, sides, 0, -(cardWidth * 0.5f + sideGap + left.Width * 0.5f), middleY);
            CopyPlaced(right, sides, leftCount, cardWidth * 0.5f + sideGap + right.Width * 0.5f, middleY);
            var top = new CardPreviewTooltipRect[influenceCount];
            var bottom = new CardPreviewTooltipRect[attachedCount];
            CopyPlaced(above, top, 0, 0f, middleY + middleHeight * 0.5f + topGap + above.Height * 0.5f);
            CopyPlaced(below, bottom, 0, 0f, middleY - middleHeight * 0.5f - bottomGap - below.Height * 0.5f);
            return new Candidate(new CardPreviewTooltipRect(0f, middleY, cardWidth, cardHeight),
                sides, bottom, top, textScale, height,
                above.PanelWidth + below.PanelWidth + (abilityCount > 0 ? sideWidth * 2f : 0f));
        }

        private static Grid MakeGrid(CardPreviewTooltipZone zone, int count, int indexOffset,
            int columns, float availableWidth, float preferredWidth, float gap, float textScale,
            Func<CardPreviewTooltipZone, int, float, float, float> measure)
        {
            if (count == 0) return new Grid(new CardPreviewTooltipRect[0], 0f, 0f, 0f);
            columns = Math.Max(1, Math.Min(columns, count));
            var panelWidth = Math.Max(1f, Math.Min(preferredWidth,
                (availableWidth - gap * (columns - 1)) / columns));
            var rows = (count + columns - 1) / columns;
            var rowHeights = new float[rows];
            for (var index = 0; index < count; index++)
                rowHeights[index / columns] = Math.Max(rowHeights[index / columns],
                    measure(zone, index + indexOffset, panelWidth, textScale));
            var totalHeight = gap * (rows - 1);
            foreach (var rowHeight in rowHeights) totalHeight += rowHeight;
            var totalWidth = columns * panelWidth + (columns - 1) * gap;
            var boxes = new CardPreviewTooltipRect[count];
            var top = totalHeight * 0.5f;
            for (var row = 0; row < rows; row++)
            {
                var entriesInRow = Math.Min(columns, count - row * columns);
                var rowWidth = entriesInRow * panelWidth + (entriesInRow - 1) * gap;
                for (var column = 0; column < entriesInRow; column++)
                    boxes[row * columns + column] = new CardPreviewTooltipRect(
                        -rowWidth * 0.5f + panelWidth * 0.5f + column * (panelWidth + gap),
                        top - rowHeights[row] * 0.5f, panelWidth, rowHeights[row]);
                top -= rowHeights[row] + gap;
            }
            return new Grid(boxes, totalWidth, totalHeight, panelWidth);
        }

        private static void CopyPlaced(Grid grid, CardPreviewTooltipRect[] target, int offset, float x, float y)
        {
            for (var index = 0; index < grid.Boxes.Length; index++)
            {
                var box = grid.Boxes[index];
                target[index + offset] = new CardPreviewTooltipRect(box.X + x, box.Y + y, box.Width, box.Height);
            }
        }

        private sealed class Grid
        {
            public Grid(CardPreviewTooltipRect[] boxes, float width, float height, float panelWidth)
            { Boxes = boxes; Width = width; Height = height; PanelWidth = panelWidth; }
            public CardPreviewTooltipRect[] Boxes { get; }
            public float Width { get; }
            public float Height { get; }
            public float PanelWidth { get; }
        }

        private sealed class Candidate
        {
            private readonly CardPreviewTooltipRect _card;
            private readonly CardPreviewTooltipRect[] _abilities;
            private readonly CardPreviewTooltipRect[] _attached;
            private readonly CardPreviewTooltipRect[] _influences;
            private readonly float _textScale;
            public Candidate(CardPreviewTooltipRect card, CardPreviewTooltipRect[] abilities,
                CardPreviewTooltipRect[] attached, CardPreviewTooltipRect[] influences,
                float textScale, float height, float score)
            { _card = card; _abilities = abilities; _attached = attached; _influences = influences;
                _textScale = textScale; Height = height; Score = score; }
            public float Height { get; }
            public float Score { get; }
            public CardPreviewTooltipLayoutResult ToResult(float scale)
            {
                CardPreviewTooltipRect[] Scale(CardPreviewTooltipRect[] boxes)
                {
                    for (var i = 0; i < boxes.Length; i++) boxes[i] = boxes[i].Scaled(scale);
                    return boxes;
                }
                return new CardPreviewTooltipLayoutResult(_card.Scaled(scale),
                    Scale(_abilities), Scale(_attached), Scale(_influences), _textScale * scale);
            }
        }
    }
}
