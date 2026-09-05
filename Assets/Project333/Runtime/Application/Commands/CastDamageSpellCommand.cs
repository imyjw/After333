using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class CastDamageSpellCommand : IHandCardCommand
    {
        public CastDamageSpellCommand(
            string cardId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            string handCardRuntimeId = null)
        {
            CardId = cardId;
            TargetOwnerId = targetOwnerId;
            TargetCoord = targetCoord;
            HandCardRuntimeId = handCardRuntimeId ?? string.Empty;
        }

        public string CardId { get; }

        public PlayerId TargetOwnerId { get; }

        public TileCoord TargetCoord { get; }

        public string HandCardRuntimeId { get; }
    }
}
