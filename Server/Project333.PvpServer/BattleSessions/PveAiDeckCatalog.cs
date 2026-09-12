using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.PvpServer.BattleSessions;

public sealed class PveAiDeckCatalog
{
    public const int RequiredDeckCount = 10;
    public const int CardsPerDeck = 33;
    public const string FileName = "pve_ai_decks.json";

    private static readonly Lazy<PveAiDeckCatalog> Cached = new(() =>
        FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", FileName)),
            PrototypeCardDefinitions.LoadCardDefinitionDatabase()));
    private static readonly IReadOnlyDictionary<CardRarity, int> RarityCounts = new Dictionary<CardRarity, int>
    {
        [CardRarity.Legendary] = 1,
        [CardRarity.Unique] = 5,
        [CardRarity.Rare] = 9,
        [CardRarity.Uncommon] = 11,
        [CardRarity.Common] = 7
    };
    private readonly IReadOnlyList<PveAiDeck> _decks;

    private PveAiDeckCatalog(List<PveAiDeck> decks) => _decks = decks.AsReadOnly();
    public static PveAiDeckCatalog Default => Cached.Value;
    public IReadOnlyList<PveAiDeck> Decks => _decks;

    public PveAiDeck SelectDeck(Func<int, int>? selectIndex = null)
    {
        var index = selectIndex == null ? RandomNumberGenerator.GetInt32(_decks.Count) : selectIndex(_decks.Count);
        if (index < 0 || index >= _decks.Count)
            throw new InvalidOperationException("PvE AI deck selector returned an invalid index.");
        return _decks[index];
    }

    public static PveAiDeckCatalog FromJson(string json, JsonCardDefinitionDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        var input = JsonSerializer.Deserialize<CatalogInput>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw Invalid("The catalog is empty.");
        if (input.SchemaVersion != 1)
            throw Invalid("schemaVersion must be 1.");
        if (input.Decks == null || input.Decks.Count != RequiredDeckCount)
            throw Invalid($"Exactly {RequiredDeckCount} fixed decks are required.");

        var cards = database.Cards.ToDictionary(card => card.Id, StringComparer.OrdinalIgnoreCase);
        var deckIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var compositions = new HashSet<string>(StringComparer.Ordinal);
        var decks = new List<PveAiDeck>();
        foreach (var deck in input.Decks)
        {
            if (deck == null || string.IsNullOrWhiteSpace(deck.Id) || !deckIds.Add(deck.Id.Trim()))
                throw Invalid("Every deck needs a unique non-empty id.");
            if (deck.Cards == null || deck.Cards.Count == 0)
                throw Invalid($"{deck.Id}: cards must not be empty.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var totals = RarityCounts.Keys.ToDictionary(rarity => rarity, _ => 0);
            var expanded = new List<string>(CardsPerDeck);
            foreach (var entry in deck.Cards)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.CardId) ||
                    !cards.TryGetValue(entry.CardId.Trim(), out var card))
                    throw Invalid($"{deck.Id}: unknown or empty cardId '{entry?.CardId}'.");
                if (!card.IncludeInDraft)
                    throw Invalid($"{deck.Id}: {card.Id} has includeInDraft=false.");
                if (!seen.Add(card.Id))
                    throw Invalid($"{deck.Id}: {card.Id} appears in multiple entries; use a single count.");
                var limit = card.Rarity == CardRarity.Legendary ? 1 : 3;
                if (entry.Count < 1 || entry.Count > limit)
                    throw Invalid($"{deck.Id}: {card.Id} count must be between 1 and {limit}.");
                if (!totals.ContainsKey(card.Rarity))
                    throw Invalid($"{deck.Id}: unsupported rarity for {card.Id}.");
                totals[card.Rarity] += entry.Count;
                expanded.AddRange(Enumerable.Repeat(card.Id, entry.Count));
            }

            foreach (var (rarity, expected) in RarityCounts)
                if (totals[rarity] != expected)
                    throw Invalid($"{deck.Id}: {rarity} requires {expected} cards, found {totals[rarity]}.");
            if (expanded.Count != CardsPerDeck)
                throw Invalid($"{deck.Id}: exactly {CardsPerDeck} cards are required.");

            var signature = JsonSerializer.Serialize(expanded.OrderBy(id => id, StringComparer.Ordinal).ToArray());
            if (!compositions.Add(signature))
                throw Invalid($"{deck.Id}: another deck has the same card composition.");
            decks.Add(new PveAiDeck(deck.Id.Trim(), expanded));
        }
        return new PveAiDeckCatalog(decks);
    }

    private static InvalidOperationException Invalid(string message) =>
        new($"Invalid {FileName}: {message}");

    public sealed class CatalogInput
    {
        public int SchemaVersion { get; set; }
        public List<DeckInput>? Decks { get; set; }
    }

    public sealed class DeckInput
    {
        public string? Id { get; set; }
        public List<CardInput>? Cards { get; set; }
    }

    public sealed class CardInput
    {
        public string? CardId { get; set; }
        public int Count { get; set; }
    }
}

public sealed class PveAiDeck
{
    internal PveAiDeck(string id, IEnumerable<string> cardIds)
    {
        Id = id;
        CardIds = Array.AsReadOnly(cardIds.ToArray());
    }

    public string Id { get; }
    public IReadOnlyList<string> CardIds { get; }
}
