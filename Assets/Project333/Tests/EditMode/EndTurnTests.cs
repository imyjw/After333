using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class EndTurnTests
    {
        [Test]
        public void EndTurn_WhenCalledDuringMain_AdvancesToOpponentTurnStart()
        {
            var battleState = CreateBattleStateInMainPhase(PlayerId.Player);
            var service = new EndTurnService();

            service.EndTurn(battleState);

            Assert.That(battleState.ActivePlayerId, Is.EqualTo(PlayerId.AI));
            Assert.That(battleState.TurnNumber, Is.EqualTo(2));
            Assert.That(battleState.Phase, Is.EqualTo(PhaseType.TurnStart));
            Assert.That(battleState.IsEnded, Is.False);
        }

        [Test]
        public void EndTurn_WhenPhaseIsNotMain_Throws()
        {
            var setupService = new BattleSetupService();
            var battleState = setupService.CreateInitialState(CreateRequest(PlayerId.Player));
            var service = new EndTurnService();

            var exception = Assert.Throws<System.InvalidOperationException>(() => service.EndTurn(battleState));

            Assert.That(exception!.Message, Is.EqualTo("Turns can only end during the Main phase."));
        }

        private static BattleState CreateBattleStateInMainPhase(PlayerId firstPlayerId)
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var turnStartService = new TurnStartService();
            var battleState = setupService.CreateInitialState(CreateRequest(firstPlayerId));

            mulliganService.PassMulligan(battleState, PlayerId.Player);
            turnStartService.ResolveTurnStart(battleState);

            return battleState;
        }

        private static BattleSetupRequest CreateRequest(PlayerId firstPlayerId)
        {
            return new BattleSetupRequest(
                playerDeckCardIds: CreateDeck("P"),
                aiDeckCardIds: CreateDeck("A"),
                firstPlayerId: firstPlayerId);
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
