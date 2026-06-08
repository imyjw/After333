using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class DeckDefinitionAssetTests
    {
        [Test]
        public void ToCardIds_ReturnsCardIdsInConfiguredOrder()
        {
            var cardA = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            cardA.ConfigureBaseForTests("unit-a", "Unit A", new ResourceSetData(0, 0, 0, 1));
            cardA.ConfigureForTests(AttackType.Melee, 1, 2, true, false, 0, new ResourceSetData(), 1, false);

            var cardB = ScriptableObject.CreateInstance<DamageSpellCardDefinitionAsset>();
            cardB.ConfigureBaseForTests("spell-b", "Spell B", new ResourceSetData(0, 0, 0, 1));
            cardB.ConfigureForTests(3);

            var deck = ScriptableObject.CreateInstance<DeckDefinitionAsset>();
            deck.ConfigureForTests(new CardDefinitionAsset[] { cardA, cardB });

            var cardIds = deck.ToCardIds();

            Assert.That(cardIds.Count, Is.EqualTo(2));
            Assert.That(cardIds[0], Is.EqualTo("unit-a"));
            Assert.That(cardIds[1], Is.EqualTo("spell-b"));
        }
    }
}
