using System;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class SystemDeckShuffler : IDeckShuffler
    {
        private readonly Random _random;

        public SystemDeckShuffler()
            : this(new Random())
        {
        }

        public SystemDeckShuffler(Random random)
        {
            _random = random;
        }

        public void Shuffle(DeckState deckState)
        {
            deckState.Shuffle(_random);
        }
    }
}
