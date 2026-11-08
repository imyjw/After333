namespace Project333.Runtime.Domain.Battle
{
    public sealed class BattleCardDrawEvent
    {
        public BattleCardDrawEvent(PlayerId ownerId)
            : this(ownerId, string.Empty, string.Empty)
        {
        }

        public BattleCardDrawEvent(PlayerId ownerId, string sourceCardId, string drawnCardId = "")
        {
            OwnerId = ownerId;
            SourceCardId = sourceCardId ?? string.Empty;
            DrawnCardId = drawnCardId ?? string.Empty;
        }

        public PlayerId OwnerId { get; }

        public string SourceCardId { get; }

        public string DrawnCardId { get; }
    }
}
