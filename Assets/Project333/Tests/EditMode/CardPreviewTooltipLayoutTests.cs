using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class CardPreviewTooltipLayoutTests
    {
        [TestCase(1920f, 1080f, 6, 8, 4)]
        [TestCase(2560f, 1080f, 10, 12, 6)]
        [TestCase(1280f, 720f, 6, 8, 4)]
        [TestCase(1920f, 1080f, 12, 14, 6)]
        [TestCase(800f, 450f, 4, 5, 3)]
        [TestCase(720f, 1280f, 4, 5, 3)]
        [TestCase(1920f, 1080f, 0, 4, 0)]
        [TestCase(1920f, 1080f, 4, 0, 0)]
        [TestCase(1280f, 720f, 0, 0, 0)]
        public void Calculate_KeepsEveryPanelInItsRegionWithoutOverlap(
            float width, float height, int abilityCount, int attachedCount, int influenceCount)
        {
            var layout = CardPreviewTooltipLayout.Calculate(width, height, 440f, 620f,
                abilityCount, attachedCount, influenceCount);
            Assert.That(layout.Abilities.Count, Is.EqualTo(abilityCount));
            Assert.That(layout.Attached.Count, Is.EqualTo(attachedCount));
            Assert.That(layout.Influences.Count, Is.EqualTo(influenceCount));
            var all = new List<CardPreviewTooltipRect> { layout.Card };
            foreach (var rect in layout.Abilities)
            {
                Assert.That(rect.Right <= layout.Card.Left || rect.Left >= layout.Card.Right, Is.True);
                all.Add(rect);
            }
            foreach (var rect in layout.Attached)
            {
                Assert.That(rect.Top, Is.LessThan(layout.Card.Bottom));
                all.Add(rect);
            }
            foreach (var rect in layout.Influences)
            {
                Assert.That(rect.Bottom, Is.GreaterThan(layout.Card.Top));
                all.Add(rect);
            }
            for (var index = 0; index < all.Count; index++)
            {
                var rect = all[index];
                Assert.That(rect.Width, Is.GreaterThan(0));
                Assert.That(rect.Height, Is.GreaterThan(0));
                Assert.That(rect.Left, Is.GreaterThanOrEqualTo(-width * 0.5f - 0.01f));
                Assert.That(rect.Right, Is.LessThanOrEqualTo(width * 0.5f + 0.01f));
                Assert.That(rect.Bottom, Is.GreaterThanOrEqualTo(-height * 0.5f - 0.01f));
                Assert.That(rect.Top, Is.LessThanOrEqualTo(height * 0.5f + 0.01f));
                for (var other = index + 1; other < all.Count; other++)
                {
                    var b = all[other];
                    Assert.That(rect.Right <= b.Left || b.Right <= rect.Left ||
                        rect.Top <= b.Bottom || b.Top <= rect.Bottom, Is.True,
                        $"Overlapping entries {index} and {other}");
                }
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void Calculate_NormalLandscapePreservesFullTextSizeByReducingCard(float width, float height)
        {
            var layout = CardPreviewTooltipLayout.Calculate(width, height, 440f, 620f, 6, 8, 4);
            Assert.That(layout.TextScale, Is.EqualTo(1f));
            Assert.That(layout.Card.Height, Is.GreaterThanOrEqualTo(155f));
        }

        [Test]
        public void Calculate_MeasuresWrappedContentInsteadOfClippingToFixedHeight()
        {
            var layout = CardPreviewTooltipLayout.Calculate(1920f, 1080f, 440f, 620f, 4, 3, 2,
                (zone, index, width, scale) =>
                    zone == CardPreviewTooltipZone.Attached && index == 1 ? 360f * scale : 116f * scale);
            Assert.That(layout.Attached[1].Height, Is.GreaterThanOrEqualTo(360f * layout.TextScale));
            Assert.That(layout.Attached[1].Top, Is.LessThan(layout.Card.Bottom));
            Assert.That(layout.Card.Height, Is.LessThan(620f));
        }

        [Test]
        public void Calculate_BalancesAsymmetricBandsByMovingCard()
        {
            var layout = CardPreviewTooltipLayout.Calculate(1920f, 1080f, 440f, 620f, 2, 10, 1);
            Assert.That(layout.Card.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void Calculate_ReusesMeasurementsAcrossSearchCandidates()
        {
            var keys = new HashSet<string>();
            var calls = 0;
            CardPreviewTooltipLayout.Calculate(1280f, 720f, 440f, 620f, 6, 8, 4,
                (zone, index, width, scale) =>
                {
                    calls++;
                    keys.Add($"{zone}/{index}/{width:R}/{scale:R}");
                    return 130f * scale;
                });
            Assert.That(calls, Is.EqualTo(keys.Count));
        }
    }
}
