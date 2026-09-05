using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BattleRichTextStylerTests
    {
        [Test]
        public void StyleTile_DoesNotEmitLiteralAlphaClosingTag()
        {
            var styled = BattleRichTextStyler.StyleTile(
                "AI (0,1)",
                "Empty",
                string.Empty,
                false);

            Assert.That(styled, Does.Not.Contain("</alpha>"));
            Assert.That(styled, Does.Contain("</color>"));
        }

        [Test]
        public void StyleHandCard_DoesNotEmitLiteralAlphaClosingTag()
        {
            var styled = BattleRichTextStyler.StyleHandCard(
                "Slot 0: sample_firebolt",
                "[Selected]",
                true,
                "Empty");

            Assert.That(styled, Does.Not.Contain("</alpha>"));
            Assert.That(styled, Does.Contain("</color>"));
        }
    }
}
