using NUnit.Framework;
using Project333.Runtime.Domain.Draft;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class DraftDeckStateTests
    {
        [Test]
        public void GetMaxCopies_LegendaryIsOne_NonLegendaryIsThree()
        {
            Assert.That(DraftDeckState.GetMaxCopies(CardRarity.Legendary), Is.EqualTo(1));
            Assert.That(DraftDeckState.GetMaxCopies(CardRarity.Common), Is.EqualTo(3));
            Assert.That(DraftDeckState.GetMaxCopies(CardRarity.Unique), Is.EqualTo(3));
        }

        [Test]
        public void AddCard_NonLegendary_AllowsUpToThreeCopies()
        {
            var draftDeckState = new DraftDeckState();

            draftDeckState.AddCard("common-card", CardRarity.Common);
            draftDeckState.AddCard("common-card", CardRarity.Common);
            draftDeckState.AddCard("common-card", CardRarity.Common);

            Assert.That(draftDeckState.GetCopyCount("common-card"), Is.EqualTo(3));
            Assert.That(draftDeckState.CanAddCard("common-card", CardRarity.Common), Is.False);
        }

        [Test]
        public void AddCard_Legendary_AllowsOnlyOneCopy()
        {
            var draftDeckState = new DraftDeckState();
            draftDeckState.AddCard("legendary-card", CardRarity.Legendary);

            Assert.That(draftDeckState.CanAddCard("legendary-card", CardRarity.Legendary), Is.False);
            Assert.That(
                () => draftDeckState.AddCard("legendary-card", CardRarity.Legendary),
                Throws.InvalidOperationException);
        }

        [Test]
        public void AddCard_WhenDeckIsComplete_RejectsMoreCards()
        {
            var draftDeckState = new DraftDeckState();

            for (var i = 0; i < DraftDeckState.TargetDeckSize; i++)
            {
                draftDeckState.AddCard($"card-{i}", CardRarity.Common);
            }

            Assert.That(draftDeckState.IsComplete, Is.True);
            Assert.That(draftDeckState.RemainingPicks, Is.EqualTo(0));
            Assert.That(
                () => draftDeckState.AddCard("overflow-card", CardRarity.Common),
                Throws.InvalidOperationException);
        }
    }
}
