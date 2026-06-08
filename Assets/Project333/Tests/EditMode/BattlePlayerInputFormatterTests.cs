using NUnit.Framework;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BattlePlayerInputFormatterTests
    {
        [Test]
        public void FormatSelectedCard_WhenEmpty_ReturnsNoneLabel()
        {
            var result = BattlePlayerInputFormatter.FormatSelectedCard(string.Empty);

            Assert.That(result, Is.EqualTo("Selected Card: None"));
        }

        [Test]
        public void FormatSelectedCard_WhenCardExists_ReturnsCardLabel()
        {
            var result = BattlePlayerInputFormatter.FormatSelectedCard("sample_firebolt");

            Assert.That(result, Is.EqualTo("Selected Card: sample_firebolt"));
        }

        [Test]
        public void FormatSelectedOccupant_WhenNoneSelected_ReturnsNoneLabel()
        {
            var result = BattlePlayerInputFormatter.FormatSelectedOccupant(false, default);

            Assert.That(result, Is.EqualTo("Selected Occupant: None"));
        }

        [Test]
        public void FormatSelectedOccupant_WhenSelected_ReturnsCoordLabel()
        {
            var result = BattlePlayerInputFormatter.FormatSelectedOccupant(true, new TileCoord(3, 1));

            Assert.That(result, Is.EqualTo("Selected Occupant: (3,1)"));
        }

        [Test]
        public void FormatInteractionStatus_WhenEmpty_ReturnsReadyLabel()
        {
            var result = BattlePlayerInputFormatter.FormatInteractionStatus(string.Empty);

            Assert.That(result, Is.EqualTo("Status: Ready"));
        }

        [Test]
        public void FormatInteractionStatus_WhenStatusExists_ReturnsStatusLabel()
        {
            var result = BattlePlayerInputFormatter.FormatInteractionStatus("Moved unit from (2,0) to (4,0).");

            Assert.That(result, Is.EqualTo("Status: Moved unit from (2,0) to (4,0)."));
        }
    }
}
