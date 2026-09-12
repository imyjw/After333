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
        [TestCase(false)]
        [TestCase(true)]
        public void RarityRoll_UsesExactSlotWeightsAndRenormalizesUnavailableRarities(bool excludeUnique)
        {
            var cards = new CardDefinitionAsset[]
            {
                CreateUnitAsset("common", CardRarity.Common),
                CreateUnitAsset("uncommon", CardRarity.Uncommon),
                CreateUnitAsset("rare", CardRarity.Rare),
                CreateUnitAsset("unique", CardRarity.Unique)
            };
            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            try
            {
                catalog.ConfigureForTests(cards);
                var total = excludeUnique ? 9600 : 10000;
                var random = new ExhaustiveRarityRandom(total);
                var service = new DraftSessionService(catalog, random);
                var eligible = cards.Where(c => !excludeUnique || c.Rarity != CardRarity.Unique).ToList();
                var choose = typeof(DraftSessionService).GetMethod("ChooseWeightedNonLegendaryRarity",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var counts = cards.ToDictionary(c => c.Rarity, _ => 0);
                for (var roll = 0; roll < total; roll++)
                {
                    var rarity = (CardRarity)choose.Invoke(service, new object[] { eligible });
                    counts[rarity]++;
                }
                Assert.That(counts[CardRarity.Common], Is.EqualTo(6600));
                Assert.That(counts[CardRarity.Uncommon], Is.EqualTo(2000));
                Assert.That(counts[CardRarity.Rare], Is.EqualTo(1000));
                Assert.That(counts[CardRarity.Unique], Is.EqualTo(excludeUnique ? 0 : 400));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                foreach (var card in cards) Object.DestroyImmediate(card);
            }
        }

        private sealed class ExhaustiveRarityRandom : System.Random
        {
            private readonly int _expectedTotal;
            private int _roll;
            public ExhaustiveRarityRandom(int expectedTotal) => _expectedTotal = expectedTotal;
            public override int Next(int maxValue)
            {
                Assert.That(maxValue, Is.EqualTo(_expectedTotal));
                return _roll++;
            }
        }

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

        [Test]
        public void ResumeDraft_WithSavedPickAndOffer_RestoresSameOffer()
        {
            var catalog = CreateValidDraftCatalog();
            var originalService = new DraftSessionService(catalog, new System.Random(11));
            var openingOffer = originalService.BeginDraft();
            var firstPickResult = originalService.SelectCard(openingOffer.CandidateCards[0].CardId);
            var savedPickCardIds = originalService.DeckState.CardIds.ToArray();
            var savedOfferCardIds = firstPickResult.NextOffer.CandidateCards
                .Select(card => card.CardId)
                .ToArray();

            var resumedService = new DraftSessionService(catalog, new System.Random(999));
            var resumedOffer = resumedService.ResumeDraft(savedPickCardIds, savedOfferCardIds);

            Assert.That(resumedService.DeckState.CardIds.ToArray(), Is.EqualTo(savedPickCardIds));
            Assert.That(resumedOffer.PickNumber, Is.EqualTo(2));
            Assert.That(resumedOffer.IsLegendaryOpeningOffer, Is.False);
            Assert.That(
                resumedOffer.CandidateCards.Select(card => card.CardId).ToArray(),
                Is.EqualTo(savedOfferCardIds));
        }

        [Test]
        public void RestoreServerDraft_PreservesIdsAndUsesLatestRarityEvenForAcceptedCopies()
        {
            var catalog = CreateValidDraftCatalog();
            var first = catalog.Cards.Single(c => c.CardId == "legendary-01");
            var repeated = catalog.Cards.Single(c => c.CardId == "common-0");
            first.ConfigureMetadataForTests(CardRarity.Common, CardAffiliation.Neutral, ChargeTileFootprint.OneByOne, "", "");
            repeated.ConfigureMetadataForTests(CardRarity.Legendary, CardAffiliation.Neutral, ChargeTileFootprint.OneByOne, "", "");
            var history = new[] { first.CardId, repeated.CardId, repeated.CardId, repeated.CardId };
            var ids = new[] { "common-2", "legendary-02", "common-1" };
            var session = new DraftSessionService(catalog);
            var offer = session.RestoreServerDraft(history, ids);
            Assert.That(session.DeckState.CardIds, Is.EqualTo(history));
            Assert.That(offer.CandidateCards.Select(c => c.CardId), Is.EqualTo(ids));
            Assert.That(offer.CandidateCards[1], Is.SameAs(catalog.Cards.Single(c => c.CardId == ids[1])));
            Assert.That(offer.CandidateCards[1].Rarity, Is.EqualTo(CardRarity.Legendary));
            Assert.That(first.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(session.DeckState.GetCopyCount(repeated.CardId), Is.EqualTo(3));
            Assert.Throws<System.InvalidOperationException>(() => new DraftSessionService(catalog).ResumeDraft(history, ids));
        }

        [Test]
        public void RestoreServerDraft_RendersStoredOpeningEvenWhenCurrentPoolCannotStartNewDraft()
        {
            var cards = new[] { CreateUnitAsset("a", CardRarity.Common), CreateUnitAsset("b", CardRarity.Rare), CreateUnitAsset("c", CardRarity.Unique) };
            var catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogAsset>();
            catalog.ConfigureForTests(cards);
            var offer = new DraftSessionService(catalog).RestoreServerDraft(System.Array.Empty<string>(), new[] { "c", "a", "b" });
            Assert.That(offer.CandidateCards.Select(c => c.CardId), Is.EqualTo(new[] { "c", "a", "b" }));
        }

        [Test]
        public void RestoreServerDraft_RejectsMissingOrDuplicateIdsWithoutLocalReplacement()
        {
            var catalog = CreateValidDraftCatalog();
            Assert.Throws<System.InvalidOperationException>(() => new DraftSessionService(catalog).RestoreServerDraft(System.Array.Empty<string>(), new[] { "missing", "common-1", "common-2" }));
            Assert.Throws<System.InvalidOperationException>(() => new DraftSessionService(catalog).RestoreServerDraft(System.Array.Empty<string>(), new[] { "common-1", "common-1", "common-2" }));
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
            asset.ConfigureMetadataForTests(rarity, CardAffiliation.Neutral, ChargeTileFootprint.OneByOne, string.Empty, string.Empty);
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
