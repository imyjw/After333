using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BattleOutcomeFormatterTests
    {
        [Test]
        public void Format_WhenBattleStateIsNull_ReturnsEmptyText()
        {
            var outcome = BattleOutcomeFormatter.Format(null);

            Assert.That(outcome, Is.Empty);
        }

        [Test]
        public void Format_WhenBattleIsStillRunning_ReturnsEmptyText()
        {
            var battleState = CreateBattleState();

            var outcome = BattleOutcomeFormatter.Format(battleState);

            Assert.That(outcome, Is.Empty);
        }

        [Test]
        public void Format_WhenPlayerWins_ReturnsVictory()
        {
            var battleState = CreateBattleState();
            battleState.EndBattle(PlayerId.Player);

            var outcome = BattleOutcomeFormatter.Format(battleState);

            Assert.That(outcome, Is.EqualTo("Victory"));
        }

        [Test]
        public void Format_WhenAiWins_ReturnsDefeat()
        {
            var battleState = CreateBattleState();
            battleState.EndBattle(PlayerId.AI);

            var outcome = BattleOutcomeFormatter.Format(battleState);

            Assert.That(outcome, Is.EqualTo("Defeat"));
        }

        private static BattleState CreateBattleState()
        {
            return new BattleSetupService().CreateInitialState(
                new BattleSetupRequest(
                    BattleDebugDeckFactory.CreateDeck("P", BattleDebugDeckFactory.MinimumDeckSize),
                    BattleDebugDeckFactory.CreateDeck("A", BattleDebugDeckFactory.MinimumDeckSize),
                    PlayerId.Player));
        }
    }
}
