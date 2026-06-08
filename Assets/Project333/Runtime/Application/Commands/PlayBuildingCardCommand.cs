using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class PlayBuildingCardCommand : IBattleCommand
    {
        public PlayBuildingCardCommand(string cardId, TileCoord targetCoord)
        {
            CardId = cardId;
            TargetCoord = targetCoord;
        }

        public string CardId { get; }

        public TileCoord TargetCoord { get; }
    }
}
