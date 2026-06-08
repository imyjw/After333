using System;
using System.Collections.Generic;

namespace Project333.Runtime.Domain.Battle
{
    public sealed class DeckState
    {
        private readonly List<string> _cardIds;

        public DeckState(IEnumerable<string> cardIds)
        {
            _cardIds = new List<string>(cardIds);
        }

        public int Count => _cardIds.Count;

        public IReadOnlyList<string> CardIds => _cardIds;

        public bool TryDraw(out string cardId)
        {
            if (_cardIds.Count == 0)
            {
                cardId = string.Empty;
                return false;
            }

            var lastIndex = _cardIds.Count - 1;
            cardId = _cardIds[lastIndex];
            _cardIds.RemoveAt(lastIndex);
            return true;
        }

        public void AddToBottom(string cardId)
        {
            _cardIds.Insert(0, cardId);
        }

        public void AddToTop(string cardId)
        {
            _cardIds.Add(cardId);
        }

        public void AddRangeToTop(IEnumerable<string> cardIds)
        {
            _cardIds.AddRange(cardIds);
        }

        public void AddRangeToBottom(IEnumerable<string> cardIds)
        {
            _cardIds.InsertRange(0, new List<string>(cardIds));
        }

        public void Shuffle(Random random)
        {
            for (var i = _cardIds.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = _cardIds[i];
                _cardIds[i] = _cardIds[swapIndex];
                _cardIds[swapIndex] = temp;
            }
        }
    }
}
