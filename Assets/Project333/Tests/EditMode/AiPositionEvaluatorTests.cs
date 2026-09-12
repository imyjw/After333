using System;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Resources;

namespace Project333.Tests.EditMode
{
    public sealed class AiPositionEvaluatorTests
    {
        private static BattleState State()
        {
            var deck = Enumerable.Repeat("unknown", 20).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.AI));
            state.SetPhase(PhaseType.Main);
            foreach (var player in new[] { state.Player, state.AI })
            {
                foreach (var id in player.Hand.CardIds.ToArray()) player.Hand.Remove(id);
                player.Resources.Spend(player.Resources.Clone());
                player.Master.RemainingAttacksThisTurn = 0;
            }
            return state;
        }

        private static double Evaluate(BattleState state)
        {
            // Keep the evaluator internal while verifying its exact score independently of search budgets.
            var type = typeof(AiDecisionService).Assembly.GetType(
                "Project333.Runtime.Application.Services.AiPositionEvaluator", throwOnError: true);
            var evaluator = Activator.CreateInstance(type, nonPublic: true);
            return (double)type.GetMethod("Evaluate").Invoke(evaluator, new object[] { state });
        }

        [TestCase(1, 0, 0, 0, 3.0)]
        [TestCase(0, 1, 0, 0, 3.0)]
        [TestCase(0, 0, 1, 0, 2.0)]
        [TestCase(0, 0, 0, 1, 4.0)]
        [TestCase(13, 0, 0, 0, 36.6)]
        [TestCase(0, 13, 0, 0, 36.6)]
        [TestCase(0, 0, 13, 0, 24.4)]
        [TestCase(0, 0, 0, 13, 48.8)]
        [TestCase(1, 1, 1, 1, 12.0)]
        public void Evaluate_HeldResourcesUseRequestedWeightsAndExcessDiscount(
            int mana, int qi, int power, int gold, double expectedValue)
        {
            foreach (var owner in new[] { PlayerId.AI, PlayerId.Player })
            {
                var state = State();
                var before = Evaluate(state);
                state.GetPlayer(owner).Resources.Add(new ResourceSet(mana, qi, power, gold));
                var sign = owner == PlayerId.AI ? 1 : -1;
                Assert.That(Evaluate(state) - before, Is.EqualTo(sign * expectedValue).Within(0.000001), owner.ToString());
            }
        }

        [TestCase(true, 1, 3.0)]
        [TestCase(true, 3, 9.0)]
        [TestCase(false, 1, 7.0)]
        [TestCase(false, 3, 21.0)]
        public void Evaluate_HandValueIsPerCardAndPreservesNormalCardWeight(bool temporary, int count, double expectedValue)
        {
            foreach (var owner in new[] { PlayerId.AI, PlayerId.Player })
            {
                var state = State();
                var before = Evaluate(state);
                for (var i = 0; i < count; i++)
                {
                    if (temporary) state.GetPlayer(owner).Hand.AddTemporaryReplicate("card");
                    else state.GetPlayer(owner).Hand.Add("card");
                }
                var sign = owner == PlayerId.AI ? 1 : -1;
                Assert.That(Evaluate(state) - before, Is.EqualTo(sign * expectedValue).Within(0.000001), owner.ToString());
            }
        }
    }
}
