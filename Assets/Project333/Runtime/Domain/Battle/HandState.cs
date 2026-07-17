using System;
using System.Collections.Generic;

namespace Project333.Runtime.Domain.Battle
{
    public sealed class HandState
    {
        private readonly List<string> _cardIds = new List<string>();
        private readonly List<HandCardState> _cards = new List<HandCardState>();

        public int Count => _cardIds.Count;

        public IReadOnlyList<string> CardIds => _cardIds;

        public IReadOnlyList<HandCardState> Cards => _cards;

        public HandCardState Add(string cardId)
        {
            return Add(cardId, Guid.NewGuid().ToString("N"), isTemporaryReplicate: false);
        }

        public HandCardState AddTemporaryReplicate(string cardId)
        {
            return Add(cardId, Guid.NewGuid().ToString("N"), isTemporaryReplicate: true);
        }

        public HandCardState Add(string cardId, string runtimeId, bool isTemporaryReplicate)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Card id is required.", nameof(cardId));
            }

            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                throw new ArgumentException("Hand card runtime id is required.", nameof(runtimeId));
            }

            var card = new HandCardState(runtimeId, cardId, isTemporaryReplicate);
            _cards.Add(card);
            _cardIds.Add(cardId);
            return card;
        }

        public bool Remove(string cardId, string runtimeId = null)
        {
            var index = FindRemovalIndex(cardId, runtimeId);
            if (index < 0)
            {
                return false;
            }

            _cards.RemoveAt(index);
            _cardIds.RemoveAt(index);
            return true;
        }

        public bool Contains(string cardId, string runtimeId = null)
        {
            return FindIndex(cardId, runtimeId) >= 0;
        }

        public HandCardState GetCardForPlay(string cardId, string runtimeId = null)
        {
            var index = FindRemovalIndex(cardId, runtimeId);
            return index < 0 ? null : _cards[index];
        }

        public int RemoveTemporaryReplicates()
        {
            var removedCount = 0;
            for (var index = _cards.Count - 1; index >= 0; index--)
            {
                if (!_cards[index].IsTemporaryReplicate)
                {
                    continue;
                }

                _cards.RemoveAt(index);
                _cardIds.RemoveAt(index);
                removedCount++;
            }

            return removedCount;
        }

        private int FindRemovalIndex(string cardId, string runtimeId)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId))
            {
                return FindIndex(cardId, runtimeId);
            }

            // Legacy commands identify only a card type. Prefer its temporary copy so
            // playing Replicate repeatedly never consumes another permanent copy first.
            for (var index = 0; index < _cards.Count; index++)
            {
                if (_cards[index].IsTemporaryReplicate &&
                    string.Equals(_cards[index].CardId, cardId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return FindIndex(cardId, runtimeId: null);
        }

        private int FindIndex(string cardId, string runtimeId)
        {
            for (var index = 0; index < _cards.Count; index++)
            {
                var card = _cards[index];
                if (!string.Equals(card.CardId, cardId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(runtimeId) ||
                    string.Equals(card.RuntimeId, runtimeId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }
    }

    public sealed class HandCardState
    {
        public HandCardState(string runtimeId, string cardId, bool isTemporaryReplicate)
        {
            RuntimeId = runtimeId ?? string.Empty;
            CardId = cardId ?? string.Empty;
            IsTemporaryReplicate = isTemporaryReplicate;
        }

        public string RuntimeId { get; }

        public string CardId { get; }

        public bool IsTemporaryReplicate { get; }
    }
}
