using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Application.Commands
{
    public sealed class AttackCommand : IBattleCommand
    {
        public AttackCommand(TileCoord attackerCoord, TileCoord targetCoord)
        {
            AttackerCoord = attackerCoord;
            TargetCoord = targetCoord;
        }

        public TileCoord AttackerCoord { get; }

        public TileCoord TargetCoord { get; }
    }
}
