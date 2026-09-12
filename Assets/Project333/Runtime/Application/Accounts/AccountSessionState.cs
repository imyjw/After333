using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project333.Runtime.Application.Accounts
{
    public static class AccountSessionState
    {
        private const string LegacyGuestTokenKey = "Project333.GuestToken";
        private const string SessionTokenKey = "Project333.SessionToken";
        private const string RefreshTokenKey = "Project333.RefreshToken";
        private const string AccountKindKey = "Project333.AccountKind";
        private const string AutomaticGoogleSignInSuppressedKey =
            "Project333.AutomaticGoogleSignInSuppressed";
        private static readonly Dictionary<string, int> OwnedCardUpgradeLevels =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> ActiveDraftPickCardIds = new List<string>();
        private static readonly List<string> ActiveDraftOfferCardIds = new List<string>();
        private static readonly List<string> LatestDraftPickCardIds = new List<string>();
        private static readonly List<string> LatestDraftOfferCardIds = new List<string>();

        public static string AccountId { get; private set; } = string.Empty;
        public static string DisplayName { get; private set; } = string.Empty;
        public static string AccountKind { get; private set; } = string.Empty;
        public static string SessionToken { get; private set; } = string.Empty;
        public static string RefreshToken { get; private set; } = string.Empty;
        public static string ActiveRunId { get; private set; } = string.Empty;
        public static string ActiveRunMode { get; private set; } = string.Empty;
        public static string ActiveRunStatus { get; private set; } = string.Empty;
        public static int ActiveRunWins { get; private set; }
        public static int ActiveRunLosses { get; private set; }
        public static string ActiveDeckId { get; private set; } = string.Empty;
        public static string ActiveDeckType { get; private set; } = string.Empty;
        public static int ActiveDeckCardCount { get; private set; }
        public static string LatestRunId { get; private set; } = string.Empty;
        public static string LatestRunMode { get; private set; } = string.Empty;
        public static string LatestRunStatus { get; private set; } = string.Empty;
        public static int LatestRunWins { get; private set; }
        public static int LatestRunLosses { get; private set; }
        public static string LatestRunRewardClaimedAt { get; private set; } = string.Empty;
        public static string LatestDeckId { get; private set; } = string.Empty;
        public static string LatestDeckType { get; private set; } = string.Empty;
        public static int LatestDeckCardCount { get; private set; }
        public static long ResourceGold { get; private set; }
        public static int Tickets { get; private set; }
        public static bool IsAuthenticated => !string.IsNullOrWhiteSpace(SessionToken);
        public static IReadOnlyList<string> ActiveDraftPicks => ActiveDraftPickCardIds;
        public static IReadOnlyList<string> ActiveDraftOffer => ActiveDraftOfferCardIds;
        public static IReadOnlyList<string> LatestDraftPicks => LatestDraftPickCardIds;
        public static IReadOnlyList<string> LatestDraftOffer => LatestDraftOfferCardIds;

        public static IEnumerable<string> OwnedCardIds => new List<string>(OwnedCardUpgradeLevels.Keys);

        public static int GetOwnedCardUpgradeLevel(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return 0;
            }

            return OwnedCardUpgradeLevels.TryGetValue(cardId, out var upgradeLevel)
                ? Math.Max(0, upgradeLevel)
                : 0;
        }

        public static bool LoadSavedLoginCredentials()
        {
            AccountCredentialStore.DeleteKey(ScopedKey(LegacyGuestTokenKey));
            SessionToken = AccountCredentialStore.GetString(ScopedKey(SessionTokenKey));
            RefreshToken = AccountCredentialStore.GetString(ScopedKey(RefreshTokenKey));
            AccountKind = PlayerPrefs.GetString(ScopedKey(AccountKindKey), string.Empty);

            if (string.Equals(AccountKind, "guest", StringComparison.OrdinalIgnoreCase))
            {
                ClearSavedSession();
                return false;
            }

            return HasSavedLoginCredential;
        }

        public static bool HasSavedRegisteredSession =>
            (!string.IsNullOrWhiteSpace(SessionToken) || !string.IsNullOrWhiteSpace(RefreshToken)) &&
            string.Equals(AccountKind, "registered", StringComparison.OrdinalIgnoreCase);

        public static bool HasSavedLoginCredential =>
            !string.IsNullOrWhiteSpace(SessionToken) ||
            !string.IsNullOrWhiteSpace(RefreshToken);

        public static bool IsAutomaticGoogleSignInSuppressed =>
            PlayerPrefs.GetInt(ScopedKey(AutomaticGoogleSignInSuppressedKey), 0) == 1;

        public static bool IsAutomaticGoogleSignInEnabled =>
            PlayerPrefs.GetInt(ScopedKey(AutomaticGoogleSignInSuppressedKey), 0) == 2;

        public static void SetAutomaticGoogleSignInSuppressed(bool suppressed)
        {
            var key = ScopedKey(AutomaticGoogleSignInSuppressedKey);
            PlayerPrefs.SetInt(key, suppressed ? 1 : 2);

            PlayerPrefs.Save();
        }

        public static void ApplyGameIdAuthResponse(GameIdAuthResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;

            SessionToken = response.ResolvedSessionToken ?? string.Empty;
            RefreshToken = response.ResolvedRefreshToken ?? string.Empty;
            AccountId = account?.ResolvedId ?? string.Empty;
            DisplayName = account?.ResolvedDisplayName ?? string.Empty;
            AccountKind = account?.ResolvedAccountKind ?? string.Empty;
            ResourceGold = wallet?.ResolvedResourceGold ?? 0;
            Tickets = wallet?.ResolvedTickets ?? 0;
            ClearActiveRunState();
            ClearLatestRunState();

            AccountCredentialStore.SetString(ScopedKey(SessionTokenKey), SessionToken);
            AccountCredentialStore.SetString(ScopedKey(RefreshTokenKey), RefreshToken);
            PlayerPrefs.SetString(ScopedKey(AccountKindKey), AccountKind);
            SetAutomaticGoogleSignInSuppressed(true);
            PlayerPrefs.Save();
        }

        public static void ApplyGoogleAuthResponse(GoogleAuthResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;

            SessionToken = response.ResolvedSessionToken ?? string.Empty;
            RefreshToken = response.ResolvedRefreshToken ?? string.Empty;
            AccountId = account?.ResolvedId ?? string.Empty;
            DisplayName = account?.ResolvedDisplayName ?? string.Empty;
            AccountKind = account?.ResolvedAccountKind ?? string.Empty;
            ResourceGold = wallet?.ResolvedResourceGold ?? 0;
            Tickets = wallet?.ResolvedTickets ?? 0;
            ClearActiveRunState();
            ClearLatestRunState();

            AccountCredentialStore.SetString(ScopedKey(SessionTokenKey), SessionToken);
            AccountCredentialStore.SetString(ScopedKey(RefreshTokenKey), RefreshToken);
            PlayerPrefs.SetString(ScopedKey(AccountKindKey), AccountKind);
            SetAutomaticGoogleSignInSuppressed(false);
            PlayerPrefs.Save();
        }

        public static void ApplyRefreshAuthResponse(RefreshAuthResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;

            SessionToken = response.ResolvedSessionToken ?? string.Empty;
            RefreshToken = response.ResolvedRefreshToken ?? string.Empty;
            AccountId = account?.ResolvedId ?? AccountId;
            DisplayName = account?.ResolvedDisplayName ?? DisplayName;
            AccountKind = account?.ResolvedAccountKind ?? AccountKind;
            ResourceGold = wallet?.ResolvedResourceGold ?? ResourceGold;
            Tickets = wallet?.ResolvedTickets ?? Tickets;

            AccountCredentialStore.SetString(ScopedKey(SessionTokenKey), SessionToken);
            AccountCredentialStore.SetString(ScopedKey(RefreshTokenKey), RefreshToken);
            PlayerPrefs.SetString(ScopedKey(AccountKindKey), AccountKind);
            PlayerPrefs.Save();
        }

        public static void ApplyStartRunResponse(StartRunResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;
            var activeRun = response.ResolvedActiveRun;

            if (account != null)
            {
                AccountId = account.ResolvedId ?? AccountId;
                DisplayName = account.ResolvedDisplayName ?? DisplayName;
                AccountKind = account.ResolvedAccountKind ?? AccountKind;
            }

            if (wallet != null)
            {
                ResourceGold = wallet.ResolvedResourceGold;
                Tickets = wallet.ResolvedTickets;
            }

            if (activeRun != null)
            {
                ApplyRunSummary(activeRun, clearWhenMissing: true);
                ApplyLatestRunSummary(activeRun);
                ActiveDeckId = string.Empty;
                ActiveDeckType = string.Empty;
                ActiveDeckCardCount = 0;
                LatestDeckId = string.Empty;
                LatestDeckType = string.Empty;
                LatestDeckCardCount = 0;
            }

            CopyCardIds(response.ResolvedDraftPickCardIds, ActiveDraftPickCardIds);
            CopyCardIds(response.ResolvedCurrentOfferCardIds, ActiveDraftOfferCardIds);
            CopyCardIds(response.ResolvedDraftPickCardIds, LatestDraftPickCardIds);
            CopyCardIds(response.ResolvedCurrentOfferCardIds, LatestDraftOfferCardIds);
        }

        public static void ApplyPurchaseTicketResponse(PurchaseTicketResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;

            if (account != null)
            {
                AccountId = account.ResolvedId ?? AccountId;
                DisplayName = account.ResolvedDisplayName ?? DisplayName;
            }

            if (wallet != null)
            {
                ResourceGold = wallet.ResolvedResourceGold;
                Tickets = wallet.ResolvedTickets;
            }
        }

        public static void ApplyRewardedAdWallet(WalletDto wallet)
        {
            if (wallet == null)
            {
                return;
            }

            ResourceGold = wallet.ResolvedResourceGold;
            Tickets = wallet.ResolvedTickets;
        }

        public static void ApplyUpgradeCardResponse(UpgradeCardResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;

            if (account != null)
            {
                AccountId = account.ResolvedId ?? AccountId;
                DisplayName = account.ResolvedDisplayName ?? DisplayName;
            }

            if (wallet != null)
            {
                ResourceGold = wallet.ResolvedResourceGold;
                Tickets = wallet.ResolvedTickets;
            }

            ApplyOwnedCardCollection(response.ResolvedCollectionSummary, replaceExisting: false);
            ApplyOwnedCard(response.ResolvedUpgradedCard);
        }

        public static void ApplyCompleteDraftResponse(CompleteDraftResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;
            var activeRun = response.ResolvedActiveRun;
            var deck = response.ResolvedDeck;

            if (account != null)
            {
                AccountId = account.ResolvedId ?? AccountId;
                DisplayName = account.ResolvedDisplayName ?? DisplayName;
            }

            if (wallet != null)
            {
                ResourceGold = wallet.ResolvedResourceGold;
                Tickets = wallet.ResolvedTickets;
            }

            if (activeRun != null)
            {
                ApplyRunSummary(activeRun, clearWhenMissing: false);
                ApplyLatestRunSummary(activeRun);
            }

            if (deck != null)
            {
                ActiveDeckId = deck.ResolvedId ?? string.Empty;
                ActiveDeckType = deck.ResolvedDeckType ?? string.Empty;
                ActiveDeckCardCount = deck.ResolvedCardCount;
                LatestDeckId = ActiveDeckId;
                LatestDeckType = ActiveDeckType;
                LatestDeckCardCount = ActiveDeckCardCount;
                ActiveDraftPickCardIds.Clear();
                ActiveDraftOfferCardIds.Clear();
                LatestDraftPickCardIds.Clear();
                LatestDraftOfferCardIds.Clear();
            }
        }

        public static void ApplySaveDraftPicksResponse(SaveDraftPicksResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;
            var activeRun = response.ResolvedActiveRun;

            if (account != null)
            {
                AccountId = account.ResolvedId ?? AccountId;
                DisplayName = account.ResolvedDisplayName ?? DisplayName;
            }

            if (wallet != null)
            {
                ResourceGold = wallet.ResolvedResourceGold;
                Tickets = wallet.ResolvedTickets;
            }

            if (activeRun != null)
            {
                ApplyRunSummary(activeRun, clearWhenMissing: false);
                ApplyLatestRunSummary(activeRun);
            }

            CopyCardIds(response.ResolvedDraftPickCardIds, ActiveDraftPickCardIds);
            CopyCardIds(response.ResolvedCurrentOfferCardIds, ActiveDraftOfferCardIds);
            CopyCardIds(response.ResolvedDraftPickCardIds, LatestDraftPickCardIds);
            CopyCardIds(response.ResolvedCurrentOfferCardIds, LatestDraftOfferCardIds);
        }

        public static void ApplyDraftStateResponse(DraftStateResponse response)
        {
            if (response == null)
            {
                return;
            }

            ApplyDraftProgress(
                response.ResolvedAccount,
                response.ResolvedWallet,
                response.ResolvedActiveRun,
                response.ResolvedDraftPickCardIds,
                response.ResolvedCurrentOfferCardIds);
        }

        public static void ApplySelectDraftCardResponse(SelectDraftCardResponse response)
        {
            if (response == null)
            {
                return;
            }

            ApplyDraftProgress(
                response.ResolvedAccount,
                response.ResolvedWallet,
                response.ResolvedActiveRun,
                response.ResolvedDraftPickCardIds,
                response.ResolvedCurrentOfferCardIds);

            var deck = response.ResolvedDeck;
            if (deck == null)
            {
                return;
            }

            ActiveDeckId = deck.ResolvedId ?? string.Empty;
            ActiveDeckType = deck.ResolvedDeckType ?? string.Empty;
            ActiveDeckCardCount = deck.ResolvedCardCount;
            LatestDeckId = ActiveDeckId;
            LatestDeckType = ActiveDeckType;
            LatestDeckCardCount = ActiveDeckCardCount;
            ActiveDraftPickCardIds.Clear();
            ActiveDraftOfferCardIds.Clear();
            LatestDraftPickCardIds.Clear();
            LatestDraftOfferCardIds.Clear();
        }

        public static void ApplyClaimRunRewardsResponse(ClaimRunRewardsResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;
            var run = response.ResolvedRun;

            if (account != null)
            {
                AccountId = account.ResolvedId ?? AccountId;
                DisplayName = account.ResolvedDisplayName ?? DisplayName;
            }

            if (wallet != null)
            {
                ResourceGold = wallet.ResolvedResourceGold;
                Tickets = wallet.ResolvedTickets;
            }

            if (run != null)
            {
                ApplyLatestRunSummary(run);
                if (string.Equals(ActiveRunId, run.ResolvedId, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(run.ResolvedStatus, "completed", System.StringComparison.OrdinalIgnoreCase))
                {
                    ClearActiveRunState();
                }
            }
        }

        public static void ApplyMeResponse(MeResponse response)
        {
            if (response == null)
            {
                return;
            }

            var account = response.ResolvedAccount;
            var wallet = response.ResolvedWallet;
            var activeRun = response.ResolvedActiveRun;
            var activeDeck = response.ResolvedActiveDeck;
            var latestRun = response.ResolvedLatestRun;
            var latestDeck = response.ResolvedLatestDeck;

            if (account != null)
            {
                AccountId = account.ResolvedId ?? AccountId;
                DisplayName = account.ResolvedDisplayName ?? DisplayName;
                AccountKind = account.ResolvedAccountKind ?? AccountKind;
            }

            if (wallet != null)
            {
                ResourceGold = wallet.ResolvedResourceGold;
                Tickets = wallet.ResolvedTickets;
            }

            if (activeRun != null)
            {
                ApplyRunSummary(activeRun, clearWhenMissing: false);
                CopyCardIds(response.ResolvedActiveDraftPickCardIds, ActiveDraftPickCardIds);
                CopyCardIds(response.ResolvedActiveDraftOfferCardIds, ActiveDraftOfferCardIds);
            }
            else
            {
                ApplyRunSummary(null, clearWhenMissing: true);
            }

            if (activeDeck != null)
            {
                ActiveDeckId = activeDeck.ResolvedId ?? ActiveDeckId;
                ActiveDeckType = activeDeck.ResolvedDeckType ?? ActiveDeckType;
                ActiveDeckCardCount = activeDeck.ResolvedCardCount;
            }
            else
            {
                ActiveDeckId = string.Empty;
                ActiveDeckType = string.Empty;
                ActiveDeckCardCount = 0;
            }

            if (latestRun != null)
            {
                ApplyLatestRunSummary(latestRun);
                CopyCardIds(response.ResolvedLatestDraftPickCardIds, LatestDraftPickCardIds);
                CopyCardIds(response.ResolvedLatestDraftOfferCardIds, LatestDraftOfferCardIds);
            }
            else
            {
                ClearLatestRunState();
            }

            if (latestDeck != null)
            {
                LatestDeckId = latestDeck.ResolvedId ?? LatestDeckId;
                LatestDeckType = latestDeck.ResolvedDeckType ?? LatestDeckType;
                LatestDeckCardCount = latestDeck.ResolvedCardCount;
            }
            else
            {
                LatestDeckId = string.Empty;
                LatestDeckType = string.Empty;
                LatestDeckCardCount = 0;
            }

            ApplyOwnedCardCollection(response.ResolvedCollectionSummary, replaceExisting: true);
            PlayerPrefs.SetString(ScopedKey(AccountKindKey), AccountKind);
            PlayerPrefs.Save();
        }

        private static void ApplyRunSummary(RunSummaryDto activeRun, bool clearWhenMissing)
        {
            if (activeRun == null)
            {
                if (clearWhenMissing)
                {
                    ClearActiveRunState();
                }

                return;
            }

            ActiveRunId = activeRun.ResolvedId ?? ActiveRunId;
            ActiveRunMode = activeRun.ResolvedMode ?? ActiveRunMode;
            ActiveRunStatus = activeRun.ResolvedStatus ?? ActiveRunStatus;
            ActiveRunWins = activeRun.ResolvedWins;
            ActiveRunLosses = activeRun.ResolvedLosses;
        }

        public static bool HasResumableRun
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ActiveRunId))
                {
                    return false;
                }

                return string.Equals(ActiveRunStatus, "drafting", System.StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(ActiveRunStatus, "ready", System.StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(ActiveRunStatus, "in_progress", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool HasSavedDraftDeck =>
            HasResumableRun &&
            !string.IsNullOrWhiteSpace(ActiveDeckId) &&
            (string.Equals(ActiveRunStatus, "ready", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ActiveRunStatus, "in_progress", System.StringComparison.OrdinalIgnoreCase));

        public static bool HasLatestRun =>
            !string.IsNullOrWhiteSpace(LatestRunId);

        public static bool IsLatestRunCompleted =>
            HasLatestRun &&
            string.Equals(LatestRunStatus, "completed", System.StringComparison.OrdinalIgnoreCase);

        public static bool LatestRunEndedByWins =>
            IsLatestRunCompleted && LatestRunWins >= 33;

        public static bool LatestRunEndedByLosses =>
            IsLatestRunCompleted && LatestRunLosses >= 3;

        public static bool IsLatestRunRewardClaimed =>
            IsLatestRunCompleted &&
            !string.IsNullOrWhiteSpace(LatestRunRewardClaimedAt);

        public static bool HasUnclaimedLatestRunRewards =>
            IsLatestRunCompleted &&
            !IsLatestRunRewardClaimed;

        public static bool HasLatestDraftDeck =>
            HasLatestRun &&
            !string.IsNullOrWhiteSpace(LatestDeckId);

        private static void ClearActiveRunState()
        {
            ActiveRunId = string.Empty;
            ActiveRunMode = string.Empty;
            ActiveRunStatus = string.Empty;
            ActiveRunWins = 0;
            ActiveRunLosses = 0;
            ActiveDeckId = string.Empty;
            ActiveDeckType = string.Empty;
            ActiveDeckCardCount = 0;
            ActiveDraftPickCardIds.Clear();
            ActiveDraftOfferCardIds.Clear();
        }

        private static void ApplyLatestRunSummary(RunSummaryDto latestRun)
        {
            if (latestRun == null)
            {
                ClearLatestRunState();
                return;
            }

            LatestRunId = latestRun.ResolvedId ?? LatestRunId;
            LatestRunMode = latestRun.ResolvedMode ?? LatestRunMode;
            LatestRunStatus = latestRun.ResolvedStatus ?? LatestRunStatus;
            LatestRunWins = latestRun.ResolvedWins;
            LatestRunLosses = latestRun.ResolvedLosses;
            LatestRunRewardClaimedAt = latestRun.ResolvedRewardClaimedAt ?? string.Empty;
        }

        private static void ClearLatestRunState()
        {
            LatestRunId = string.Empty;
            LatestRunMode = string.Empty;
            LatestRunStatus = string.Empty;
            LatestRunWins = 0;
            LatestRunLosses = 0;
            LatestRunRewardClaimedAt = string.Empty;
            LatestDeckId = string.Empty;
            LatestDeckType = string.Empty;
            LatestDeckCardCount = 0;
            LatestDraftPickCardIds.Clear();
            LatestDraftOfferCardIds.Clear();
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

        private static void ApplyDraftProgress(
            AuthAccountDto account,
            WalletDto wallet,
            RunSummaryDto activeRun,
            IReadOnlyList<string> draftPickCardIds,
            IReadOnlyList<string> currentOfferCardIds)
        {
            if (account != null)
            {
                AccountId = account.ResolvedId ?? AccountId;
                DisplayName = account.ResolvedDisplayName ?? DisplayName;
            }

            if (wallet != null)
            {
                ResourceGold = wallet.ResolvedResourceGold;
                Tickets = wallet.ResolvedTickets;
            }

            if (activeRun != null)
            {
                ApplyRunSummary(activeRun, clearWhenMissing: false);
                ApplyLatestRunSummary(activeRun);
            }

            CopyCardIds(draftPickCardIds, ActiveDraftPickCardIds);
            CopyCardIds(currentOfferCardIds, ActiveDraftOfferCardIds);
            CopyCardIds(draftPickCardIds, LatestDraftPickCardIds);
            CopyCardIds(currentOfferCardIds, LatestDraftOfferCardIds);
        }

        private static void ApplyOwnedCardCollection(CollectionSummaryDto collection, bool replaceExisting)
        {
            if (replaceExisting)
            {
                OwnedCardUpgradeLevels.Clear();
            }

            if (collection == null)
            {
                return;
            }

            foreach (var ownedCard in collection.ResolvedOwnedCards)
            {
                ApplyOwnedCard(ownedCard);
            }
        }

        private static void ApplyOwnedCard(OwnedCardDto ownedCard)
        {
            if (ownedCard == null)
            {
                return;
            }

            var cardId = ownedCard.ResolvedCardId;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return;
            }

            OwnedCardUpgradeLevels[cardId] = Math.Max(0, ownedCard.ResolvedUpgradeLevel);
        }

        public static void ClearSavedSession()
        {
            AccountId = string.Empty;
            DisplayName = string.Empty;
            AccountKind = string.Empty;
            SessionToken = string.Empty;
            RefreshToken = string.Empty;
            ClearActiveRunState();
            ClearLatestRunState();
            OwnedCardUpgradeLevels.Clear();
            ResourceGold = 0;
            Tickets = 0;
            AccountCredentialStore.DeleteKey(ScopedKey(LegacyGuestTokenKey));
            AccountCredentialStore.DeleteKey(ScopedKey(SessionTokenKey));
            AccountCredentialStore.DeleteKey(ScopedKey(RefreshTokenKey));
            PlayerPrefs.DeleteKey(ScopedKey(AccountKindKey));
            PlayerPrefs.Save();
        }

        public static void ClearAccessToken()
        {
            SessionToken = string.Empty;
            AccountCredentialStore.DeleteKey(ScopedKey(SessionTokenKey));
            PlayerPrefs.Save();
        }

        private static string ScopedKey(string baseKey)
        {
            return Project333ClientProfile.ToScopedPlayerPrefsKey(baseKey);
        }
    }
}
