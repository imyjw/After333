using System;
using System.Collections.Generic;

namespace Project333.Runtime.Presentation.Draft
{
    public enum DraftBattleLaunchMode
    {
        Local,
        OnlineServerAi,
        OnlineMatchmaking
    }

    public enum DraftBattleOutcome
    {
        Victory,
        Defeat,
        Draw
    }

    public static class DraftRunSessionState
    {
        public const int RunWinLimit = 33;
        public const int RunLossLimit = 3;
        private static readonly List<string> DraftedDeckCardIds = new List<string>();
        private static readonly List<string> CompletedDraftDeckCardIds = new List<string>();

        public static IReadOnlyList<string> CurrentDraftDeckCardIds => DraftedDeckCardIds;

        public static IReadOnlyList<string> LastCompletedDraftDeckCardIds => CompletedDraftDeckCardIds;

        public static string DraftSceneName { get; private set; } = "Draft_VSlice";

        public static string BattleSceneName { get; private set; } = "Battle_VSlice";

        public static int Wins { get; private set; }

        public static int Losses { get; private set; }

        public static string LastBattleOutcomeText { get; private set; } = string.Empty;

        public static string ServerRunId { get; private set; } = string.Empty;

        public static string ServerDeckId { get; private set; } = string.Empty;

        public static bool HasPendingBattleStart { get; private set; }

        public static DraftBattleLaunchMode PendingBattleLaunchMode { get; private set; } = DraftBattleLaunchMode.Local;

        public static bool PendingBattleStartAllowsMissingDeck { get; private set; }

        public static bool PendingBattleStartIsPvpReconnect { get; private set; }

        private static bool LastConsumedBattleStartWasPvpReconnect { get; set; }

        public static bool HasDraftedDeckReady => DraftedDeckCardIds.Count == 33 || CompletedDraftDeckCardIds.Count == 33;

        public static bool HasRunEnded => Wins >= RunWinLimit || Losses >= RunLossLimit;

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
            CompletedDraftDeckCardIds.Clear();
            HasPendingBattleStart = false;
            PendingBattleLaunchMode = DraftBattleLaunchMode.Local;
            PendingBattleStartAllowsMissingDeck = false;
            PendingBattleStartIsPvpReconnect = false;
            LastConsumedBattleStartWasPvpReconnect = false;
            ServerRunId = string.Empty;
            ServerDeckId = string.Empty;
            Wins = 0;
            Losses = 0;
            LastBattleOutcomeText = string.Empty;
        }

        public static void ResetSessionState()
        {
            ResetForNewDraft();
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

            if (DraftedDeckCardIds.Count == 33)
            {
                CopyCardIds(DraftedDeckCardIds, CompletedDraftDeckCardIds);
            }
        }

        public static void QueueBattleStart(DraftBattleLaunchMode launchMode = DraftBattleLaunchMode.Local)
        {
            HasPendingBattleStart = HasDraftedDeckReady;
            PendingBattleLaunchMode = HasPendingBattleStart ? launchMode : DraftBattleLaunchMode.Local;
            PendingBattleStartAllowsMissingDeck = false;
            PendingBattleStartIsPvpReconnect = false;
        }

        public static void QueuePvpReconnectBattleStart()
        {
            HasPendingBattleStart = true;
            PendingBattleLaunchMode = DraftBattleLaunchMode.OnlineMatchmaking;
            PendingBattleStartAllowsMissingDeck = true;
            PendingBattleStartIsPvpReconnect = true;
        }

        public static void SetServerRunDeckMetadata(string runId, string deckId)
        {
            if (!string.IsNullOrWhiteSpace(runId))
            {
                ServerRunId = runId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(deckId))
            {
                ServerDeckId = deckId.Trim();
            }
        }

        public static bool TryConsumePendingBattleStart(out IReadOnlyList<string> draftedDeckCardIds)
        {
            return TryConsumePendingBattleStart(out draftedDeckCardIds, out _);
        }

        public static bool TryConsumePendingBattleStart(
            out IReadOnlyList<string> draftedDeckCardIds,
            out DraftBattleLaunchMode launchMode)
        {
            if (!HasPendingBattleStart || (!HasDraftedDeckReady && !PendingBattleStartAllowsMissingDeck))
            {
                draftedDeckCardIds = Array.Empty<string>();
                launchMode = DraftBattleLaunchMode.Local;
                LastConsumedBattleStartWasPvpReconnect = false;
                return false;
            }

            HasPendingBattleStart = false;
            launchMode = PendingBattleLaunchMode;
            PendingBattleLaunchMode = DraftBattleLaunchMode.Local;
            var allowMissingDeck = PendingBattleStartAllowsMissingDeck;
            PendingBattleStartAllowsMissingDeck = false;
            LastConsumedBattleStartWasPvpReconnect = PendingBattleStartIsPvpReconnect;
            PendingBattleStartIsPvpReconnect = false;
            var sourceDeck = DraftedDeckCardIds.Count == 33
                ? DraftedDeckCardIds
                : CompletedDraftDeckCardIds;
            if (sourceDeck.Count == 0 && allowMissingDeck)
            {
                draftedDeckCardIds = Array.Empty<string>();
                return true;
            }

            draftedDeckCardIds = new List<string>(sourceDeck);
            return true;
        }

        public static bool ConsumeLastBattleStartWasPvpReconnect()
        {
            var wasReconnect = LastConsumedBattleStartWasPvpReconnect;
            LastConsumedBattleStartWasPvpReconnect = false;
            return wasReconnect;
        }

        public static void RecordBattleResult(bool playerWon)
        {
            RestoreCurrentDraftDeckFromCompletedDeckIfNeeded();

            if (playerWon)
            {
                Wins += 1;
                SetLastBattleOutcome(DraftBattleOutcome.Victory);
                return;
            }

            Losses += 1;
            SetLastBattleOutcome(DraftBattleOutcome.Defeat);
        }

        public static void RecordBattleDraw()
        {
            RestoreCurrentDraftDeckFromCompletedDeckIfNeeded();
            SetLastBattleOutcome(DraftBattleOutcome.Draw);
        }

        public static void SetLastBattleOutcome(DraftBattleOutcome outcome)
        {
            LastBattleOutcomeText = outcome.ToString();
        }

        public static void ApplyServerRunRecord(int wins, int losses)
        {
            Wins = Math.Max(0, wins);
            Losses = Math.Max(0, losses);
        }

        public static bool TryApplyAuthoritativeServerRunRecord(int wins, int losses, bool isCompletedRun)
        {
            var normalizedWins = Math.Max(0, wins);
            var normalizedLosses = Math.Max(0, losses);
            if (!isCompletedRun && (normalizedWins < Wins || normalizedLosses < Losses))
            {
                return false;
            }

            Wins = normalizedWins;
            Losses = normalizedLosses;
            return true;
        }

        public static void RestoreCurrentDraftDeckFromCompletedDeckIfNeeded()
        {
            if (DraftedDeckCardIds.Count == 33 || CompletedDraftDeckCardIds.Count != 33)
            {
                return;
            }

            CopyCardIds(CompletedDraftDeckCardIds, DraftedDeckCardIds);
        }

        private static void CopyCardIds(IReadOnlyList<string> source, List<string> destination)
        {
            destination.Clear();
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var cardId = source[i];
                if (!string.IsNullOrWhiteSpace(cardId))
                {
                    destination.Add(cardId);
                }
            }
        }
    }
}
