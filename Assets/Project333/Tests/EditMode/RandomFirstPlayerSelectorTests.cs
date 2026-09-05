using System;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class RandomFirstPlayerSelectorTests
    {
        [TestCase(0, PlayerId.Player)]
        [TestCase(1, PlayerId.AI)]
        public void SelectFirstPlayer_MapsRandomIndexToPlayer(int randomIndex, PlayerId expected)
        {
            var selector = new RandomFirstPlayerSelector(new FixedRandom(randomIndex));

            Assert.That(selector.SelectFirstPlayer(), Is.EqualTo(expected));
        }

        private sealed class FixedRandom : Random
        {
            private readonly int _value;

            public FixedRandom(int value)
            {
                _value = value;
            }

            public override int Next(int maxValue)
            {
                return _value;
            }
        }
    }
}
