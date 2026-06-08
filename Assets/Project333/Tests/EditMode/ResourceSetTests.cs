using NUnit.Framework;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class ResourceSetTests
    {
        [Test]
        public void CanAfford_WhenGoldCanCoverManaQiAndPowerDeficits_ReturnsTrue()
        {
            var resources = new ResourceSet(mana: 0, qi: 0, power: 0, gold: 4);
            var cost = new ResourceSet(mana: 1, qi: 1, power: 1, gold: 1);

            var canAfford = resources.CanAfford(cost);

            Assert.That(canAfford, Is.True);
        }

        [Test]
        public void Spend_UsesTypedResourcesFirstAndGoldForRemainingDeficits()
        {
            var resources = new ResourceSet(mana: 1, qi: 0, power: 0, gold: 5);
            var cost = new ResourceSet(mana: 2, qi: 1, power: 0, gold: 1);

            resources.Spend(cost);

            Assert.That(resources.Mana, Is.EqualTo(0));
            Assert.That(resources.Qi, Is.EqualTo(0));
            Assert.That(resources.Power, Is.EqualTo(0));
            Assert.That(resources.Gold, Is.EqualTo(2));
        }
    }
}
