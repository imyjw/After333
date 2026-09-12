using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Project333.Runtime.Domain.Draft;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.PvpServer.Runs;

public sealed class ServerDraftOfferGenerator
{
    public const int DeckSize = 33;
    public const int OfferSize = 3;
    public const int RequiredNonLegendaryCardKinds = 13;

    private static readonly (CardRarity Rarity, int Weight)[] NonLegendaryWeights =
    {
        (CardRarity.Common, DraftRarityWeights.Common),
        (CardRarity.Uncommon, DraftRarityWeights.Uncommon),
        (CardRarity.Rare, DraftRarityWeights.Rare),
        (CardRarity.Unique, DraftRarityWeights.Unique)
    };

    private readonly IReadOnlyList<JsonCardDefinitionRecord> _draftCards;
    private readonly Dictionary<string, JsonCardDefinitionRecord> _cardsById;

    public ServerDraftOfferGenerator(JsonCardDefinitionDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        _draftCards = (database.Cards ?? new List<JsonCardDefinitionRecord>())
            .Where(card =>
                card != null &&
                !string.IsNullOrWhiteSpace(card.Id) &&

                !string.Equals(card.Id, "Master", StringComparison.OrdinalIgnoreCase))
            .GroupBy(card => card.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        _cardsById = _draftCards.ToDictionary(
            card => card.Id,
            StringComparer.OrdinalIgnoreCase);

        _draftCards = _cardsById.Values.Where(card => card.IncludeInDraft).ToArray();
    }

    public IReadOnlyList<string> CreateOffer(
        int draftSeed,
        IReadOnlyList<string> selectedCardIds)
    {
        return CreateOfferCore(draftSeed, selectedCardIds, preserveAcceptedPicks: false);
    }

    internal IReadOnlyList<string> CreateOfferFromSavedPicks(int draftSeed, IReadOnlyList<string> selectedCardIds) =>
        CreateOfferCore(draftSeed, selectedCardIds, preserveAcceptedPicks: true);

    private IReadOnlyList<string> CreateOfferCore(int draftSeed, IReadOnlyList<string> selectedCardIds, bool preserveAcceptedPicks)
    {
        var selectedCards = ResolveAndValidateHistory(selectedCardIds, allowComplete: false, preserveAcceptedPicks);
        var random = CreateDeterministicRandom(draftSeed, selectedCards);

        if (selectedCards.Count == 0)
        {
            ValidateCatalog();
            return ChooseUniqueCards(
                _draftCards.Where(card => card.Rarity == CardRarity.Legendary).ToList(),
                OfferSize,
                random);
        }

        var copyCounts = selectedCards
            .GroupBy(card => card.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var remainingCards = _draftCards
            .Where(card =>
                card.Rarity != CardRarity.Legendary &&
                (!copyCounts.TryGetValue(card.Id, out var count) || count < 3))
            .ToList();

        if (remainingCards.Count < OfferSize)
        {
            throw new RunServiceException(
                "draft_pool_exhausted",
                "Fewer than 3 eligible non-Legendary cards remain in the server draft pool.");
        }

        var offer = new List<string>(OfferSize);
        while (offer.Count < OfferSize)
        {
            var rarity = ChooseWeightedRarity(remainingCards, random);
            var rarityCards = remainingCards
                .Where(card => card.Rarity == rarity)
                .ToList();
            if (rarityCards.Count == 0)
            {
                rarityCards = remainingCards;
            }

            var selectedCard = rarityCards[random.Next(rarityCards.Count)];
            offer.Add(selectedCard.Id);
            remainingCards.RemoveAll(card =>
                string.Equals(card.Id, selectedCard.Id, StringComparison.OrdinalIgnoreCase));
        }

        return offer;
    }

    public string ResolveSelectedCard(
        IReadOnlyList<string> authoritativeOfferCardIds,
        string? requestedCardId)
    {
        if (authoritativeOfferCardIds == null || authoritativeOfferCardIds.Count != OfferSize)
        {
            throw new RunServiceException(
                "invalid_server_draft_offer",
                "The server draft offer must contain exactly 3 cards.");
        }

        var normalizedCardId = requestedCardId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCardId))
        {
            throw new RunServiceException("invalid_draft_selection", "A selected draft card id is required.");
        }

        var selectedCardId = authoritativeOfferCardIds.FirstOrDefault(cardId =>
            string.Equals(cardId, normalizedCardId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(selectedCardId))
        {
            throw new RunServiceException(
                "card_not_in_draft_offer",
                $"Card '{normalizedCardId}' is not part of the current server draft offer.");
        }

        if (!_cardsById.TryGetValue(selectedCardId, out var card))
            throw new RunServiceException("draft_card_not_found", "The saved card ID is missing from the current catalog; the offer was not replaced.");
        return card.Id;
    }

    public IReadOnlyList<string> ValidateCompletedDeck(IReadOnlyList<string> selectedCardIds)
    {
        return ResolveAndValidateHistory(selectedCardIds, allowComplete: true)
            .Select(card => card.Id)
            .ToArray();
    }

    internal IReadOnlyList<string> ValidateSavedCompletedDeck(IReadOnlyList<string> ids) =>
        ResolveAndValidateHistory(ids, allowComplete: true, preserveAcceptedPicks: true).Select(card => card.Id).ToArray();

    private IReadOnlyList<JsonCardDefinitionRecord> ResolveAndValidateHistory(
        IReadOnlyList<string>? selectedCardIds,
        bool allowComplete, bool preserveAcceptedPicks = false)
    {
        var cardIds = selectedCardIds ?? Array.Empty<string>();
        var maximumCount = allowComplete ? DeckSize : DeckSize - 1;
        if (cardIds.Count > maximumCount)
        {
            throw new RunServiceException(
                "invalid_draft_pick_count",
                $"Draft history contains {cardIds.Count} picks; maximum allowed is {maximumCount}.");
        }

        if (allowComplete && cardIds.Count != DeckSize)
        {
            throw new RunServiceException(
                "invalid_deck_size",
                $"Completed server draft deck must contain exactly {DeckSize} cards.");
        }

        var resolvedCards = new List<JsonCardDefinitionRecord>(cardIds.Count);
        var copyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < cardIds.Count; index++)
        {
            var cardId = cardIds[index]?.Trim();
            if (string.IsNullOrWhiteSpace(cardId) || !_cardsById.TryGetValue(cardId, out var card) || (!preserveAcceptedPicks && !card.IncludeInDraft))
            {
                throw new RunServiceException(
                    "draft_card_not_found",
                    $"Draft card '{cardId}' was not found in the server card database.");
            }

            if (!preserveAcceptedPicks && index == 0 && card.Rarity != CardRarity.Legendary)
            {
                throw new RunServiceException(
                    "invalid_legendary_opening_pick",
                    "The first server draft pick must be Legendary.");
            }

            if (!preserveAcceptedPicks && index > 0 && card.Rarity == CardRarity.Legendary)
            {
                throw new RunServiceException(
                    "legendary_after_opening_pick",
                    "Legendary cards can only be selected for the first server draft pick.");
            }

            var copyCount = copyCounts.TryGetValue(card.Id, out var existingCount)
                ? existingCount + 1
                : 1;
            var copyLimit = preserveAcceptedPicks ? 3 : card.Rarity == CardRarity.Legendary ? 1 : 3;
            if (copyCount > copyLimit)
            {
                throw new RunServiceException(
                    "draft_copy_limit_exceeded",
                    $"Card '{card.Id}' exceeds its server draft copy limit of {copyLimit}.");
            }

            copyCounts[card.Id] = copyCount;
            resolvedCards.Add(card);
        }

        return resolvedCards;
    }

