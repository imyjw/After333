using System;
using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Domain.Draft
{
    public sealed class DraftDeckState
    {
        public const int TargetDeckSize = 33;

        private readonly List<string> _cardIds = new List<string>();
        private readonly Dictionary<string, int> _copyCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<string> CardIds => _cardIds;

        public int Count => _cardIds.Count;

        public int RemainingPicks => TargetDeckSize - _cardIds.Count;

        public bool IsComplete => _cardIds.Count >= TargetDeckSize;

        public bool CanAddCard(string cardId, CardRarity rarity)
        {
            if (string.IsNullOrWhiteSpace(cardId) || IsComplete)
            {
                return false;
            }

            return GetCopyCount(cardId) < GetMaxCopies(rarity);
        }

        public void AddCard(string cardId, CardRarity rarity)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Draft card id is required.", nameof(cardId));
            }

            if (IsComplete)
            {
                throw new InvalidOperationException("The draft deck is already complete.");
            }

            if (!CanAddCard(cardId, rarity))
            {
                throw new InvalidOperationException($"Card '{cardId}' has reached its draft copy limit.");
            }

            _cardIds.Add(cardId);
            _copyCounts[cardId] = GetCopyCount(cardId) + 1;
        }

        public int GetCopyCount(string cardId)
        {
            return !string.IsNullOrWhiteSpace(cardId) && _copyCounts.TryGetValue(cardId, out var count)
                ? count
                : 0;
        }

        public static int GetMaxCopies(CardRarity rarity)
        {
            return rarity == CardRarity.Legendary ? 1 : 3;
        }
    }
}
