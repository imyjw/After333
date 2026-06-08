using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class PlayUnitCardCommand : IBattleCommand
    {
        public PlayUnitCardCommand(string cardId, TileCoord targetCoord)
        {
            CardId = cardId;
            TargetCoord = targetCoord;
        }

        public string CardId { get; }

        public TileCoord TargetCoord { get; }
    }
}
