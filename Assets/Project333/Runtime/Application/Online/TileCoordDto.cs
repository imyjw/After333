using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Online
{
    public sealed class TileCoordDto
    {
        public int Column { get; set; }

        public int Row { get; set; }

        public static TileCoordDto FromDomain(TileCoord coord)
        {
            return new TileCoordDto
            {
                Column = coord.Column,
                Row = coord.Row
            };
        }

        public TileCoord ToDomain()
        {
            return new TileCoord(Column, Row);
        }
    }
}
