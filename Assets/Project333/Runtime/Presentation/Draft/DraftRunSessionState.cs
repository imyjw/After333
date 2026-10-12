using System;
using System.Collections.Generic;
using Project333.Runtime.Application.Accounts;

namespace Project333.Runtime.Presentation.Draft
{
    public enum DraftBattleLaunchMode
    {
        Local,
        OnlineServerAi,
        OnlineMatchmaking,
        OnlineAiChallenge
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
        public static string PendingAiChallengeResultAttemptId { get; private set; } = string.Empty;
        public static bool ActiveBattleIsReconnect { get; private set; }
        private static bool PendingExplicitReconnect;
        public static string ActiveBattleGameMode { get; private set; } = "run";
        public static string ActiveAiChallengeAttemptId { get; private set; } = string.Empty;
        private static string PendingAiChallengeAttemptId = string.Empty;
        private static string PendingPveReconnectMatchId = string.Empty;
        private static DateTimeOffset PendingPveReconnectDeadlineUtc;
        private static string LastConsumedPveReconnectMatchId = string.Empty;
        private static DateTimeOffset LastConsumedPveReconnectDeadlineUtc;

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
            ClearPveReconnectLaunch();
            ServerRunId = string.Empty;
            ServerDeckId = string.Empty;
            Wins = 0;
            Losses = 0;
            LastBattleOutcomeText = string.Empty;
            PendingAiChallengeResultAttemptId = string.Empty;
            ActiveBattleIsReconnect = false;
            ActiveBattleGameMode = "run";
            ActiveAiChallengeAttemptId = string.Empty;
            PendingAiChallengeAttemptId = string.Empty;
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
            ClearPveReconnectLaunch();
            HasPendingBattleStart = HasDraftedDeckReady;
            PendingBattleLaunchMode = HasPendingBattleStart ? launchMode : DraftBattleLaunchMode.Local;
            PendingBattleStartAllowsMissingDeck = false;
            PendingBattleStartIsPvpReconnect = false;
        }

        public static void QueuePvpReconnectBattleStart()
        {
            ClearPveReconnectLaunch();
            HasPendingBattleStart = true;
            PendingBattleLaunchMode = DraftBattleLaunchMode.OnlineMatchmaking;
            PendingBattleStartAllowsMissingDeck = true;
            PendingBattleStartIsPvpReconnect = true;
        }

        public static void QueuePveReconnectBattleStart(string matchId, int remainingSeconds)
        {
            if (string.IsNullOrWhiteSpace(matchId) || remainingSeconds <= 0)
            {
                throw new ArgumentException("A live server battle and remaining reconnect time are required.");
            }

            ClearPveReconnectLaunch();
            HasPendingBattleStart = true;
            PendingBattleLaunchMode = DraftBattleLaunchMode.OnlineServerAi;
            PendingBattleStartAllowsMissingDeck = true;
            PendingBattleStartIsPvpReconnect = false;
            PendingPveReconnectMatchId = matchId.Trim();
            PendingPveReconnectDeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(remainingSeconds);
            PendingExplicitReconnect = true;
        }

        public static void QueueBattleReconnect(PvpReconnectStatusResponse response)
        {
            if (response == null || !response.ResolvedHasReconnectableBattle)
                throw new ArgumentException("A live battle is required.");
            if (response.ResolvedIsServerAiBattle || response.ResolvedIsAiChallenge)
            {
                QueuePveReconnectBattleStart(response.ResolvedMatchId, response.ResolvedRemainingSeconds);
                PendingAiChallengeAttemptId = response.ResolvedAiChallengeAttemptId ?? string.Empty;
                if (response.ResolvedIsAiChallenge)
                    PendingBattleLaunchMode = DraftBattleLaunchMode.OnlineAiChallenge;
            }
            else QueuePvpReconnectBattleStart();
        }

