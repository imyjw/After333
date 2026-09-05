using System;
using System.Security.Cryptography;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class RandomFirstPlayerSelector
    {
        private readonly Random _random;

        public RandomFirstPlayerSelector()
            : this(CreateRandom())
        {
        }

        public RandomFirstPlayerSelector(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public PlayerId SelectFirstPlayer()
        {
            return _random.Next(2) == 0
                ? PlayerId.Player
                : PlayerId.AI;
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
