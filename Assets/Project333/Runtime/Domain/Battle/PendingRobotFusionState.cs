using System;

namespace Project333.Runtime.Domain.Battle
{
    public sealed class PendingRobotFusionState
    {
        public PendingRobotFusionState(PlayerId ownerId, string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Card id is required.", nameof(cardId));
            }

            OwnerId = ownerId;
            CardId = cardId;
        }

        public PlayerId OwnerId { get; }

        public string CardId { get; }
    }
}
