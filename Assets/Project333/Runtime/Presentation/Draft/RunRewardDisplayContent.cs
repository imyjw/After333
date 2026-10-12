using System;
using System.Collections.Generic;
using System.Globalization;
using Project333.Runtime.Application.Accounts;

namespace Project333.Runtime.Presentation.Draft
{
    // Text is literal: consumers must disable rich text when displaying server/catalog names.
    public sealed class RunRewardDisplayContent
    {
        public string Title { get; private set; }
        public string RecordText { get; private set; }
        public long GoldAmount { get; private set; }
        public int TicketAmount { get; private set; }
        public long CurrentGold { get; private set; }
        public string GoldText { get; private set; }
        public string TicketText { get; private set; }
        public string CurrentGoldText { get; private set; }
        public long CardTotalCount { get; private set; }
        public string CardSummaryText { get; private set; }
        public IReadOnlyList<RunRewardDisplayRow> Rows { get; private set; }
        public string EmptyMessage { get; private set; }

        public static RunRewardDisplayContent Build(
            RewardGrantDto reward,
            int wins,
            int losses,
            long currentGold,
            Func<string, string> resolveName = null)
        {
            var cardRows = CollectRows(reward?.ResolvedCardRewardItems, true, resolveName);
            var packRows = CollectRows(reward?.ResolvedPackRewardItems, false, resolveName);
            long cardCount = 0;
            foreach (var row in cardRows)
            {
                cardCount += row.Count;
            }

            var rows = new List<RunRewardDisplayRow>(cardRows.Count + packRows.Count);
            rows.AddRange(cardRows);
            rows.AddRange(packRows);
            var gold = Math.Max(0L, reward?.ResolvedResourceGoldDelta ?? 0L);
            var tickets = Math.Max(0, reward?.ResolvedTicketDelta ?? 0);
            return new RunRewardDisplayContent
            {
                Title = "런 보상",
                RecordText = $"최종 기록  {Math.Max(0, wins)}승 · {Math.Max(0, losses)}패",
                GoldAmount = gold,
                TicketAmount = tickets,
                CurrentGold = currentGold,
                GoldText = "+" + FormatNumber(gold),
                TicketText = tickets > 0 ? "+" + FormatNumber(tickets) : string.Empty,
                CurrentGoldText = "보유 골드  " + FormatNumber(currentGold),
                CardTotalCount = cardCount,
                CardSummaryText = cardCount > 0
                    ? $"카드 {FormatNumber(cardCount)}장 · {cardRows.Count}종"
                    : "획득 카드 없음",
                Rows = rows.AsReadOnly(),
                EmptyMessage = gold == 0 && tickets == 0 && rows.Count == 0
                    ? "이번 런에서 획득한 보상이 없습니다."
                    : string.Empty,
            };
        }

        private static List<RunRewardDisplayRow> CollectRows(
            RewardItemDto[] items,
            bool isCard,
            Func<string, string> resolveName)
        {
            var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (items != null)
            {
                foreach (var item in items)
                {
                    var id = item?.ResolvedId?.Trim();
                    var count = item?.ResolvedCount ?? 0;
                    if (string.IsNullOrWhiteSpace(id) || count <= 0)
                    {
                        continue;
                    }

                    counts.TryGetValue(id, out var previous);
                    counts[id] = previous + count;
                }
            }

            var rows = new List<RunRewardDisplayRow>(counts.Count);
            foreach (var pair in counts)
            {
                var label = resolveName?.Invoke(pair.Key);
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = pair.Key;
                }

                rows.Add(new RunRewardDisplayRow(pair.Key, label, pair.Value, isCard));
            }

            rows.Sort((left, right) =>
            {
                var labelOrder = string.Compare(left.Label, right.Label, StringComparison.Ordinal);
                return labelOrder != 0
                    ? labelOrder
                    : string.Compare(left.CardId, right.CardId, StringComparison.OrdinalIgnoreCase);
            });
            return rows;
        }

        private static string FormatNumber(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    public sealed class RunRewardDisplayRow
    {
        public string CardId { get; }
        public string Label { get; }
        public long Count { get; }
        public bool IsCard { get; }

        internal RunRewardDisplayRow(string cardId, string label, long count, bool isCard)
        {
            CardId = cardId;
            Label = label;
            Count = count;
            IsCard = isCard;
        }
    }
}
