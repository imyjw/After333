namespace Project333.Runtime.Domain.Battle
{
    public sealed class BattleCardDrawEvent
    {
        public BattleCardDrawEvent(PlayerId ownerId)
            : this(ownerId, string.Empty)
        {
        }

        public BattleCardDrawEvent(PlayerId ownerId, string sourceCardId)
        {
            OwnerId = ownerId;
            SourceCardId = sourceCardId ?? string.Empty;
        }

        public PlayerId OwnerId { get; }

        public string SourceCardId { get; }
    }
}
