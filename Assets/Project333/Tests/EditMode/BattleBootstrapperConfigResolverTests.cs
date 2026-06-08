using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class BattleBootstrapperConfigResolverTests
    {
        [Test]
        public void Resolve_WhenDeckAssetsExist_PrefersDeckAssetCardIds()
        {
            var playerCard = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            playerCard.ConfigureBaseForTests("player-card", "Player Card", new ResourceSetData(0, 0, 0, 1));
            playerCard.ConfigureForTests(AttackType.Melee, 1, 2, true, false, 0, new ResourceSetData(), 1, false);

            var aiCard = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            aiCard.ConfigureBaseForTests("ai-card", "AI Card", new ResourceSetData(0, 0, 0, 1));
            aiCard.ConfigureForTests(AttackType.Melee, 1, 2, true, false, 0, new ResourceSetData(), 1, false);

            var playerDeck = ScriptableObject.CreateInstance<DeckDefinitionAsset>();
            playerDeck.ConfigureForTests(new CardDefinitionAsset[] { playerCard });

            var aiDeck = ScriptableObject.CreateInstance<DeckDefinitionAsset>();
            aiDeck.ConfigureForTests(new CardDefinitionAsset[] { aiCard });

            var resolved = BattleBootstrapperConfigResolver.Resolve(
                cardCatalogAsset: null,
                playerDeckAsset: playerDeck,
                aiDeckAsset: aiDeck,
                playerDeckCardIds: new[] { "fallback-player" },
                aiDeckCardIds: new[] { "fallback-ai" },
                useGeneratedDebugDecksWhenEmpty: false,
                generatedDeckSize: 10);

            Assert.That(resolved.PlayerDeckCardIds.Count, Is.EqualTo(1));
            Assert.That(resolved.PlayerDeckCardIds[0], Is.EqualTo("player-card"));
            Assert.That(resolved.AIDeckCardIds[0], Is.EqualTo("ai-card"));
        }

        [Test]
        public void Resolve_WhenRuntimeOverridesExist_PrefersOverrideDeckCardIds()
        {
            var playerCard = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            playerCard.ConfigureBaseForTests("player-card", "Player Card", new ResourceSetData(0, 0, 0, 1));
            playerCard.ConfigureForTests(AttackType.Melee, 1, 2, true, false, 0, new ResourceSetData(), 1, false);

            var playerDeck = ScriptableObject.CreateInstance<DeckDefinitionAsset>();
            playerDeck.ConfigureForTests(new CardDefinitionAsset[] { playerCard });

            var resolved = BattleBootstrapperConfigResolver.Resolve(
                cardCatalogAsset: null,
                playerDeckAsset: playerDeck,
                aiDeckAsset: null,
                playerDeckCardIds: new[] { "fallback-player" },
                aiDeckCardIds: new[] { "fallback-ai" },
                useGeneratedDebugDecksWhenEmpty: false,
                generatedDeckSize: 10,
                playerDeckOverrideCardIds: new[] { "draft-card-01", "draft-card-02" });

            Assert.That(resolved.PlayerDeckCardIds, Is.EqualTo(new[] { "draft-card-01", "draft-card-02" }));
        }

        [Test]
        public void Resolve_WhenCatalogExists_UsesCatalogProvider()
        {
            var catalogCard = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            catalogCard.ConfigureBaseForTests("shared-card", "Catalog Version", new ResourceSetData(0, 0, 0, 1));
            catalogCard.ConfigureForTests(AttackType.Ranged, 7, 8, false, true, 2, new ResourceSetData(0, 1, 0, 0), 2, true);

            var deckCard = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            deckCard.ConfigureBaseForTests("shared-card", "Deck Version", new ResourceSetData(0, 0, 0, 1));
            deckCard.ConfigureForTests(AttackType.Melee, 1, 2, true, false, 0, new ResourceSetData(), 1, false);

            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            catalog.ConfigureForTests(new CardDefinitionAsset[] { catalogCard });

            var playerDeck = ScriptableObject.CreateInstance<DeckDefinitionAsset>();
            playerDeck.ConfigureForTests(new CardDefinitionAsset[] { deckCard });

            var resolved = BattleBootstrapperConfigResolver.Resolve(
                cardCatalogAsset: catalog,
                playerDeckAsset: playerDeck,
                aiDeckAsset: null,
                playerDeckCardIds: null,
                aiDeckCardIds: null,
                useGeneratedDebugDecksWhenEmpty: true,
                generatedDeckSize: 10);

            var definition = (UnitCardDefinition)resolved.CardDefinitionProvider.GetRequired("shared-card");

            Assert.That(definition.DisplayName, Is.EqualTo("Catalog Version"));
            Assert.That(definition.Attack, Is.EqualTo(7));
        }

        [Test]
        public void Resolve_WhenDecksAreMissingAndDebugEnabled_GeneratesDebugDecks()
        {
            var resolved = BattleBootstrapperConfigResolver.Resolve(
                cardCatalogAsset: null,
                playerDeckAsset: null,
                aiDeckAsset: null,
                playerDeckCardIds: null,
                aiDeckCardIds: null,
                useGeneratedDebugDecksWhenEmpty: true,
                generatedDeckSize: 10);

            Assert.That(resolved.PlayerDeckCardIds[0], Is.EqualTo("P-00"));
            Assert.That(resolved.AIDeckCardIds[0], Is.EqualTo("A-00"));
            Assert.That(resolved.PlayerDeckCardIds.Count, Is.EqualTo(10));
            Assert.That(resolved.AIDeckCardIds.Count, Is.EqualTo(10));
        }
    }
}
