using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class CastDamageSpellCommand : IBattleCommand
    {
        public CastDamageSpellCommand(string cardId, PlayerId targetOwnerId, TileCoord targetCoord)
        {
            CardId = cardId;
            TargetOwnerId = targetOwnerId;
            TargetCoord = targetCoord;
        }

        public string CardId { get; }

        public PlayerId TargetOwnerId { get; }

        public TileCoord TargetCoord { get; }
    }
}
