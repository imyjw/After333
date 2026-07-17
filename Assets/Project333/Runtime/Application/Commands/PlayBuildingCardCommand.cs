using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class PlayBuildingCardCommand : IHandCardCommand
    {
        public PlayBuildingCardCommand(string cardId, TileCoord targetCoord, string handCardRuntimeId = null)
        {
            CardId = cardId;
            TargetCoord = targetCoord;
            HandCardRuntimeId = handCardRuntimeId ?? string.Empty;
        }

        public string CardId { get; }

        public TileCoord TargetCoord { get; }

        public string HandCardRuntimeId { get; }
    }
}
