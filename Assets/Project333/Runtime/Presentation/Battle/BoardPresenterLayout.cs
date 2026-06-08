using System;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BoardPresenterLayout
    {
        public const int TilesPerSide = BoardState.ColumnCount * BoardState.RowCount;

        public static TileCoord GetCoordForIndex(int index)
        {
            if (index < 0 || index >= TilesPerSide)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Board presenter tile index is outside the valid range.");
            }

            return new TileCoord(index / BoardState.RowCount, index % BoardState.RowCount);
        }
    }
}
