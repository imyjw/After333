using System;

namespace Project333.Runtime.Domain.Board
{
    public struct TileCoord : IEquatable<TileCoord>, IComparable<TileCoord>
    {
        public TileCoord(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public int Column { get; }

        public int Row { get; }

        public int CompareTo(TileCoord other)
        {
            var columnCompare = Column.CompareTo(other.Column);
            if (columnCompare != 0)
            {
                return columnCompare;
            }

            return Row.CompareTo(other.Row);
        }

        public bool Equals(TileCoord other)
        {
            return Column == other.Column && Row == other.Row;
        }

        public override bool Equals(object obj)
        {
            return obj is TileCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Column * 397) ^ Row;
            }
        }

        public override string ToString()
        {
            return "(" + Column + "," + Row + ")";
        }

        public static bool operator ==(TileCoord left, TileCoord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TileCoord left, TileCoord right)
        {
            return !left.Equals(right);
        }
    }
}
