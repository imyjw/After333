using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Presentation.Draft;

namespace Project333.Tests.EditMode
{
    public sealed class RunRewardDisplayContentTests
    {
        [Test]
        public void ReceiptAmountsRemainSeparateFromCurrentWalletBalance()
        {
            var content = RunRewardDisplayContent.Build(new RewardGrantDto
            {
                ResourceGoldDelta = 99,
                TicketDelta = 2,
            }, 33, 2, 12345);

            Assert.AreEqual("런 보상", content.Title);
            Assert.AreEqual("최종 기록  33승 · 2패", content.RecordText);
            Assert.AreEqual(99, content.GoldAmount);
            Assert.AreEqual("+99", content.GoldText);
            Assert.AreEqual(2, content.TicketAmount);
            Assert.AreEqual("+2", content.TicketText);
            Assert.AreEqual(12345, content.CurrentGold);
            Assert.AreEqual("보유 골드  12,345", content.CurrentGoldText);
            Assert.IsEmpty(content.EmptyMessage);
        }

        [Test]
        public void DuplicateCardIdsAreMergedWithoutMergingDifferentCardsWithSameName()
        {
            var content = RunRewardDisplayContent.Build(new RewardGrantDto
            {
                CardRewardItems = new[]
                {
                    Item("OrcWarrior", 2), Item(" orcwarrior ", 3), Item("OrcBerserker", 1),
                },
            }, 2, 3, 6, _ => "오크");

            Assert.AreEqual(6, content.CardTotalCount);
            Assert.AreEqual(2, content.Rows.Count);
            Assert.AreEqual(5, content.Rows.Single(row => row.CardId == "OrcWarrior").Count);
            Assert.IsTrue(content.Rows.All(row => row.IsCard));
            Assert.AreEqual("카드 6장 · 2종", content.CardSummaryText);
        }

        [Test]
        public void InvalidItemsAreIgnoredAndMissingNamesUseCardId()
        {
            var content = RunRewardDisplayContent.Build(new RewardGrantDto
            {
                CardRewardItems = new[]
                {
                    null, Item(null, 4), Item(" ", 2), Item("Zero", 0), Item("Negative", -2),
                    Item("Unknown", 1),
                },
            }, 1, 3, 3, _ => "  ");

            Assert.AreEqual(1, content.Rows.Count);
            Assert.AreEqual("Unknown", content.Rows[0].Label);
            Assert.AreEqual(1, content.CardTotalCount);
        }

        [Test]
        public void ZeroWinRewardHasReadableEmptyStateAndDoesNotInventTickets()
        {
            var content = RunRewardDisplayContent.Build(null, 0, 3, 0);

            Assert.AreEqual("최종 기록  0승 · 3패", content.RecordText);
            Assert.AreEqual("+0", content.GoldText);
            Assert.IsEmpty(content.TicketText);
            Assert.IsEmpty(content.Rows);
            Assert.AreEqual("획득 카드 없음", content.CardSummaryText);
            Assert.AreEqual("이번 런에서 획득한 보상이 없습니다.", content.EmptyMessage);
        }

        [Test]
        public void CamelCaseReceiptAndOptionalPacksRemainVisibleAfterCardRows()
        {
            var content = RunRewardDisplayContent.Build(new RewardGrantDto
            {
                resourceGoldDelta = 6,
                ticketDelta = 1,
                cardRewardItems = new[] { new RewardItemDto { id = "Zeta", count = 2 } },
                packRewardItems = new[] { Item("Alpha", 1), Item("ALPHA", 2) },
            }, 2, 3, 9);

            Assert.AreEqual(6, content.GoldAmount);
            Assert.AreEqual(1, content.TicketAmount);
            Assert.AreEqual(2, content.CardTotalCount);
            Assert.AreEqual(2, content.Rows.Count);
            Assert.IsTrue(content.Rows[0].IsCard);
            Assert.IsFalse(content.Rows[1].IsCard);
            Assert.AreEqual(3, content.Rows[1].Count);
        }

        [Test]
        public void AllNinetyNineCopiesAcrossFortyOneKindsRemainVisible()
        {
            var items = Enumerable.Range(0, 99)
                .Select(index => Item($"Card{index % 41:D2}", 1)).ToArray();
            var content = RunRewardDisplayContent.Build(new RewardGrantDto
            {
                ResourceGoldDelta = 99,
                CardRewardItems = items,
            }, 33, 0, 99, id => "길이가 아주 긴 카드 이름 " + id);

            Assert.AreEqual(99, content.CardTotalCount);
            Assert.AreEqual(41, content.Rows.Count);
            Assert.AreEqual(99, content.Rows.Sum(row => row.Count));
            Assert.AreEqual("카드 99장 · 41종", content.CardSummaryText);
            Assert.IsTrue(content.Rows.All(row => row.Label.StartsWith("길이가 아주 긴 카드 이름 ")));
            CollectionAssert.AreEqual(content.Rows.Select(row => row.Label).OrderBy(name => name, StringComparer.Ordinal),
                content.Rows.Select(row => row.Label));
        }

        [Test]
        public void LiteralMarkupCharactersArePreservedForNonRichTextLabels()
        {
            const string label = "<size=999><color=red>카드</color></size>";
            var content = RunRewardDisplayContent.Build(new RewardGrantDto
            {
                CardRewardItems = new[] { Item("Literal", 1) },
            }, 1, 3, 3, _ => label);

            Assert.AreEqual(label, content.Rows[0].Label);
        }

        [Test]
        public void CountsUseLongWhenMergedAndCurrencyFormattingIsCultureIndependent()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                var content = RunRewardDisplayContent.Build(new RewardGrantDto
                {
                    ResourceGoldDelta = 12345,
                    CardRewardItems = new[] { Item("Large", int.MaxValue), Item("large", int.MaxValue) },
                }, 33, 0, 9876543);

                Assert.AreEqual(4294967294L, content.CardTotalCount);
                Assert.AreEqual(4294967294L, content.Rows[0].Count);
                Assert.AreEqual("+12,345", content.GoldText);
                Assert.AreEqual("보유 골드  9,876,543", content.CurrentGoldText);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        private static RewardItemDto Item(string id, int count)
        {
            return new RewardItemDto { Id = id, Count = count };
        }
    }
}
