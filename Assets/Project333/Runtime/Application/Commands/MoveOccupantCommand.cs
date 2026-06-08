using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class MoveOccupantCommand : IBattleCommand
    {
        public MoveOccupantCommand(TileCoord from, TileCoord to)
        {
            From = from;
            To = to;
        }

        public TileCoord From { get; }

        public TileCoord To { get; }
    }
}
