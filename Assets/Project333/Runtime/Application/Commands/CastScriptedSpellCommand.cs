using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class CastScriptedSpellCommand : IBattleCommand
    {
        public CastScriptedSpellCommand(string cardId)
        {
            CardId = cardId;
            HasTarget = false;
        }

        public CastScriptedSpellCommand(string cardId, PlayerId targetOwnerId, TileCoord targetCoord)
        {
            CardId = cardId;
            TargetOwnerId = targetOwnerId;
            TargetCoord = targetCoord;
            HasTarget = true;
        }

        public string CardId { get; }

        public bool HasTarget { get; }

        public PlayerId TargetOwnerId { get; }

        public TileCoord TargetCoord { get; }
    }
}
