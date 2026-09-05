using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public readonly struct BoardResponsiveLayout
    {
        public BoardResponsiveLayout(
            Vector2 tileSize,
            Vector2 tileGap,
            Vector2 playerBoardCenter,
            Vector2 aiBoardCenter)
        {
            TileSize = tileSize;
            TileGap = tileGap;
            PlayerBoardCenter = playerBoardCenter;
            AIBoardCenter = aiBoardCenter;
        }

        public Vector2 TileSize { get; }

        public Vector2 TileGap { get; }

        public Vector2 PlayerBoardCenter { get; }

        public Vector2 AIBoardCenter { get; }

        public Vector2 GetTileCenter(PlayerId ownerId, TileCoord coord)
        {
            var horizontalStep = TileSize.x + TileGap.x;
            var verticalStep = TileSize.y + TileGap.y;
            var boardCenter = ownerId == PlayerId.Player ? PlayerBoardCenter : AIBoardCenter;

            // Front rows face the center, while the opponent's column order is mirrored vertically.
            var frontDirection = ownerId == PlayerId.Player ? 1f : -1f;
            var verticalDirection = ownerId == PlayerId.Player ? 1f : -1f;
            var rowOffset = (0.5f - coord.Row) * horizontalStep * frontDirection;
            var columnOffset = ((BoardState.ColumnCount - 1) * 0.5f - coord.Column) *
                               verticalStep * verticalDirection;

            return boardCenter + new Vector2(rowOffset, columnOffset);
        }
    }

    public static class BoardResponsiveLayoutCalculator
    {
        private const float MinimumSize = 1f;

        public static float CalculateUniformTileScale(Vector2 tileSize, Vector2 referenceTileSize)
        {
            var resolvedTileSize = new Vector2(
                Mathf.Max(MinimumSize, tileSize.x),
                Mathf.Max(MinimumSize, tileSize.y));
            var resolvedReferenceSize = new Vector2(
                Mathf.Max(MinimumSize, referenceTileSize.x),
                Mathf.Max(MinimumSize, referenceTileSize.y));

            return Mathf.Min(
                resolvedTileSize.x / resolvedReferenceSize.x,
                resolvedTileSize.y / resolvedReferenceSize.y);
        }

        public static Vector2 ScaleTileRelativeOffset(
            Vector2 referenceOffset,
            Vector2 tileSize,
            Vector2 referenceTileSize)
        {
            return referenceOffset * CalculateUniformTileScale(tileSize, referenceTileSize);
        }

        public static BoardResponsiveLayout Calculate(
            int screenWidth,
            int screenHeight,
            Rect safeArea,
            float heightUsage,
            float outerHorizontalPadding,
            float centerGap,
            Vector2 tileGap,
            float tileAspectRatio,
            float boardOutwardOffset = 0f)
        {
            var resolvedWidth = Mathf.Max(1, screenWidth);
            var resolvedHeight = Mathf.Max(1, screenHeight);
            var resolvedSafeArea = ResolveSafeArea(safeArea, resolvedWidth, resolvedHeight);
            var resolvedHeightUsage = Mathf.Clamp(heightUsage, 0.1f, 1f);
            var resolvedOuterPadding = Mathf.Clamp(
                outerHorizontalPadding,
                0f,
                resolvedSafeArea.width * 0.2f);
            var maxCenterGap = Mathf.Max(0f, resolvedSafeArea.width - resolvedOuterPadding * 2f - 2f);
            var resolvedCenterGap = Mathf.Clamp(centerGap, 0f, maxCenterGap);
            var resolvedTileGap = new Vector2(
                Mathf.Max(0f, tileGap.x),
                Mathf.Max(0f, tileGap.y));
            var resolvedAspectRatio = Mathf.Max(0.1f, tileAspectRatio);

            var sideWidth = Mathf.Max(
                MinimumSize,
                (resolvedSafeArea.width - resolvedOuterPadding * 2f - resolvedCenterGap) * 0.5f);
            var boardHeight = Mathf.Max(MinimumSize, resolvedSafeArea.height * resolvedHeightUsage);
            var heightForTiles = Mathf.Max(
                MinimumSize,
                boardHeight - resolvedTileGap.y * (BoardState.ColumnCount - 1));
            var widthForTiles = Mathf.Max(
                MinimumSize,
                sideWidth - resolvedTileGap.x * (BoardState.RowCount - 1));

            var tileHeightFromHeight = heightForTiles / BoardState.ColumnCount;
            var tileWidthFromWidth = widthForTiles / BoardState.RowCount;
            var tileHeight = Mathf.Max(
                MinimumSize,
                Mathf.Min(tileHeightFromHeight, tileWidthFromWidth / resolvedAspectRatio));
            var tileSize = new Vector2(tileHeight * resolvedAspectRatio, tileHeight);

            var playerCenterScreen = new Vector2(
                resolvedSafeArea.xMin + resolvedOuterPadding + sideWidth * 0.5f,
                resolvedSafeArea.center.y);
            var aiCenterScreen = new Vector2(
                resolvedSafeArea.xMax - resolvedOuterPadding - sideWidth * 0.5f,
                resolvedSafeArea.center.y);

            // Shift both boards outward without changing tile size, while keeping every tile inside the safe area.
            var requestedOutwardOffset = Mathf.Max(0f, boardOutwardOffset);
            var boardHalfWidth = tileSize.x + resolvedTileGap.x * 0.5f;
            var minimumPlayerCenterX = resolvedSafeArea.xMin + resolvedOuterPadding + boardHalfWidth;
            var maximumAiCenterX = resolvedSafeArea.xMax - resolvedOuterPadding - boardHalfWidth;
            var availablePlayerOffset = Mathf.Max(0f, playerCenterScreen.x - minimumPlayerCenterX);
            var availableAiOffset = Mathf.Max(0f, maximumAiCenterX - aiCenterScreen.x);
            var resolvedOutwardOffset = Mathf.Min(
                requestedOutwardOffset,
                availablePlayerOffset,
                availableAiOffset);
            playerCenterScreen.x -= resolvedOutwardOffset;
            aiCenterScreen.x += resolvedOutwardOffset;

            var screenCenter = new Vector2(resolvedWidth * 0.5f, resolvedHeight * 0.5f);

            return new BoardResponsiveLayout(
                tileSize,
                resolvedTileGap,
                playerCenterScreen - screenCenter,
                aiCenterScreen - screenCenter);
        }

        private static Rect ResolveSafeArea(Rect safeArea, int screenWidth, int screenHeight)
        {
            if (safeArea.width <= 0f || safeArea.height <= 0f)
            {
                return new Rect(0f, 0f, screenWidth, screenHeight);
            }

            var xMin = Mathf.Clamp(safeArea.xMin, 0f, screenWidth);
            var yMin = Mathf.Clamp(safeArea.yMin, 0f, screenHeight);
            var xMax = Mathf.Clamp(safeArea.xMax, xMin, screenWidth);
            var yMax = Mathf.Clamp(safeArea.yMax, yMin, screenHeight);
            if (xMax - xMin < MinimumSize || yMax - yMin < MinimumSize)
            {
                return new Rect(0f, 0f, screenWidth, screenHeight);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
