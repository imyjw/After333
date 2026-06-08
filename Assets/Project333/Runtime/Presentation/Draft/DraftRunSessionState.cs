using System;
using System.Collections.Generic;

namespace Project333.Runtime.Presentation.Draft
{
    public static class DraftRunSessionState
    {
        public const int DefaultStartingTickets = 9;
        private static readonly List<string> DraftedDeckCardIds = new List<string>();

        public static IReadOnlyList<string> CurrentDraftDeckCardIds => DraftedDeckCardIds;

        public static string DraftSceneName { get; private set; } = "Draft_VSlice";

        public static string BattleSceneName { get; private set; } = "Battle_VSlice";

        public static int AvailableTickets { get; private set; } = DefaultStartingTickets;

        public static int Wins { get; private set; }

        public static int Losses { get; private set; }

        public static string LastBattleOutcomeText { get; private set; } = string.Empty;

        public static bool HasPendingBattleStart { get; private set; }

        public static bool HasDraftedDeckReady => DraftedDeckCardIds.Count == 33;

        public static void ConfigureSceneNames(string draftSceneName, string battleSceneName)
        {
            if (!string.IsNullOrWhiteSpace(draftSceneName))
            {
                DraftSceneName = draftSceneName;
            }

            if (!string.IsNullOrWhiteSpace(battleSceneName))
            {
                BattleSceneName = battleSceneName;
            }
        }

        public static void ResetForNewDraft()
        {
            DraftedDeckCardIds.Clear();
            HasPendingBattleStart = false;
            Wins = 0;
            Losses = 0;
            LastBattleOutcomeText = string.Empty;
        }

        public static void ResetSessionState()
        {
            ResetForNewDraft();
            AvailableTickets = DefaultStartingTickets;
        }

        public static bool CanSpendTickets(int ticketCost)
        {
            if (ticketCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticketCost));
            }

            return AvailableTickets >= ticketCost;
        }

        public static bool TrySpendTicketsForNewRun(int ticketCost)
        {
            if (ticketCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticketCost));
            }

            if (AvailableTickets < ticketCost)
            {
                return false;
            }

            AvailableTickets -= ticketCost;
            ResetForNewDraft();
            return true;
        }

        public static void SetDraftDeck(IReadOnlyList<string> draftedDeckCardIds)
        {
            DraftedDeckCardIds.Clear();

            if (draftedDeckCardIds == null)
            {
                return;
            }

            for (var i = 0; i < draftedDeckCardIds.Count; i++)
            {
                var cardId = draftedDeckCardIds[i];
                if (!string.IsNullOrWhiteSpace(cardId))
                {
                    DraftedDeckCardIds.Add(cardId);
                }
            }
        }

        public static void QueueBattleStart()
        {
            HasPendingBattleStart = HasDraftedDeckReady;
        }

        public static bool TryConsumePendingBattleStart(out IReadOnlyList<string> draftedDeckCardIds)
        {
            if (!HasPendingBattleStart || !HasDraftedDeckReady)
            {
                draftedDeckCardIds = Array.Empty<string>();
                return false;
            }

            HasPendingBattleStart = false;
            draftedDeckCardIds = new List<string>(DraftedDeckCardIds);
            return true;
        }

        public static void RecordBattleResult(bool playerWon)
        {
            if (playerWon)
            {
                Wins += 1;
                LastBattleOutcomeText = "Victory";
                return;
            }

            Losses += 1;
            LastBattleOutcomeText = "Defeat";
        }
    }
}
