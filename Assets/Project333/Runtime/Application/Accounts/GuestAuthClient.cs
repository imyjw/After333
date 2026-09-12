using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project333.Runtime.Application.Accounts
{
    public sealed class GuestAuthClient
    {
        private readonly string _baseUrl;

        public GuestAuthClient(string baseUrl)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? "http://127.0.0.1:7333"
                : baseUrl.TrimEnd('/');
        }

        public async Task<GameIdAuthResponse> RegisterGameIdAsync(
            string gameId,
            string password,
            string displayName,
            string clientVersion,
            CancellationToken cancellationToken)
        {
            var request = new GameIdAuthRequest
            {
                GameId = gameId,
                Password = password,
                DisplayName = displayName,
                ClientVersion = clientVersion
            };
            var response = await PostJsonAsync<GameIdAuthRequest, GameIdAuthResponse>(
                "/auth/register",
                request,
                cancellationToken);
            if (response == null || string.IsNullOrWhiteSpace(response.ResolvedSessionToken))
            {
                throw new InvalidOperationException("Register response did not include a session token.");
            }

            return response;
        }

        public async Task<GameIdAuthResponse> LoginGameIdAsync(
            string gameId,
            string password,
            string clientVersion,
            CancellationToken cancellationToken)
        {
            var request = new GameIdAuthRequest
            {
                GameId = gameId,
                Password = password,
                ClientVersion = clientVersion
            };
            var response = await PostJsonAsync<GameIdAuthRequest, GameIdAuthResponse>(
                "/auth/login",
                request,
                cancellationToken);
            if (response == null || string.IsNullOrWhiteSpace(response.ResolvedSessionToken))
            {
                throw new InvalidOperationException("Login response did not include a session token.");
            }

            return response;
        }

        public async Task<GoogleAuthResponse> AuthenticateGoogleAsync(
            string idToken,
            string clientVersion,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new InvalidOperationException("Google ID token is missing.");
            }

            var request = new GoogleAuthRequest
            {
                IdToken = idToken,
                ClientVersion = clientVersion
            };
            var response = await PostJsonAsync<GoogleAuthRequest, GoogleAuthResponse>(
                "/auth/google",
                request,
                cancellationToken);
            if (response == null || string.IsNullOrWhiteSpace(response.ResolvedSessionToken))
            {
                throw new InvalidOperationException("Google auth response did not include a session token.");
            }

            return response;
        }

        public async Task<string> ExchangeGoogleDesktopCodeAsync(
            string clientId,
            string authorizationCode,
            string redirectUri,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(authorizationCode) ||
                string.IsNullOrWhiteSpace(redirectUri) ||
                string.IsNullOrWhiteSpace(codeVerifier))
            {
                throw new InvalidOperationException("Google desktop authorization data is incomplete.");
            }

            var request = new GoogleDesktopCodeExchangeRequest
            {
                ClientId = clientId,
                AuthorizationCode = authorizationCode,
                RedirectUri = redirectUri,
                CodeVerifier = codeVerifier
            };
            var response = await PostJsonAsync<GoogleDesktopCodeExchangeRequest, GoogleDesktopCodeExchangeResponse>(
                "/auth/google/desktop-token",
                request,
                cancellationToken);
            if (response == null || string.IsNullOrWhiteSpace(response.ResolvedIdToken))
            {
                throw new InvalidOperationException("After333 server did not return a Google ID token.");
            }

            return response.ResolvedIdToken;
        }

        public async Task<RefreshAuthResponse> RefreshSessionAsync(
            string refreshToken,
            string clientVersion,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new InvalidOperationException("Server refresh token is missing.");
            }

            var request = new RefreshAuthRequest
            {
                RefreshToken = refreshToken,
                ClientVersion = clientVersion
            };
            var response = await PostJsonAsync<RefreshAuthRequest, RefreshAuthResponse>(
                "/auth/refresh",
                request,
                cancellationToken);
            if (response == null ||
                string.IsNullOrWhiteSpace(response.ResolvedSessionToken) ||
                string.IsNullOrWhiteSpace(response.ResolvedRefreshToken))
            {
                throw new InvalidOperationException("Refresh response did not include a complete token pair.");
            }

            return response;
        }

        public async Task<LogoutAuthResponse> LogoutAsync(
            string sessionToken,
            string refreshToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(refreshToken))
            {
                return new LogoutAuthResponse { LoggedOut = true };
            }

            var request = new LogoutAuthRequest
            {
                RefreshToken = refreshToken
            };
            return await PostJsonAsync<LogoutAuthRequest, LogoutAuthResponse>(
                "/auth/logout",
                request,
                sessionToken,
                cancellationToken);
        }

        public async Task<MeResponse> GetMeAsync(
            string sessionToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            return await GetJsonAsync<MeResponse>(
                "/me",
                sessionToken,
                cancellationToken);
        }

        public async Task<ServerStatusResponse> GetServerStatusAsync(CancellationToken cancellationToken)
        {
            return await GetPublicJsonAsync<ServerStatusResponse>(
                "/server/status",
                cancellationToken);
        }

        public async Task<PvpReconnectStatusResponse> GetPvpReconnectStatusAsync(
            string sessionToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new InvalidOperationException("Server session token is missing.");
            }

            return await GetJsonAsync<PvpReconnectStatusResponse>(
                "/pvp/reconnect-status",
                sessionToken,
                cancellationToken);
        }

        private async Task<TResponse> GetPublicJsonAsync<TResponse>(
            string path,
            CancellationToken cancellationToken)
        {
            using var webRequest = new UnityWebRequest($"{_baseUrl}{path}", UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            webRequest.SetRequestHeader("Accept", "application/json");

            await SendAsync(webRequest, cancellationToken);
            return DeserializeResponse<TResponse>(webRequest);
        }

        private async Task<TResponse> GetJsonAsync<TResponse>(
            string path,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            using var webRequest = new UnityWebRequest($"{_baseUrl}{path}", UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            webRequest.SetRequestHeader("Accept", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {sessionToken}");

            await SendAsync(webRequest, cancellationToken);
            return DeserializeResponse<TResponse>(webRequest);
        }

        private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
            string path,
            TRequest request,
            CancellationToken cancellationToken)
        {
            return await PostJsonAsync<TRequest, TResponse>(
                path,
                request,
                sessionToken: null,
                cancellationToken);
        }

        private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
            string path,
            TRequest request,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var json = JsonUtility.ToJson(request);
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            using var webRequest = new UnityWebRequest($"{_baseUrl}{path}", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bodyRaw),
                downloadHandler = new DownloadHandlerBuffer()
            };
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {sessionToken}");
            }

            await SendAsync(webRequest, cancellationToken);
            return DeserializeResponse<TResponse>(webRequest);
        }

        private static async Task SendAsync(UnityWebRequest webRequest, CancellationToken cancellationToken)
        {
            await AccountHttpTransport.SendAsync(webRequest, cancellationToken);

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                var body = webRequest.downloadHandler?.text;
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(body)
                        ? $"HTTP request failed: {webRequest.responseCode} {webRequest.error}"
                        : $"HTTP request failed: {webRequest.responseCode} {webRequest.error} {body}");
            }
        }

        private static TResponse DeserializeResponse<TResponse>(UnityWebRequest webRequest)
        {
            var json = webRequest.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Server returned an empty response.");
            }

            var response = JsonUtility.FromJson<TResponse>(json);
            if (response == null)
            {
                throw new InvalidOperationException("Failed to parse server response.");
            }

            return response;
        }
    }

    [Serializable]
    public sealed class GameIdAuthRequest
    {
        public string gameId;
        public string password;
        public string displayName;
        public string clientVersion;

        public string GameId
        {
            get => gameId;
            set => gameId = value;
        }

        public string Password
        {
            get => password;
            set => password = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public string ClientVersion
        {
            get => clientVersion;
            set => clientVersion = value;
        }
    }

    [Serializable]
    public sealed class GoogleAuthRequest
    {
        public string idToken;
        public string clientVersion;

        public string IdToken
        {
            get => idToken;
            set => idToken = value;
        }

        public string ClientVersion
        {
            get => clientVersion;
            set => clientVersion = value;
        }
    }

    [Serializable]
    public sealed class GoogleDesktopCodeExchangeRequest
    {
        public string clientId;
        public string authorizationCode;
        public string redirectUri;
        public string codeVerifier;

        public string ClientId
        {
            get => clientId;
            set => clientId = value;
        }

        public string AuthorizationCode
        {
            get => authorizationCode;
            set => authorizationCode = value;
        }

        public string RedirectUri
        {
            get => redirectUri;
            set => redirectUri = value;
        }

        public string CodeVerifier
        {
            get => codeVerifier;
            set => codeVerifier = value;
        }
    }

    [Serializable]
    public sealed class GoogleDesktopCodeExchangeResponse
    {
        public string IdToken;
        public string idToken;

        public string ResolvedIdToken => string.IsNullOrWhiteSpace(IdToken) ? idToken : IdToken;
    }

    [Serializable]
    public sealed class RefreshAuthRequest
    {
        public string refreshToken;
        public string clientVersion;

        public string RefreshToken
        {
            get => refreshToken;
            set => refreshToken = value;
        }

        public string ClientVersion
        {
            get => clientVersion;
            set => clientVersion = value;
        }
    }

    [Serializable]
    public sealed class LogoutAuthRequest
    {
        public string refreshToken;

        public string RefreshToken
        {
            get => refreshToken;
            set => refreshToken = value;
        }
    }

    [Serializable]
    public sealed class GameIdAuthResponse
    {
        public string SessionToken;
        public string RefreshToken;
        public AuthAccountDto Account;
        public WalletDto Wallet;

        public string sessionToken;
        public string refreshToken;
        public AuthAccountDto account;
        public WalletDto wallet;

        public string ResolvedSessionToken => string.IsNullOrWhiteSpace(SessionToken) ? sessionToken : SessionToken;
        public string ResolvedRefreshToken => string.IsNullOrWhiteSpace(RefreshToken) ? refreshToken : RefreshToken;
        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
    }

    [Serializable]
    public sealed class GoogleAuthResponse
    {
        public string SessionToken;
        public string RefreshToken;
        public AuthAccountDto Account;
        public WalletDto Wallet;

        public string sessionToken;
        public string refreshToken;
        public AuthAccountDto account;
        public WalletDto wallet;

        public string ResolvedSessionToken => string.IsNullOrWhiteSpace(SessionToken) ? sessionToken : SessionToken;
        public string ResolvedRefreshToken => string.IsNullOrWhiteSpace(RefreshToken) ? refreshToken : RefreshToken;
        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
    }

    [Serializable]
    public sealed class RefreshAuthResponse
    {
        public string SessionToken;
        public string RefreshToken;
        public AuthAccountDto Account;
        public WalletDto Wallet;

        public string sessionToken;
        public string refreshToken;
        public AuthAccountDto account;
        public WalletDto wallet;

        public string ResolvedSessionToken => string.IsNullOrWhiteSpace(SessionToken) ? sessionToken : SessionToken;
        public string ResolvedRefreshToken => string.IsNullOrWhiteSpace(RefreshToken) ? refreshToken : RefreshToken;
        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
    }

    [Serializable]
    public sealed class LogoutAuthResponse
    {
        public bool LoggedOut;
        public bool loggedOut;

        public bool ResolvedLoggedOut => LoggedOut || loggedOut;
    }

    [Serializable]
    public sealed class PvpReconnectStatusResponse
    {
        public bool HasReconnectableBattle;
        public string MatchId;
        public string SeatId;
        public string OnlineSeatId;
        public int RemainingSeconds;
        public string ReconnectDeadlineUtc;

        public bool hasReconnectableBattle;
        public string matchId;
        public string seatId;
        public string onlineSeatId;
        public int remainingSeconds;
        public string reconnectDeadlineUtc;

        public bool ResolvedHasReconnectableBattle => HasReconnectableBattle || hasReconnectableBattle;
        public string ResolvedMatchId => string.IsNullOrWhiteSpace(MatchId) ? matchId : MatchId;
        public string ResolvedSeatId => string.IsNullOrWhiteSpace(SeatId) ? seatId : SeatId;
        public string ResolvedOnlineSeatId => string.IsNullOrWhiteSpace(OnlineSeatId) ? onlineSeatId : OnlineSeatId;
        public int ResolvedRemainingSeconds => RemainingSeconds != 0 ? RemainingSeconds : remainingSeconds;
        public string ResolvedReconnectDeadlineUtc => string.IsNullOrWhiteSpace(ReconnectDeadlineUtc)
            ? reconnectDeadlineUtc
            : ReconnectDeadlineUtc;
    }

    [Serializable]
    public sealed class ServerStatusResponse
    {
        public string Name;
        public string Status;
        public string ServerTimeUtc;
        public string Version;
        public string Environment;
        public string ListenUrl;
        public string PublicUrl;
        public ServerDatabaseStatusDto Database;
        public ServerCardDatabaseStatusDto Cards;
        public ServerDeploymentStatusDto Deployment;
        public ServerPvpStatusDto PvP;
        public ServerClientCompatibilityDto ClientCompatibility;
        public ServerAuthenticationStatusDto Authentication;

        public string name;
        public string status;
        public string serverTimeUtc;
        public string version;
        public string environment;
        public string listenUrl;
        public string publicUrl;
        public ServerDatabaseStatusDto database;
        public ServerCardDatabaseStatusDto cards;
        public ServerDeploymentStatusDto deployment;
        public ServerPvpStatusDto pvp;
        public ServerClientCompatibilityDto clientCompatibility;
        public ServerAuthenticationStatusDto authentication;

        public string ResolvedStatus => string.IsNullOrWhiteSpace(Status) ? status : Status;
        public string ResolvedPublicUrl => string.IsNullOrWhiteSpace(PublicUrl) ? publicUrl : PublicUrl;
        public ServerDatabaseStatusDto ResolvedDatabase => Database ?? database;
        public ServerCardDatabaseStatusDto ResolvedCards => Cards ?? cards;
        public ServerDeploymentStatusDto ResolvedDeployment => Deployment ?? deployment;
        public ServerPvpStatusDto ResolvedPvP => PvP ?? pvp;
        public ServerClientCompatibilityDto ResolvedClientCompatibility => ClientCompatibility ?? clientCompatibility;
        public ServerAuthenticationStatusDto ResolvedAuthentication => Authentication ?? authentication;
    }

    [Serializable]
    public sealed class ServerAuthenticationStatusDto
    {
        public ServerGoogleAuthStatusDto Google;
        public ServerGoogleAuthStatusDto google;

        public ServerGoogleAuthStatusDto ResolvedGoogle => Google ?? google;
    }

    [Serializable]
    public sealed class ServerGoogleAuthStatusDto
    {
        public bool Configured;
        public int AcceptedClientIdCount;
        public bool DesktopCodeExchangeConfigured;

        public bool configured;
        public int acceptedClientIdCount;
        public bool desktopCodeExchangeConfigured;

        public bool ResolvedConfigured => Configured || configured;
        public int ResolvedAcceptedClientIdCount => AcceptedClientIdCount != 0
            ? AcceptedClientIdCount
            : acceptedClientIdCount;
        public bool ResolvedDesktopCodeExchangeConfigured =>
            DesktopCodeExchangeConfigured || desktopCodeExchangeConfigured;
    }

    [Serializable]
    public sealed class ServerDatabaseStatusDto
    {
        public string Status;
        public bool Configured;
        public bool Reachable;
        public int AppliedMigrationCount;
        public string LatestMigration;
        public string Message;

        public string status;
        public bool configured;
        public bool reachable;
        public int appliedMigrationCount;
        public string latestMigration;
        public string message;

        public string ResolvedStatus => string.IsNullOrWhiteSpace(Status) ? status : Status;
        public bool ResolvedConfigured => Configured || configured;
        public bool ResolvedReachable => Reachable || reachable;
        public int ResolvedAppliedMigrationCount => AppliedMigrationCount != 0 ? AppliedMigrationCount : appliedMigrationCount;
    }

    [Serializable]
    public sealed class ServerCardDatabaseStatusDto
    {
        public string Status;
        public int SchemaVersion;
        public int CardCount;
        public string Message;

        public string status;
        public int schemaVersion;
        public int cardCount;
        public string message;

        public string ResolvedStatus => string.IsNullOrWhiteSpace(Status) ? status : Status;
        public int ResolvedSchemaVersion => SchemaVersion != 0 ? SchemaVersion : schemaVersion;
        public int ResolvedCardCount => CardCount != 0 ? CardCount : cardCount;
    }

    [Serializable]
    public sealed class ServerDeploymentStatusDto
    {
        public string Status;
        public string Mode;
        public bool Ready;
        public string[] Warnings;
        public string Message;

        public string status;
        public string mode;
        public bool ready;
        public string[] warnings;
        public string message;

        public string ResolvedStatus => string.IsNullOrWhiteSpace(Status) ? status : Status;
        public string ResolvedMode => string.IsNullOrWhiteSpace(Mode) ? mode : Mode;
        public bool ResolvedReady => Ready || ready;
        public IReadOnlyList<string> ResolvedWarnings => Warnings ?? warnings ?? Array.Empty<string>();
        public string ResolvedMessage => string.IsNullOrWhiteSpace(Message) ? message : Message;
    }

    [Serializable]
    public sealed class ServerPvpStatusDto
    {
        public int ReconnectGraceSeconds;
        public bool StartupRecoveryEnabled;
        public bool VerboseTransportLogsEnabled;

        public int reconnectGraceSeconds;
        public bool startupRecoveryEnabled;
        public bool verboseTransportLogsEnabled;

        public int ResolvedReconnectGraceSeconds => ReconnectGraceSeconds != 0 ? ReconnectGraceSeconds : reconnectGraceSeconds;
        public bool ResolvedStartupRecoveryEnabled => StartupRecoveryEnabled || startupRecoveryEnabled;
        public bool ResolvedVerboseTransportLogsEnabled => VerboseTransportLogsEnabled || verboseTransportLogsEnabled;
    }

    [Serializable]
    public sealed class ServerClientCompatibilityDto
    {
        public string RequiredClientVersion;
        public string RecommendedClientVersion;
        public string CardDefinitionVersion;

        public string requiredClientVersion;
        public string recommendedClientVersion;
        public string cardDefinitionVersion;

        public string ResolvedRequiredClientVersion => string.IsNullOrWhiteSpace(RequiredClientVersion)
            ? requiredClientVersion
            : RequiredClientVersion;
        public string ResolvedRecommendedClientVersion => string.IsNullOrWhiteSpace(RecommendedClientVersion)
            ? recommendedClientVersion
            : RecommendedClientVersion;
        public string ResolvedCardDefinitionVersion => string.IsNullOrWhiteSpace(CardDefinitionVersion)
            ? cardDefinitionVersion
            : CardDefinitionVersion;
    }

    [Serializable]
    public sealed class MeResponse
    {
        public AuthAccountDto Account;
        public WalletDto Wallet;
        public CollectionSummaryDto CollectionSummary;
        public RunSummaryDto ActiveRun;
        public DeckSummaryDto ActiveDeck;
        public string[] ActiveDeckCardIds;
        public string[] ActiveDraftPickCardIds;
        public string[] ActiveDraftOfferCardIds;
        public RunSummaryDto LatestRun;
        public DeckSummaryDto LatestDeck;
        public string[] LatestDeckCardIds;
        public string[] LatestDraftPickCardIds;
        public string[] LatestDraftOfferCardIds;

        public AuthAccountDto account;
        public WalletDto wallet;
        public CollectionSummaryDto collectionSummary;
        public RunSummaryDto activeRun;
        public DeckSummaryDto activeDeck;
        public string[] activeDeckCardIds;
        public string[] activeDraftPickCardIds;
        public string[] activeDraftOfferCardIds;
        public RunSummaryDto latestRun;
        public DeckSummaryDto latestDeck;
        public string[] latestDeckCardIds;
        public string[] latestDraftPickCardIds;
        public string[] latestDraftOfferCardIds;

        public AuthAccountDto ResolvedAccount => Account ?? account;
        public WalletDto ResolvedWallet => Wallet ?? wallet;
        public CollectionSummaryDto ResolvedCollectionSummary => CollectionSummary ?? collectionSummary;
        public RunSummaryDto ResolvedActiveRun => ActiveRun ?? activeRun;
        public DeckSummaryDto ResolvedActiveDeck => ActiveDeck ?? activeDeck;
        public IReadOnlyList<string> ResolvedActiveDeckCardIds => ActiveDeckCardIds ?? activeDeckCardIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResolvedActiveDraftPickCardIds => ActiveDraftPickCardIds ?? activeDraftPickCardIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResolvedActiveDraftOfferCardIds => ActiveDraftOfferCardIds ?? activeDraftOfferCardIds ?? Array.Empty<string>();
        public RunSummaryDto ResolvedLatestRun => LatestRun ?? latestRun;
        public DeckSummaryDto ResolvedLatestDeck => LatestDeck ?? latestDeck;
        public IReadOnlyList<string> ResolvedLatestDeckCardIds => LatestDeckCardIds ?? latestDeckCardIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResolvedLatestDraftPickCardIds => LatestDraftPickCardIds ?? latestDraftPickCardIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ResolvedLatestDraftOfferCardIds => LatestDraftOfferCardIds ?? latestDraftOfferCardIds ?? Array.Empty<string>();
    }

    [Serializable]
    public sealed class AuthAccountDto
    {
        public string Id;
        public string DisplayName;
        public string AccountKind;

        public string id;
        public string displayName;
        public string accountKind;

        public string ResolvedId => string.IsNullOrWhiteSpace(Id) ? id : Id;
        public string ResolvedDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? displayName : DisplayName;
        public string ResolvedAccountKind => string.IsNullOrWhiteSpace(AccountKind) ? accountKind : AccountKind;
    }

    [Serializable]
    public sealed class WalletDto
    {
        public long ResourceGold;
        public int Tickets;

        public long resourceGold;
        public int tickets;

        public long ResolvedResourceGold => ResourceGold != 0 ? ResourceGold : resourceGold;
        public int ResolvedTickets => Tickets != 0 ? Tickets : tickets;
    }

    [Serializable]
    public sealed class CollectionSummaryDto
    {
        public int OwnedCardKinds;
        public OwnedCardDto[] OwnedCards;

        public int ownedCardKinds;
        public OwnedCardDto[] ownedCards;

        public int ResolvedOwnedCardKinds => OwnedCardKinds != 0 ? OwnedCardKinds : ownedCardKinds;
        public IReadOnlyList<OwnedCardDto> ResolvedOwnedCards => OwnedCards ?? ownedCards ?? Array.Empty<OwnedCardDto>();
    }

    [Serializable]
    public sealed class OwnedCardDto
    {
        public string CardId;
        public int CopyCount;
        public int UpgradeLevel;

        public string cardId;
        public int copyCount;
        public int upgradeLevel;

        public string ResolvedCardId => string.IsNullOrWhiteSpace(CardId) ? cardId : CardId;
        public int ResolvedCopyCount => CopyCount != 0 ? CopyCount : copyCount;
        public int ResolvedUpgradeLevel => UpgradeLevel != 0 ? UpgradeLevel : upgradeLevel;
    }
}
