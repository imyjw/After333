using System;
using System.Security.Cryptography;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class SystemDeckShuffler : IDeckShuffler
    {
        private readonly Random _random;

        public SystemDeckShuffler()
            : this(CreateRandom())
        {
        }

        public SystemDeckShuffler(Random random)
        {
            _random = random;
        }

        public void Shuffle(DeckState deckState)
        {
            if (deckState == null)
            {
                throw new ArgumentNullException(nameof(deckState));
            }

            deckState.Shuffle(_random);
        }

        private static Random CreateRandom()
        {
            var seedBytes = new byte[sizeof(int)];
            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                randomNumberGenerator.GetBytes(seedBytes);
            }

            return new Random(BitConverter.ToInt32(seedBytes, 0));
        }
    }
}
