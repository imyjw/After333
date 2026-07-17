using System;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public abstract class CardDefinition
    {
        protected CardDefinition(
            string cardId,
            string displayName,
            CardType cardType,
            ResourceSet cost,
            bool includeInDraft = true,
            bool hasReplicate = false)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Card id is required.", nameof(cardId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name is required.", nameof(displayName));
            }

            CardId = cardId;
            DisplayName = displayName;
            CardType = cardType;
            Cost = cost?.Clone() ?? throw new ArgumentNullException(nameof(cost));
            IncludeInDraft = includeInDraft;
            HasReplicate = hasReplicate;
        }

        public string CardId { get; }

        public string DisplayName { get; }

        public CardType CardType { get; }

        public ResourceSet Cost { get; }

        public bool IncludeInDraft { get; }

        public bool HasReplicate { get; }
    }
}
