using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class DraftSessionServiceTests
    {
        [Test]
        public void ValidateCatalog_FailsWhenLegendaryPoolHasFewerThanThreeCards()
        {
            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            catalog.ConfigureForTests(new CardDefinitionAsset[]
            {
                CreateUnitAsset("legendary-01", CardRarity.Legendary),
                CreateUnitAsset("legendary-02", CardRarity.Legendary),
                CreateUnitAsset("common-01", CardRarity.Common),
            });

            var service = new DraftSessionService(catalog, new System.Random(1));
            var validation = service.ValidateCatalog();

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Message, Does.Contain("at least 3 legendary cards"));
        }

        [Test]
        public void ValidateCatalog_FailsWhenNonLegendaryPoolHasFewerThanThirteenUniqueCards()
        {
            var cards = new CardDefinitionAsset[15];
            cards[0] = CreateUnitAsset("legendary-01", CardRarity.Legendary);
            cards[1] = CreateUnitAsset("legendary-02", CardRarity.Legendary);
            cards[2] = CreateUnitAsset("legendary-03", CardRarity.Legendary);

            for (var i = 0; i < 12; i++)
            {
                cards[i + 3] = CreateUnitAsset($"common-{i}", CardRarity.Common);
            }

            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            catalog.ConfigureForTests(cards);

            var service = new DraftSessionService(catalog, new System.Random(1));
            var validation = service.ValidateCatalog();

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Message, Does.Contain("13 unique non-legendary cards"));
        }

        [Test]
        public void BeginDraft_ReturnsThreeUniqueLegendaryCards()
        {
            var catalog = CreateValidDraftCatalog();
            var service = new DraftSessionService(catalog, new System.Random(3));

            var offer = service.BeginDraft();

            Assert.That(offer.IsLegendaryOpeningOffer, Is.True);
            Assert.That(offer.CandidateCards.Count, Is.EqualTo(3));
            Assert.That(offer.CandidateCards.All(card => card.Rarity == CardRarity.Legendary), Is.True);
            Assert.That(offer.CandidateCards.Select(card => card.CardId).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void SelectCard_AfterLegendaryPick_ReturnsUniqueNonLegendaryOffer()
        {
            var catalog = CreateValidDraftCatalog();
            var service = new DraftSessionService(catalog, new System.Random(7));
            var openingOffer = service.BeginDraft();

            var result = service.SelectCard(openingOffer.CandidateCards[0].CardId);

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.NextOffer, Is.Not.Null);
            Assert.That(result.NextOffer.IsLegendaryOpeningOffer, Is.False);
            Assert.That(result.NextOffer.CandidateCards.Count, Is.EqualTo(3));
            Assert.That(result.NextOffer.CandidateCards.All(card => card.Rarity != CardRarity.Legendary), Is.True);
            Assert.That(result.NextOffer.CandidateCards.Select(card => card.CardId).Distinct().Count(), Is.EqualTo(3));
        }

        private static CardDefinitionCatalogAsset CreateValidDraftCatalog()
        {
            var cards = new CardDefinitionAsset[16];
            cards[0] = CreateUnitAsset("legendary-01", CardRarity.Legendary);
            cards[1] = CreateUnitAsset("legendary-02", CardRarity.Legendary);
            cards[2] = CreateUnitAsset("legendary-03", CardRarity.Legendary);

            for (var i = 0; i < 13; i++)
            {
                cards[i + 3] = CreateUnitAsset($"common-{i}", CardRarity.Common);
            }

            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            catalog.ConfigureForTests(cards);
            return catalog;
        }

        private static UnitCardDefinitionAsset CreateUnitAsset(string cardId, CardRarity rarity)
        {
            var asset = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            asset.ConfigureBaseForTests(cardId, cardId, new ResourceSetData(0, 0, 0, 0));
            asset.ConfigureMetadataForTests(rarity, "Neutral", CardAffiliation.Neutral, ChargeTileFootprint.OneByOne, string.Empty, string.Empty);
            asset.ConfigureForTests(
                attackType: AttackType.Melee,
                attack: 1,
                health: 1,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0,
                turnStartResourceGain: new ResourceSetData(0, 0, 0, 0),
                maxAttacksPerTurn: 1,
                canAttackOnSummon: false);
            return asset;
        }
    }
}
