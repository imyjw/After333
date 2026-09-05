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
            public CardRarity Rarity;
            public int Count;
        }

        public static string BuildDeckListText(
            IReadOnlyList<string> draftedCardIds,
            Func<string, CardDefinitionAsset> cardAssetResolver,
            string emptyText,
            string linePrefix = "",
            bool includeCardDetails = false)
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
                        Rarity = cardAsset != null ? cardAsset.Rarity : CardRarity.Common,
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
                var detailText = includeCardDetails
                    ? $"    {FormatCost(entry.Cost)} | {FormatRarityShort(entry.Rarity)}"
                    : string.Empty;
                lines.Add($"{linePrefix}{entry.Count}x {entry.DisplayName}{detailText}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public static string BuildDeckBuildingPanelText(
            IReadOnlyList<string> draftedCardIds,
            Func<string, CardDefinitionAsset> cardAssetResolver,
            string emptyText)
        {
            if (draftedCardIds == null || draftedCardIds.Count == 0)
            {
                return emptyText;
            }

            var summaryText = BuildDeckDistributionSummary(draftedCardIds, cardAssetResolver);
            var listText = BuildDeckListText(
                draftedCardIds,
                cardAssetResolver,
                emptyText,
                "- ",
                includeCardDetails: true);

            return $"{summaryText}{Environment.NewLine}{Environment.NewLine}CARD LIST{Environment.NewLine}{listText}";
        }

        private static string BuildDeckDistributionSummary(
            IReadOnlyList<string> draftedCardIds,
            Func<string, CardDefinitionAsset> cardAssetResolver)
        {
            var totalCards = draftedCardIds?.Count ?? 0;
            var unitCount = 0;
            var buildingCount = 0;
            var spellCount = 0;
            var unknownTypeCount = 0;
            var rarityCounts = new int[5];
            var costBuckets = new int[8];
            var totalMana = 0;
            var totalQi = 0;
            var totalPower = 0;
            var totalGold = 0;

            if (draftedCardIds != null)
            {
                for (var i = 0; i < draftedCardIds.Count; i++)
                {
                    var cardId = draftedCardIds[i];
                    if (string.IsNullOrWhiteSpace(cardId))
                    {
                        continue;
                    }

                    var cardAsset = cardAssetResolver != null ? cardAssetResolver(cardId) : null;
                    if (cardAsset is UnitCardDefinitionAsset)
                    {
                        unitCount += 1;
                    }
                    else if (cardAsset is BuildingCardDefinitionAsset)
                    {
                        buildingCount += 1;
                    }
                    else if (cardAsset is DamageSpellCardDefinitionAsset ||
                             cardAsset is PersistentResourceSpellCardDefinitionAsset ||
                             cardAsset is ScriptedSpellCardDefinitionAsset)
                    {
                        spellCount += 1;
                    }
                    else
                    {
                        unknownTypeCount += 1;
                    }

                    if (cardAsset != null)
                    {
                        var rarityIndex = Math.Max(0, Math.Min(rarityCounts.Length - 1, (int)cardAsset.Rarity));
                        rarityCounts[rarityIndex] += 1;
                    }

                    var cost = cardAsset != null ? cardAsset.Cost : default;
                    var totalCost = GetTotalCost(cost);
                    var bucketIndex = Math.Min(costBuckets.Length - 1, Math.Max(0, totalCost));
                    costBuckets[bucketIndex] += 1;

                    totalMana += cost.Mana;
                    totalQi += cost.Qi;
                    totalPower += cost.Power;
                    totalGold += cost.Gold;
                }
            }

            var typeText = unknownTypeCount > 0
                ? $"Type    Unit {unitCount} | Building {buildingCount} | Spell {spellCount} | Unknown {unknownTypeCount}"
                : $"Type    Unit {unitCount} | Building {buildingCount} | Spell {spellCount}";
            var rarityText =
                $"Rarity  L {rarityCounts[(int)CardRarity.Legendary]} | U {rarityCounts[(int)CardRarity.Unique]} | R {rarityCounts[(int)CardRarity.Rare]} | UC {rarityCounts[(int)CardRarity.Uncommon]} | C {rarityCounts[(int)CardRarity.Common]}";
            var costCurveText =
                $"Curve   0:{costBuckets[0]} | 1:{costBuckets[1]} | 2:{costBuckets[2]} | 3:{costBuckets[3]} | 4:{costBuckets[4]} | 5:{costBuckets[5]} | 6:{costBuckets[6]} | 7+:{costBuckets[7]}";
            var resourceText = $"CostSum M {totalMana} | Q {totalQi} | P {totalPower} | G {totalGold}";

            return string.Join(Environment.NewLine, new[]
            {
                $"DECK {totalCards}/33   Left {Math.Max(0, 33 - totalCards)}",
                typeText,
                rarityText,
                costCurveText,
                resourceText
            });
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

        private static string FormatCost(ResourceSetData cost)
        {
            var parts = new List<string>();
            if (cost.Mana > 0)
            {
                parts.Add($"{cost.Mana}M");
            }

            if (cost.Qi > 0)
            {
                parts.Add($"{cost.Qi}Q");
            }

            if (cost.Power > 0)
            {
                parts.Add($"{cost.Power}P");
            }

            if (cost.Gold > 0)
            {
                parts.Add($"{cost.Gold}G");
            }

            return parts.Count == 0 ? "Free" : string.Join(" ", parts);
        }

        private static string FormatRarityShort(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Legendary:
                    return "L";
                case CardRarity.Unique:
                    return "U";
                case CardRarity.Rare:
                    return "R";
                case CardRarity.Uncommon:
                    return "UC";
                default:
                    return "C";
            }
        }

        private static int CompareDescending(int left, int right)
        {
            return right.CompareTo(left);
        }
    }
}
