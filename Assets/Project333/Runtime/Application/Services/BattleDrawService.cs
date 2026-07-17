using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;

namespace Project333.Runtime.Application.Services
{
    public static class BattleDrawService
    {
        private const int FailedDrawDamageStep = 3;

        public static void DrawCards(PlayerState playerState, BattleState battleState, int drawCount)
        {
            if (playerState == null)
            {
                throw new ArgumentNullException(nameof(playerState));
            }

            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (drawCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(drawCount));
            }

            for (var i = 0; i < drawCount && !battleState.IsEnded; i++)
            {
                if (!playerState.Deck.TryDraw(out var cardId))
                {
                    ApplyFailedDrawDamage(playerState, battleState);
                    continue;
                }

                if (playerState.Hand.Count >= playerState.MaxHandSize)
                {
                    continue;
                }

                playerState.Hand.Add(cardId);
            }
        }

        private static void ApplyFailedDrawDamage(PlayerState playerState, BattleState battleState)
        {
            var failedDrawCount = playerState.IncrementFailedDrawCount();
            var damage = failedDrawCount * FailedDrawDamageStep;
            var actualDamage = DamageResolutionRules.ApplyEffectDamage(
                playerState.Master,
                damage,
                DamageType.Fixed);

            BattleValuePopupRecorder.RecordDamage(
                battleState,
                playerState.Master,
                actualDamage,
                BattleValueChangeCause.DeckExhaustion,
                DamageType.Fixed,
                playerState.Id,
                playerState.Master.RuntimeId,
                playerState.Master.CardId);

            if (playerState.Master.CurrentHp <= 0)
            {
                battleState.EndBattle(battleState.GetOpponent(playerState.Id).Id);
            }
        }
    }
}
