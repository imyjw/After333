using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;

namespace Project333.Tests.EditMode
{
    public sealed class CombatLogFormatterTests
    {
        [Test]
        public void FormatBattleStarted_UsesFirstPlayer()
        {
            Assert.That(
                CombatLogFormatter.FormatBattleStarted(PlayerId.Player),
                Is.EqualTo("Battle started. Player acts first."));
        }

        [Test]
        public void FormatCommand_ForAttackCommand_ReturnsAttackText()
        {
            var text = CombatLogFormatter.FormatCommand(
                PlayerId.AI,
                new AttackCommand(new TileCoord(2, 1), new TileCoord(2, 1)));

            Assert.That(text, Is.EqualTo("AI attacked from (2,1) to (2,1)."));
        }

        [Test]
        public void FormatSwap_ReturnsSwapText()
        {
            Assert.That(
                CombatLogFormatter.FormatSwap(new TileCoord(1, 0), new TileCoord(3, 1)),
                Is.EqualTo("Swapped occupants at (1,0) and (3,1)."));
        }
    }
}
