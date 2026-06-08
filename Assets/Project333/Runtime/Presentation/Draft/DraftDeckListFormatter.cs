using System;
using System.Collections.Generic;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Presentation.Draft
{
    public static class DraftDeckListFormatter
    {
        private sealed class DeckListEntry
        {
            public string CardId;
            public string DisplayName;
            public ResourceSetData Cost;
            public int Count;
        }

        public static string BuildDeckListText(
            IReadOnlyList<string> draftedCardIds,
            Func<string, CardDefinitionAsset> cardAssetResolver,
            string emptyText,
            string linePrefix = "")
        {
            if (draftedCardIds == null || draftedCardIds.Count == 0)
            {
                return emptyText;
            }

            var entriesByCardId = new Dictionary<string, DeckListEntry>(StringComparer.Ordinal);

            for (var i = 0; i < draftedCardIds.Count; i++)
            {
                var cardId = draftedCardIds[i];
                if (string.IsNullOrWhiteSpace(cardId))
                {
                    continue;
                }

                if (!entriesByCardId.TryGetValue(cardId, out var entry))
                {
                    var cardAsset = cardAssetResolver != null ? cardAssetResolver(cardId) : null;
                    entry = new DeckListEntry
                    {
                        CardId = cardId,
                        DisplayName = cardAsset != null && !string.IsNullOrWhiteSpace(cardAsset.DisplayName)
                            ? cardAsset.DisplayName
                            : cardId,
                        Cost = cardAsset != null ? cardAsset.Cost : default,
                        Count = 0,
                    };

                    entriesByCardId.Add(cardId, entry);
                }

                entry.Count += 1;
            }

            if (entriesByCardId.Count == 0)
            {
                return emptyText;
            }

            var entries = new List<DeckListEntry>(entriesByCardId.Values);
            entries.Sort(CompareEntries);

            var lines = new List<string>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                lines.Add($"{linePrefix}{entry.Count}x {entry.DisplayName}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static int CompareEntries(DeckListEntry left, DeckListEntry right)
        {
            var compare = CompareDescending(GetTotalCost(left.Cost), GetTotalCost(right.Cost));
            if (compare != 0)
            {
                return compare;
            }

            compare = CompareDescending(left.Cost.Gold, right.Cost.Gold);
            if (compare != 0)
            {
                return compare;
            }

            compare = CompareDescending(left.Cost.Mana, right.Cost.Mana);
            if (compare != 0)
            {
                return compare;
            }

            compare = CompareDescending(left.Cost.Qi, right.Cost.Qi);
            if (compare != 0)
            {
                return compare;
            }

            compare = CompareDescending(left.Cost.Power, right.Cost.Power);
            if (compare != 0)
            {
                return compare;
            }

            compare = string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCulture);
            if (compare != 0)
            {
                return compare;
            }

            return string.Compare(left.CardId, right.CardId, StringComparison.Ordinal);
        }

        private static int GetTotalCost(ResourceSetData cost)
        {
            return cost.Mana + cost.Qi + cost.Power + cost.Gold;
        }

        private static int CompareDescending(int left, int right)
        {
            return right.CompareTo(left);
        }
    }
}
