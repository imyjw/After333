using System.Collections.Generic;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class TurnStartServiceTests
    {
        [Test]
        public void ResolveTurnStart_WhenHandAlreadyHasTenCards_RemovesTheNextDraw()
        {
            var battleState = CreateBattleState();
            var turnStartService = new TurnStartService();

            foreach (var cardId in new List<string>(battleState.Player.Hand.CardIds))
            {
                battleState.Player.Hand.Remove(cardId);
            }

            for (var i = 0; i < 10; i++)
            {
                battleState.Player.Hand.Add($"Hand-{i:00}");
            }

            var deckCountBefore = battleState.Player.Deck.Count;
            var discardCountBefore = battleState.Player.Discard.Count;

            battleState.SetPhase(PhaseType.TurnStart);
            turnStartService.ResolveTurnStart(battleState);

            Assert.That(battleState.Player.Hand.Count, Is.EqualTo(10));
            Assert.That(battleState.Player.Deck.Count, Is.EqualTo(deckCountBefore - 1));
            Assert.That(battleState.Player.Discard.Count, Is.EqualTo(discardCountBefore));
        }

        [Test]
        public void PlayerState_MaxHandSizeBonus_IsClampedToThirteen()
        {
            var battleState = CreateBattleState();

            battleState.Player.IncreaseMaxHandSizeBonus(1);
            battleState.Player.IncreaseMaxHandSizeBonus(1);
            battleState.Player.IncreaseMaxHandSizeBonus(1);
            battleState.Player.IncreaseMaxHandSizeBonus(1);
            battleState.Player.IncreaseMaxHandSizeBonus(1);

            Assert.That(battleState.Player.MaxHandSize, Is.EqualTo(13));
            Assert.That(battleState.Player.MaxHandSizeBonus, Is.EqualTo(3));
        }

        private static BattleState CreateBattleState()
        {
            var setupService = new BattleSetupService();
            var mulliganService = new MulliganService();
            var battleState = setupService.CreateInitialState(
                new BattleSetupRequest(CreateDeck("P"), CreateDeck("A"), PlayerId.Player));

            mulliganService.PassMulligan(battleState, PlayerId.Player);
            return battleState;
        }

        private static IReadOnlyList<string> CreateDeck(string prefix)
        {
            var cardIds = new List<string>();
            for (var i = 0; i < 20; i++)
            {
                cardIds.Add($"{prefix}-{i:00}");
            }

            return cardIds;
        }
    }
}