    private void ValidateCatalog()
    {
        var legendaryCount = _draftCards.Count(card => card.Rarity == CardRarity.Legendary);
        if (legendaryCount < OfferSize)
        {
            throw new RunServiceException(
                "invalid_draft_catalog",
                $"Server draft requires at least {OfferSize} unique Legendary cards, but found {legendaryCount}.");
        }

        var nonLegendaryCount = _draftCards.Count(card => card.Rarity != CardRarity.Legendary);
        if (nonLegendaryCount < RequiredNonLegendaryCardKinds)
        {
            throw new RunServiceException(
                "invalid_draft_catalog",
                $"Server draft requires at least {RequiredNonLegendaryCardKinds} unique non-Legendary cards, but found {nonLegendaryCount}.");
        }

        if (nonLegendaryCount * 3 < DeckSize - 1)
        {
            throw new RunServiceException(
                "invalid_draft_catalog",
                "Server draft pool does not have enough non-Legendary copy capacity for 32 picks.");
        }
    }

    private static IReadOnlyList<string> ChooseUniqueCards(
        List<JsonCardDefinitionRecord> cards,
        int count,
        Random random)
    {
        if (cards.Count < count)
        {
            throw new RunServiceException(
                "invalid_draft_catalog",
                $"Server draft offer requires {count} unique cards, but only {cards.Count} are available.");
        }

        var chosenCards = new List<string>(count);
        while (chosenCards.Count < count)
        {
            var index = random.Next(cards.Count);
            chosenCards.Add(cards[index].Id);
            cards.RemoveAt(index);
        }

        return chosenCards;
    }

    private static CardRarity ChooseWeightedRarity(
        IReadOnlyList<JsonCardDefinitionRecord> remainingCards,
        Random random)
    {
        var availableRarities = remainingCards
            .Select(card => card.Rarity)
            .ToHashSet();
        var availableWeights = NonLegendaryWeights
            .Where(weight => availableRarities.Contains(weight.Rarity))
            .ToArray();
        var totalWeight = availableWeights.Sum(weight => weight.Weight);
        if (totalWeight <= 0)
        {
            throw new RunServiceException(
                "invalid_draft_catalog",
                "No weighted non-Legendary rarity is available for the server draft offer.");
        }

        var roll = random.Next(totalWeight);
        foreach (var weight in availableWeights)
        {
            if (roll < weight.Weight)
            {
                return weight.Rarity;
            }

            roll -= weight.Weight;
        }

        return availableWeights[^1].Rarity;
    }

    private static Random CreateDeterministicRandom(
        int draftSeed,
        IReadOnlyList<JsonCardDefinitionRecord> selectedCards)
    {
        var input = new StringBuilder()
            .Append(draftSeed)
            .Append('|')
            .Append(selectedCards.Count);
        foreach (var card in selectedCards)
        {
            input.Append('|').Append(card.Id);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString()));
        var seed = BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(0, sizeof(int))) & int.MaxValue;
        return new Random(seed);
    }
}
