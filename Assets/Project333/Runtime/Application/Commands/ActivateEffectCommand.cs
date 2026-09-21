using Project333.Runtime.Domain.Board;
namespace Project333.Runtime.Application.Commands
{
    public sealed class ActivateEffectCommand : IBattleCommand
    {
        public ActivateEffectCommand(TileCoord sourceCoord, string occupantRuntimeId)
        {
            SourceCoord = sourceCoord;
            OccupantRuntimeId = occupantRuntimeId;
        }
        public TileCoord SourceCoord { get; }
        public string OccupantRuntimeId { get; }
    }
}
