using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Domain.Cards
{
    public static class BiochemicalBombRules
    {
        public const string CardId = "BiochemicalBomb";
        public const string EffectId = "biochemical_bomb";
        public const int PowerCost = 4;
        public const int GoldCost = 1;
        public const int BaseDamage = 25;
        public const int TriggerCount = 4;
        public const int AreaWidth = 4;
        public const int LeftAreaStartColumn = 0;
        public const int RightAreaStartColumn = 1;

        public static bool IsValidTargetStart(TileCoord coord)
        {
            return coord.Row == 0 &&
                   (coord.Column == LeftAreaStartColumn || coord.Column == RightAreaStartColumn);
        }

        public static int ResolveStartColumnFromHoveredColumn(int hoveredColumn)
        {
            return hoveredColumn < BoardState.ColumnCount / 2
                ? LeftAreaStartColumn
                : RightAreaStartColumn;
        }

        public static bool ContainsColumn(int startColumn, int column)
        {
            return column >= startColumn && column < startColumn + AreaWidth;
        }
    }
}
