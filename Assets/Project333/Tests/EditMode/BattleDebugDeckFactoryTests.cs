using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BattleDebugDeckFactoryTests
    {
        [Test]
        public void CreateDeck_WhenCalledWithDefaults_CreatesTenSequentialCardIds()
        {
            var deck = BattleDebugDeckFactory.CreateDeck("P");

            Assert.That(deck.Length, Is.EqualTo(10));
            Assert.That(deck[0], Is.EqualTo("P-00"));
            Assert.That(deck[1], Is.EqualTo("P-01"));
            Assert.That(deck[9], Is.EqualTo("P-09"));
        }

        [Test]
        public void CreateDeck_WhenDeckSizeIsBelowMinimum_Throws()
        {
            var exception = Assert.Throws<System.ArgumentOutOfRangeException>(
                () => BattleDebugDeckFactory.CreateDeck("P", 9));

            Assert.That(exception!.Message, Does.Contain("Debug deck size must be at least 10."));
        }
    }
}
