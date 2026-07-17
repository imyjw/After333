using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Application.Services
{
    public sealed class MulliganService
    {
        public void ApplyMulligan(
            BattleState battleState,
            PlayerId playerId,
            IEnumerable<string> selectedCardIds,
            IDeckShuffler deckShuffler)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (selectedCardIds == null)
            {
                throw new ArgumentNullException(nameof(selectedCardIds));
            }

            if (deckShuffler == null)
            {
                throw new ArgumentNullException(nameof(deckShuffler));
            }

            EnsureMulliganPhase(battleState);

            var playerState = battleState.GetPlayer(playerId);
            EnsureMulliganAvailable(playerState);

            var selectedCards = new List<string>(selectedCardIds);
            ValidateSelectedCards(playerState, selectedCards);

            foreach (var selectedCardId in selectedCards)
            {
                playerState.Hand.Remove(selectedCardId);
            }

            if (selectedCards.Count > 0)
            {
                // Keep the returned copies out of the deck while drawing replacements.
                // Another copy of the same card type may still be drawn from the deck.
                DrawExactCards(playerState, selectedCards.Count);
                playerState.Deck.AddRangeToTop(selectedCards);
                deckShuffler.Shuffle(playerState.Deck);
            }

            playerState.MarkMulliganUsed();
            AdvanceToFirstTurnIfReady(battleState);
        }

        public void PassMulligan(BattleState battleState, PlayerId playerId)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            EnsureMulliganPhase(battleState);

            var playerState = battleState.GetPlayer(playerId);
            EnsureMulliganAvailable(playerState);

            playerState.MarkMulliganUsed();
            AdvanceToFirstTurnIfReady(battleState);
        }

        private static void EnsureMulliganPhase(BattleState battleState)
        {
            if (battleState.Phase != PhaseType.Mulligan)
            {
                throw new InvalidOperationException("Mulligan can only be applied during the mulligan phase.");
            }
        }

        private static void EnsureMulliganAvailable(PlayerState playerState)
        {
            if (playerState.HasUsedMulligan)
            {
                throw new InvalidOperationException("This player has already used their mulligan.");
            }
        }

        private static void ValidateSelectedCards(PlayerState playerState, List<string> selectedCards)
        {
            var handSnapshot = new List<string>(playerState.Hand.CardIds);

            foreach (var selectedCardId in selectedCards)
            {
                if (!handSnapshot.Remove(selectedCardId))
                {
                    throw new InvalidOperationException(
                        "Selected mulligan card does not exist in the player's current hand.");
                }
            }
        }

        private static void AdvanceToFirstTurnIfReady(BattleState battleState)
        {
            if (!battleState.Player.HasUsedMulligan || !battleState.AI.HasUsedMulligan)
            {
                return;
            }

            battleState.StartNextTurn(battleState.ActivePlayerId);
        }

        private static void DrawExactCards(PlayerState playerState, int drawCount)
        {
            for (var i = 0; i < drawCount; i++)
            {
                if (!playerState.Deck.TryDraw(out var cardId))
                {
                    throw new InvalidOperationException(
                        "Deck does not contain enough cards to complete the mulligan redraw.");
                }

                playerState.Hand.Add(cardId);
            }
        }
    }
}
