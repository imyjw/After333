using Project333.Runtime.Domain.Board;

namespace Project333.Runtime.Domain.Battle
{
    public sealed class BattleValuePopupEvent
    {
        public BattleValuePopupEvent(string runtimeId, PlayerId ownerId, TileCoord coord, bool isHealing, int amount)
        {
            RuntimeId = runtimeId ?? string.Empty;
            OwnerId = ownerId;
            Coord = coord;
            IsHealing = isHealing;
            Amount = amount < 0 ? 0 : amount;
        }

        public string RuntimeId { get; }

        public PlayerId OwnerId { get; }

        public TileCoord Coord { get; }

        public bool IsHealing { get; }

        public int Amount { get; }
    }
}
