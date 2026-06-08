using System.Collections.Generic;

namespace Project333.Runtime.Domain.Battle
{
    public sealed class HandState
    {
        private readonly List<string> _cardIds = new List<string>();

        public int Count => _cardIds.Count;

        public IReadOnlyList<string> CardIds => _cardIds;

        public void Add(string cardId)
        {
            _cardIds.Add(cardId);
        }

        public bool Remove(string cardId)
        {
            return _cardIds.Remove(cardId);
        }

        public bool Contains(string cardId)
        {
            return _cardIds.Contains(cardId);
        }
    }
}
