using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class AiTurnRunnerTests
    {
        [Test]
        public void RunAiTurn_WhenAiHasNoAction_EndsTurn()
        {
            var flowController = CreateFlowController();
            flowController.StartBattle(new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));

            var battleState = flowController.CurrentBattleState;
            battleState.StartNextTurn(PlayerId.AI);
            battleState.SetPhase(PhaseType.Main);
            battleState.AI.Master.IsDrained = true;
            ClearHand(battleState.AI);

            var aiTurnRunner = new AiTurnRunner(new AiDecisionService(new InMemoryCardDefinitionProvider(new CardDefinition[0])));

            aiTurnRunner.RunAiTurn(flowController);

            Assert.That(flowController.CurrentBattleState.ActivePlayerId, Is.EqualTo(PlayerId.Player));
            Assert.That(flowController.CurrentBattleState.Phase, Is.EqualTo(PhaseType.TurnStart));
            Assert.That(flowController.CurrentBattleState.TurnNumber, Is.EqualTo(2));
        }

        private static void ClearHand(PlayerState playerState)
        {
            foreach (var cardId in new List<string>(playerState.Hand.CardIds))
            {
                playerState.Hand.Remove(cardId);
            }
        }

        private static BattleFlowController CreateFlowController()
        {
            var provider = new InMemoryCardDefinitionProvider(new CardDefinition[0]);
            var processor = new BattleCommandProcessor(
                new PlayCardService(provider),
                new SpellService(provider),
                new MoveService(),
                new AttackService(),
                new EndTurnService());

            return new BattleFlowController(
                new BattleSetupService(),
                new MulliganService(),
                new TurnStartService(),
                processor);
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