        public static void QueueAiChallengeBattleStart(string matchId, string attemptId)
        {
            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(attemptId))
                throw new ArgumentException("A server challenge attempt and match are required.");
            QueuePveReconnectBattleStart(matchId, 60);
            PendingBattleLaunchMode = DraftBattleLaunchMode.OnlineAiChallenge;
            PendingAiChallengeAttemptId = attemptId;
            PendingExplicitReconnect = false;
        }

        public static void ApplyAuthoritativeBattleMode(string gameMode, string attemptId)
        {
            ActiveBattleGameMode = string.Equals(gameMode, "ai_challenge", StringComparison.OrdinalIgnoreCase)
                ? "ai_challenge" : "run";
            ActiveAiChallengeAttemptId = ActiveBattleGameMode == "ai_challenge" ? attemptId ?? string.Empty : string.Empty;
        }

        public static bool TryConsumeLastPveReconnect(out string matchId, out int remainingSeconds)
        {
            matchId = LastConsumedPveReconnectMatchId;
            remainingSeconds = string.IsNullOrWhiteSpace(matchId) ? 0 : Math.Max(0, (int)Math.Ceiling(
                (LastConsumedPveReconnectDeadlineUtc - DateTimeOffset.UtcNow).TotalSeconds));
            ClearPveReconnectLaunch();
            return !string.IsNullOrWhiteSpace(matchId);
        }

        private static void ClearPveReconnectLaunch()
        {
            PendingPveReconnectMatchId = string.Empty;
            PendingPveReconnectDeadlineUtc = default;
            LastConsumedPveReconnectMatchId = string.Empty;
            LastConsumedPveReconnectDeadlineUtc = default;
            PendingAiChallengeAttemptId = string.Empty;
            PendingExplicitReconnect = false;
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
                ClearPveReconnectLaunch();
                return false;
            }

            HasPendingBattleStart = false;
            launchMode = PendingBattleLaunchMode;
            ActiveBattleIsReconnect = PendingExplicitReconnect || PendingBattleStartIsPvpReconnect;
            PendingExplicitReconnect = false;
            ApplyAuthoritativeBattleMode(launchMode == DraftBattleLaunchMode.OnlineAiChallenge
                ? "ai_challenge" : "run", PendingAiChallengeAttemptId);
            PendingAiChallengeAttemptId = string.Empty;
            PendingBattleLaunchMode = DraftBattleLaunchMode.Local;
            var allowMissingDeck = PendingBattleStartAllowsMissingDeck;
            PendingBattleStartAllowsMissingDeck = false;
            LastConsumedBattleStartWasPvpReconnect = PendingBattleStartIsPvpReconnect;
            LastConsumedPveReconnectMatchId = PendingPveReconnectMatchId;
            LastConsumedPveReconnectDeadlineUtc = PendingPveReconnectDeadlineUtc;
            PendingPveReconnectMatchId = string.Empty;
            PendingPveReconnectDeadlineUtc = default;
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
            ClearPveReconnectLaunch();
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

        public static void PrepareForUnavailableBattleReturn()
        {
            HasPendingBattleStart = false;
            PendingBattleLaunchMode = DraftBattleLaunchMode.Local;
            PendingBattleStartAllowsMissingDeck = false;
            PendingBattleStartIsPvpReconnect = false;
            LastConsumedBattleStartWasPvpReconnect = false;
            ClearPveReconnectLaunch();
            RestoreCurrentDraftDeckFromCompletedDeckIfNeeded();
            LastBattleOutcomeText = string.Empty;
        }
        public static void QueueAiChallengeReceiptRefresh()
        {
            if (!string.IsNullOrWhiteSpace(ActiveAiChallengeAttemptId))
                PendingAiChallengeResultAttemptId = ActiveAiChallengeAttemptId;
        }

        public static void RecordAiChallengeOutcome(bool won, bool draw)
        {
            PendingAiChallengeResultAttemptId = ActiveAiChallengeAttemptId;
            LastBattleOutcomeText = draw ? "골드 던전 무승부" : won ? "골드 던전 승리" : "골드 던전 패배";
        }

        public static void ApplyAiChallengeReceipt(AiChallengeAttemptResponse receipt)
        {
            if (receipt == null || !receipt.IsFinished ||
                receipt.AttemptId != PendingAiChallengeResultAttemptId) return;
            LastBattleOutcomeText = receipt.Status == "completed"
                ? receipt.Won == true ? $"골드 던전 승리 · 서버 골드 +{Math.Max(0, receipt.RewardGold)}"
                    : "골드 던전 종료 · 보상 없음"
                : "골드 던전 취소";
            PendingAiChallengeResultAttemptId = string.Empty;
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
