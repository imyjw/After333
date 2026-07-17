using System;
using System.Collections.Generic;
using System.Linq;
using Project333.Runtime.Domain.Draft;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Services
{
    public sealed class DraftSessionService
    {
        private const int OfferSize = 3;
        private const int NonLegendaryCommonWeight = 40;
        private const int NonLegendaryUncommonWeight = 30;
        private const int NonLegendaryRareWeight = 20;
        private const int NonLegendaryUniqueWeight = 10;
        private const int RequiredNonLegendaryUniqueCards = 13;

        private static readonly CardRarity[] WeightedRarityOrder =
        {
            CardRarity.Common,
            CardRarity.Uncommon,
            CardRarity.Rare,
            CardRarity.Unique,
        };

        private readonly List<CardDefinitionAsset> _catalogCards;
        private readonly System.Random _random;

        public DraftSessionService(CardDefinitionCatalogAsset cardCatalogAsset, System.Random random = null)
        {
            _catalogCards = cardCatalogAsset == null
                ? new List<CardDefinitionAsset>()
                : GetUniqueCardAssets(cardCatalogAsset.Cards);
            _random = random ?? new System.Random();
            DeckState = new DraftDeckState();
        }

        public DraftDeckState DeckState { get; }

        public DraftOffer CurrentOffer { get; private set; }

        public DraftValidationResult ValidateCatalog()
        {
            var legendaryCards = GetLegendaryCards();
            if (legendaryCards.Count < OfferSize)
            {
                return DraftValidationResult.Failure(
                    "Draft requires at least 3 legendary cards for the opening legendary pick.");
            }

            var nonLegendaryCards = GetNonLegendaryCards();
            if (nonLegendaryCards.Count < RequiredNonLegendaryUniqueCards)
            {
                return DraftValidationResult.Failure(
                    $"Draft requires at least {RequiredNonLegendaryUniqueCards} unique non-legendary cards so 32 picks can continue while each offer still shows 3 different cards.");
            }

            var totalNonLegendaryCapacity = nonLegendaryCards.Count * DraftDeckState.GetMaxCopies(CardRarity.Common);
            if (totalNonLegendaryCapacity < DraftDeckState.TargetDeckSize - 1)
            {
                return DraftValidationResult.Failure(
                    "Draft requires enough non-legendary copy capacity to fill the remaining 32 deck slots after the opening legendary pick.");
            }

            return DraftValidationResult.Success();
        }

        public DraftOffer BeginDraft()
        {
            var validation = ValidateCatalog();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Message);
            }

            CurrentOffer = new DraftOffer(
                candidateCards: CreateLegendaryOpeningOffer(),
                pickNumber: 1,
                isLegendaryOpeningOffer: true);

            return CurrentOffer;
        }

        public DraftOffer ResumeDraft(
            IReadOnlyList<string> selectedCardIds,
            IReadOnlyList<string> currentOfferCardIds = null)
        {
            var validation = ValidateCatalog();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Message);
            }

            if (selectedCardIds == null || selectedCardIds.Count == 0)
            {
                CurrentOffer = CreateSavedOfferOrDefault(
                    currentOfferCardIds,
                    pickNumber: 1,
                    isLegendaryOpeningOffer: true);
                return CurrentOffer;
            }

            if (DeckState.Count > 0)
            {
                throw new InvalidOperationException("Draft resume requires a fresh draft session.");
            }

            for (var i = 0; i < selectedCardIds.Count; i++)
            {
                var cardId = selectedCardIds[i];
                var card = _catalogCards.FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.CardId, cardId, StringComparison.Ordinal));
                if (card == null)
                {
                    throw new InvalidOperationException($"Saved draft card '{cardId}' was not found in the card catalog.");
                }

                if (i == 0 && card.Rarity != CardRarity.Legendary)
                {
                    throw new InvalidOperationException("Saved draft state is invalid: the first draft pick must be legendary.");
                }

                if (i > 0 && card.Rarity == CardRarity.Legendary)
                {
                    throw new InvalidOperationException("Saved draft state is invalid: legendary cards are only allowed as the first draft pick.");
                }

                DeckState.AddCard(card.CardId, card.Rarity);
            }

            if (DeckState.IsComplete)
            {
                CurrentOffer = null;
                return null;
            }

            CurrentOffer = CreateSavedOfferOrDefault(
                currentOfferCardIds,
                pickNumber: DeckState.Count + 1,
                isLegendaryOpeningOffer: false);

            return CurrentOffer;
        }

        private DraftOffer CreateSavedOfferOrDefault(
            IReadOnlyList<string> currentOfferCardIds,
            int pickNumber,
            bool isLegendaryOpeningOffer)
        {
            if (currentOfferCardIds == null || currentOfferCardIds.Count == 0)
            {
                return new DraftOffer(
                    candidateCards: isLegendaryOpeningOffer ? CreateLegendaryOpeningOffer() : CreateNonLegendaryOffer(),
                    pickNumber: pickNumber,
                    isLegendaryOpeningOffer: isLegendaryOpeningOffer);
            }

            if (currentOfferCardIds.Count != OfferSize)
            {
                throw new InvalidOperationException("Saved draft offer is invalid: offer must contain exactly 3 cards.");
            }

            var candidateCards = new List<CardDefinitionAsset>(OfferSize);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < currentOfferCardIds.Count; i++)
            {
                var cardId = currentOfferCardIds[i];
                if (string.IsNullOrWhiteSpace(cardId) || !seen.Add(cardId))
                {
                    throw new InvalidOperationException("Saved draft offer is invalid: offer cards must be unique.");
                }

                var card = _catalogCards.FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.CardId, cardId, StringComparison.Ordinal));
                if (card == null)
                {
                    throw new InvalidOperationException($"Saved draft offer card '{cardId}' was not found in the card catalog.");
                }

                if (isLegendaryOpeningOffer && card.Rarity != CardRarity.Legendary)
                {
                    throw new InvalidOperationException("Saved draft offer is invalid: opening offer must contain only legendary cards.");
                }

                if (!isLegendaryOpeningOffer &&
                    (card.Rarity == CardRarity.Legendary || !DeckState.CanAddCard(card.CardId, card.Rarity)))
                {
                    throw new InvalidOperationException($"Saved draft offer card '{card.CardId}' is no longer eligible.");
                }

                candidateCards.Add(card);
            }

            return new DraftOffer(
                candidateCards: candidateCards,
                pickNumber: pickNumber,
                isLegendaryOpeningOffer: isLegendaryOpeningOffer);
        }

        public DraftAdvanceResult SelectCard(string cardId)
        {
            if (CurrentOffer == null)
            {
                throw new InvalidOperationException("Draft has not started yet.");
            }

            var selectedCard = CurrentOffer.CandidateCards.FirstOrDefault(card =>
                card != null &&
                string.Equals(card.CardId, cardId, StringComparison.Ordinal));

            if (selectedCard == null)
            {
                throw new InvalidOperationException($"Card '{cardId}' is not part of the current draft offer.");
            }

            DeckState.AddCard(selectedCard.CardId, selectedCard.Rarity);

            if (DeckState.IsComplete)
            {
                var completedDeck = new List<string>(DeckState.CardIds);
                CurrentOffer = null;
                return new DraftAdvanceResult(
                    isComplete: true,
                    nextOffer: null,
                    completedDeckCardIds: completedDeck,
                    selectedCardId: selectedCard.CardId);
            }

            var nextOffer = new DraftOffer(
                candidateCards: CreateNonLegendaryOffer(),
                pickNumber: DeckState.Count + 1,
                isLegendaryOpeningOffer: false);
            CurrentOffer = nextOffer;

            return new DraftAdvanceResult(
                isComplete: false,
                nextOffer: nextOffer,
                completedDeckCardIds: null,
                selectedCardId: selectedCard.CardId);
        }

        private List<CardDefinitionAsset> CreateLegendaryOpeningOffer()
        {
            var legendaryCards = GetLegendaryCards();
            return ChooseUniqueCards(legendaryCards, OfferSize);
        }

        private List<CardDefinitionAsset> CreateNonLegendaryOffer()
        {
            var availableCards = GetNonLegendaryCards()
                .Where(card => DeckState.CanAddCard(card.CardId, card.Rarity))
                .ToList();

            if (availableCards.Count < OfferSize)
            {
                throw new InvalidOperationException(
                    "Draft cannot continue because fewer than 3 eligible non-legendary cards remain. Add more non-legendary card types to the draft pool.");
            }

            var offer = new List<CardDefinitionAsset>(OfferSize);
            var remainingCards = new List<CardDefinitionAsset>(availableCards);

            while (offer.Count < OfferSize)
            {
                var chosenRarity = ChooseWeightedNonLegendaryRarity(remainingCards);
                var matchingCards = remainingCards
                    .Where(card => card.Rarity == chosenRarity)
                    .ToList();

                if (matchingCards.Count == 0)
                {
                    matchingCards = remainingCards;
                }

                var chosenCard = matchingCards[_random.Next(matchingCards.Count)];
                offer.Add(chosenCard);
                remainingCards.RemoveAll(card => string.Equals(card.CardId, chosenCard.CardId, StringComparison.Ordinal));
            }

            return offer;
        }

        private CardRarity ChooseWeightedNonLegendaryRarity(List<CardDefinitionAsset> remainingCards)
        {
            var availableRarities = new HashSet<CardRarity>(remainingCards.Select(card => card.Rarity));
            var totalWeight = 0;

            foreach (var rarity in WeightedRarityOrder)
            {
                if (availableRarities.Contains(rarity))
                {
                    totalWeight += GetWeight(rarity);
                }
            }

            if (totalWeight <= 0)
            {
                return remainingCards[0].Rarity;
            }

            var roll = _random.Next(totalWeight);
            var cumulativeWeight = 0;

            foreach (var rarity in WeightedRarityOrder)
            {
                if (!availableRarities.Contains(rarity))
                {
                    continue;
                }

                cumulativeWeight += GetWeight(rarity);
                if (roll < cumulativeWeight)
                {
                    return rarity;
                }
            }

            return remainingCards[0].Rarity;
        }

        private List<CardDefinitionAsset> ChooseUniqueCards(List<CardDefinitionAsset> cards, int count)
        {
            if (cards.Count < count)
            {
                throw new InvalidOperationException($"Draft offer requires {count} unique cards, but only {cards.Count} are available.");
            }

            var pool = new List<CardDefinitionAsset>(cards);
            var chosenCards = new List<CardDefinitionAsset>(count);

            while (chosenCards.Count < count)
            {
                var chosenIndex = _random.Next(pool.Count);
                chosenCards.Add(pool[chosenIndex]);
                pool.RemoveAt(chosenIndex);
            }

            return chosenCards;
        }

        private List<CardDefinitionAsset> GetLegendaryCards()
        {
            return _catalogCards
                .Where(card => card != null && card.Rarity == CardRarity.Legendary)
                .ToList();
        }

        private List<CardDefinitionAsset> GetNonLegendaryCards()
        {
            return _catalogCards
                .Where(card => card != null && card.Rarity != CardRarity.Legendary)
                .ToList();
        }

        private static int GetWeight(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:
                    return NonLegendaryCommonWeight;
                case CardRarity.Uncommon:
                    return NonLegendaryUncommonWeight;
                case CardRarity.Rare:
                    return NonLegendaryRareWeight;
                case CardRarity.Unique:
                    return NonLegendaryUniqueWeight;
                default:
                    return 0;
            }
        }

        private static List<CardDefinitionAsset> GetUniqueCardAssets(IReadOnlyList<CardDefinitionAsset> cards)
        {
            var uniqueCards = new List<CardDefinitionAsset>();
            var seenCardIds = new HashSet<string>(StringComparer.Ordinal);

            if (cards == null)
            {
                return uniqueCards;
            }

            foreach (var card in cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.CardId) || !seenCardIds.Add(card.CardId))
                {
                    continue;
                }

                uniqueCards.Add(card);
            }

            return uniqueCards;
        }
    }
}
