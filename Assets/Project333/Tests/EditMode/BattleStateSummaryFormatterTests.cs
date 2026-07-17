using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BattleStateSummaryFormatterTests
    {
        [Test]
        public void Format_WhenBattleStateIsNull_ReturnsFallbackText()
        {
            var summary = BattleStateSummaryFormatter.Format(null);

            Assert.That(summary, Is.EqualTo("No active battle."));
        }

        [Test]
        public void Format_WhenBattleStateExists_IncludesCoreBattleFields()
        {
            var battleState = CreateBattleState();

            var summary = BattleStateSummaryFormatter.Format(battleState);

            Assert.That(summary, Does.Contain("Turn: 1"));
            Assert.That(summary, Does.Contain("Phase: Main"));
            Assert.That(summary, Does.Contain("Active Player: Player"));
            Assert.That(summary, Does.Contain("Player Master HP: 333/333"));
            Assert.That(summary, Does.Contain("AI Master HP: 333/333"));
            Assert.That(summary, Does.Contain("Player Resources: M:0 Q:0 P:0 G:3"));
            Assert.That(summary, Does.Contain("Persistent Effects: 0"));
            Assert.That(summary, Does.Contain("Battle Ended: False"));
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var turnStartService = new TurnStartService();
            var battleState = setupService.CreateInitialState(
                new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));

            mulliganService.PassMulligan(battleState, PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            return new List<string>
            {
                prefix + "-00",
                prefix + "-01",
                prefix + "-02",
                prefix + "-03",
                prefix + "-04",
                prefix + "-05",
                prefix + "-06",
                prefix + "-07",
                prefix + "-08",
                prefix + "-09",
            };
        }
    }
}
