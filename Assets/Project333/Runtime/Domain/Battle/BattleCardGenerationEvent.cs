using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Domain.Battle
{
    public sealed class BattleCardGenerationEvent
    {
        public BattleCardGenerationEvent(
            PlayerId ownerId,
            string sourceRuntimeId,
            string sourceCardId,
            TileCoord sourceCoord,
            string generatedCardId,
            bool addedToHand)
        {
            OwnerId = ownerId;
            SourceRuntimeId = sourceRuntimeId ?? string.Empty;
            SourceCardId = sourceCardId ?? string.Empty;
            SourceCoord = sourceCoord;
            GeneratedCardId = generatedCardId ?? string.Empty;
            AddedToHand = addedToHand;
        }

        public PlayerId OwnerId { get; }
        public string SourceRuntimeId { get; }
        public string SourceCardId { get; }
        public TileCoord SourceCoord { get; }
        public string GeneratedCardId { get; }
        public bool AddedToHand { get; }
    }
}
