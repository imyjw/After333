using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Project333.Runtime.Application.Accounts;
using Project333.PvpServer.Persistence.Db;

namespace Project333.PvpServer.Auth;

public sealed class GuestAuthService
{
    private const string GuestProvider = "guest";
    private const string GameIdProvider = "game_id";
    private const string GoogleProvider = "google";
    private const string GuestAccountKind = "guest";
    private const string RegisteredAccountKind = "registered";
    private const string PasswordAlgorithm = "pbkdf2-sha256";
    private const int PasswordHashIterations = 100_000;
    private const int PasswordSaltByteLength = 16;
    private const int PasswordHashByteLength = 32;
    private const int LegacyGameIdMinLength = 3;
    private const int LegacyGameIdMaxLength = 32;
    private const int PasswordMaxLength = 128;
    private const int DisplayNameMaxLength = 32;

    private readonly DbConnectionFactory _connectionFactory;
    private readonly AuthTokenService _tokenService;
    private readonly GoogleIdentityTokenValidator _googleTokenValidator;
    private readonly IConfiguration _configuration;

    public GuestAuthService(
        DbConnectionFactory connectionFactory,
        AuthTokenService tokenService,
        GoogleIdentityTokenValidator googleTokenValidator,
        IConfiguration configuration)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _googleTokenValidator = googleTokenValidator ?? throw new ArgumentNullException(nameof(googleTokenValidator));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public bool IsDatabaseConfigured => _connectionFactory.IsConfigured;
    public bool IsGoogleAuthConfigured => _googleTokenValidator.IsConfigured;
    public int GoogleAcceptedClientIdCount => _googleTokenValidator.AcceptedClientIdCount;

    public Task<GuestAuthResponse> AuthenticateGuestAsync(
        GuestAuthRequest? request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        throw new AuthServiceException(
            "guest_accounts_disabled",
            "Guest accounts are no longer supported. Please sign in with Game ID or Google.");
    }

    public async Task<GameIdAuthResponse> RegisterGameIdAsync(
        GameIdAuthRequest? request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var normalizedGameId = NormalizeRegistrationGameId(request?.GameId);
        var password = ValidateRegistrationPassword(request?.Password);
        var displayName = NormalizeDisplayName(request?.DisplayName, normalizedGameId);
        var passwordHash = HashPassword(password);
        var clientContext = BuildClientContext(request?.ClientVersion, httpContext);
        var tokenPair = CreateSessionTokenPair();
        var newAccountTickets = ResolveInt("PROJECT333_NEW_ACCOUNT_TICKETS", 0);
        var newAccountResourceGold = ResolveLong("PROJECT333_NEW_ACCOUNT_RESOURCE_GOLD", 0);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GameIdIdentityExistsAsync(connection, transaction, normalizedGameId, cancellationToken))
        {
            throw new AuthServiceException("game_id_taken", "This Game ID is already registered.");
        }

        var account = await CreateGameIdAccountAsync(
            connection,
            transaction,
            normalizedGameId,
            displayName,
            passwordHash,
            newAccountResourceGold,
            newAccountTickets,
            cancellationToken);
        var wallet = await LoadWalletAsync(connection, transaction, account.Id, cancellationToken);
        wallet = await EnsureDevMinimumTicketsAsync(
            connection,
            transaction,
            account.Id,
            wallet,
            cancellationToken);
        await InsertSessionPairAsync(
            connection,
            transaction,
            account.Id,
            tokenPair,
            clientContext,
            Guid.NewGuid(),
            cancellationToken);
        await TouchIdentityLoginAsync(
            connection,
            transaction,
            account.Id,
            GameIdProvider,
            normalizedGameId,
            cancellationToken);
        await InsertIdentityEventAsync(
            connection,
            transaction,
            account.Id,
            authIdentityId: null,
            GameIdProvider,
            "login",
            previousAccountId: null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GameIdAuthResponse(
            tokenPair.SessionToken,
            tokenPair.RefreshToken,
            ToAccountDto(account),
            wallet);
    }

    public async Task<GameIdAuthResponse> LoginGameIdAsync(
        GameIdAuthRequest? request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var normalizedGameId = NormalizeLoginGameId(request?.GameId);
        var password = ValidateLoginPassword(request?.Password);
        var clientContext = BuildClientContext(request?.ClientVersion, httpContext);
        var tokenPair = CreateSessionTokenPair();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var identity = await FindGameIdIdentityAsync(connection, transaction, normalizedGameId, cancellationToken);
        if (identity == null || !VerifyPassword(password, identity.PasswordHash))
        {
            throw new AuthServiceException("invalid_credentials", "Game ID or password is incorrect.");
        }

        var account = identity.Account;
        if (account.IsBanned ||
            !string.Equals(account.AccountStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthServiceException("account_inactive", "This account is not active.");
        }

        var wallet = await LoadWalletAsync(connection, transaction, account.Id, cancellationToken);
        await InsertSessionPairAsync(
            connection,
            transaction,
            account.Id,
            tokenPair,
            clientContext,
            Guid.NewGuid(),
            cancellationToken);
        await TouchIdentityLoginAsync(
            connection,
            transaction,
            account.Id,
            GameIdProvider,
            normalizedGameId,
            cancellationToken);
        await InsertIdentityEventAsync(
            connection,
            transaction,
            account.Id,
            identity.IdentityId,
            GameIdProvider,
            "login",
            previousAccountId: null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GameIdAuthResponse(
            tokenPair.SessionToken,
            tokenPair.RefreshToken,
            ToAccountDto(account),
            wallet);
    }

    public async Task<LinkGameIdResponse> LinkGameIdAsync(
        AuthenticatedAccount authenticatedAccount,
        LinkGameIdRequest? request,
        CancellationToken cancellationToken)
    {
        if (authenticatedAccount == null)
        {
            throw new ArgumentNullException(nameof(authenticatedAccount));
        }

        var normalizedGameId = NormalizeRegistrationGameId(request?.GameId);
        var password = ValidateRegistrationPassword(request?.Password);
        var displayName = NormalizeDisplayName(request?.DisplayName, authenticatedAccount.Account.DisplayName);
        var passwordHash = HashPassword(password);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await GameIdIdentityExistsAsync(connection, transaction, normalizedGameId, cancellationToken))
        {
            throw new AuthServiceException("game_id_taken", "This Game ID is already registered.");
        }

        var identityId = await InsertGameIdIdentityAsync(
            connection,
            transaction,
            authenticatedAccount.Account.Id,
            normalizedGameId,
            passwordHash,
            cancellationToken);
        var account = await PromoteAccountToRegisteredAsync(
            connection,
            transaction,
            authenticatedAccount.Account.Id,
            displayName,
            cancellationToken);
        var wallet = await LoadWalletAsync(connection, transaction, account.Id, cancellationToken);
        await InsertIdentityEventAsync(
            connection,
            transaction,
            account.Id,
            identityId,
            GameIdProvider,
            "linked",
            previousAccountId: null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new LinkGameIdResponse(
            ToAccountDto(account),
            wallet);
    }

    public async Task<GoogleAuthResponse> AuthenticateGoogleAsync(
        GoogleAuthRequest? request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var googleIdentity = await _googleTokenValidator.ValidateAsync(
            request?.IdToken,
            cancellationToken);
        var clientContext = BuildClientContext(request?.ClientVersion, httpContext);
        var tokenPair = CreateSessionTokenPair();
        var newAccountTickets = ResolveInt("PROJECT333_NEW_ACCOUNT_TICKETS", 0);
        var newAccountResourceGold = ResolveLong("PROJECT333_NEW_ACCOUNT_RESOURCE_GOLD", 0);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existingIdentity = await FindExternalIdentityAsync(
            connection,
            transaction,
            GoogleProvider,
            googleIdentity.Subject,
            cancellationToken);
        var isNewAccount = existingIdentity == null;
        AccountRow account;
        Guid identityId;
        if (existingIdentity == null)
        {
            CreatedExternalAccount created;
            try
            {
                created = await CreateGoogleAccountAsync(
                    connection,
                    transaction,
                    googleIdentity,
                    newAccountResourceGold,
                    newAccountTickets,
                    cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new AuthServiceException(
                    "google_identity_taken",
                    "This Google account was linked by another concurrent request. Please sign in again.");
            }

            account = created.Account;
            identityId = created.IdentityId;
        }
        else
        {
            account = existingIdentity.Account;
            identityId = existingIdentity.IdentityId;
            await UpdateGoogleIdentityProfileAsync(
                connection,
                transaction,
                identityId,
                googleIdentity,
                cancellationToken);
        }

        if (account.IsBanned ||
            !string.Equals(account.AccountStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthServiceException("account_inactive", "This account is not active.");
        }

        var wallet = await LoadWalletAsync(connection, transaction, account.Id, cancellationToken);
        if (isNewAccount)
        {
            wallet = await EnsureDevMinimumTicketsAsync(
                connection,
                transaction,
                account.Id,
                wallet,
                cancellationToken);
        }

        await InsertSessionPairAsync(
            connection,
            transaction,
            account.Id,
            tokenPair,
            clientContext,
            Guid.NewGuid(),
            cancellationToken);
        await TouchIdentityLoginAsync(
            connection,
            transaction,
            account.Id,
            GoogleProvider,
            googleIdentity.Subject,
            cancellationToken);
        await InsertIdentityEventAsync(
            connection,
            transaction,
            account.Id,
            identityId,
            GoogleProvider,
            "login",
            previousAccountId: null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GoogleAuthResponse(
            tokenPair.SessionToken,
            tokenPair.RefreshToken,
            ToAccountDto(account),
            wallet);
    }

    public async Task<LinkGoogleResponse> LinkGoogleAsync(
        AuthenticatedAccount authenticatedAccount,
        LinkGoogleRequest? request,
        CancellationToken cancellationToken)
    {
        if (authenticatedAccount == null)
        {
            throw new ArgumentNullException(nameof(authenticatedAccount));
        }

        var googleIdentity = await _googleTokenValidator.ValidateAsync(
            request?.IdToken,
            cancellationToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var linkedIdentity = await FindExternalIdentityAsync(
            connection,
            transaction,
            GoogleProvider,
            googleIdentity.Subject,
            cancellationToken);
        if (linkedIdentity != null && linkedIdentity.Account.Id != authenticatedAccount.Account.Id)
        {
            throw new AuthServiceException(
                "google_identity_taken",
                "This Google account is already linked to another After333 account.");
        }

        var accountGoogleIdentity = await FindAccountProviderIdentityAsync(
            connection,
            transaction,
            authenticatedAccount.Account.Id,
            GoogleProvider,
            cancellationToken);
        if (accountGoogleIdentity != null &&
            !string.Equals(
                accountGoogleIdentity.ProviderUserId,
                googleIdentity.Subject,
                StringComparison.Ordinal))
        {
            throw new AuthServiceException(
                "google_identity_already_linked",
                "This After333 account is already linked to a different Google account.");
        }

        Guid identityId;
        var wasAlreadyLinked = linkedIdentity != null;
        if (linkedIdentity != null)
        {
            identityId = linkedIdentity.IdentityId;
            await UpdateGoogleIdentityProfileAsync(
                connection,
                transaction,
                identityId,
                googleIdentity,
                cancellationToken);
        }
        else
        {
            try
            {
                identityId = await InsertGoogleIdentityAsync(
                    connection,
                    transaction,
                    authenticatedAccount.Account.Id,
                    googleIdentity,
                    cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new AuthServiceException(
                    "google_identity_taken",
                    "This Google account was linked by another concurrent request. Please try again.");
            }
        }

        var account = await PromoteAccountToRegisteredAsync(
            connection,
            transaction,
            authenticatedAccount.Account.Id,
            authenticatedAccount.Account.DisplayName,
            cancellationToken);
        var wallet = await LoadWalletAsync(connection, transaction, account.Id, cancellationToken);
        if (!wasAlreadyLinked)
        {
            await InsertIdentityEventAsync(
                connection,
                transaction,
                account.Id,
                identityId,
                GoogleProvider,
                "linked",
                previousAccountId: null,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new LinkGoogleResponse(
            ToAccountDto(account),
            wallet);
    }

    public async Task<RefreshAuthResponse> RefreshSessionAsync(
        RefreshAuthRequest? request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var refreshToken = NormalizeNullable(request?.RefreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthServiceException("missing_refresh_token", "Refresh token is required.");
        }

        var refreshTokenHash = _tokenService.HashToken(refreshToken);
        var clientContext = BuildClientContext(request?.ClientVersion, httpContext);
        var tokenPair = CreateSessionTokenPair();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var storedToken = await FindRefreshTokenForUpdateAsync(
            connection,
            transaction,
            refreshTokenHash,
            cancellationToken);
        if (storedToken == null)
        {
            throw new AuthServiceException("invalid_refresh_token", "Refresh token is invalid.");
        }

        if (storedToken.RevokedAt != null)
        {
            await RevokeRefreshTokenFamilyAsync(
                connection,
                transaction,
                storedToken.TokenFamilyId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new AuthServiceException(
                "refresh_token_reused",
                "This refresh token was already used. Please sign in again.");
        }

        if (storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await RevokeRefreshTokenAndSessionAsync(
                connection,
                transaction,
                storedToken.Id,
                storedToken.SessionId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new AuthServiceException(
                "refresh_token_expired",
                "Refresh token expired. Please sign in again.");
        }

        var account = storedToken.Account;
        if (account.IsBanned ||
            !string.Equals(account.AccountStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            await RevokeRefreshTokenFamilyAsync(
                connection,
                transaction,
                storedToken.TokenFamilyId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new AuthServiceException("account_inactive", "This account is not active.");
        }

        if (string.Equals(account.AccountKind, GuestAccountKind, StringComparison.OrdinalIgnoreCase))
        {
            await RevokeRefreshTokenFamilyAsync(
                connection,
                transaction,
                storedToken.TokenFamilyId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new AuthServiceException(
                "guest_accounts_disabled",
                "Guest accounts are no longer supported. Please sign in with Game ID or Google.");
        }

        var issuedSession = await InsertSessionPairAsync(
            connection,
            transaction,
            account.Id,
            tokenPair,
            clientContext,
            storedToken.TokenFamilyId,
            cancellationToken);
        await RotateRefreshTokenAsync(
            connection,
            transaction,
            storedToken.Id,
            storedToken.SessionId,
            issuedSession.RefreshTokenId,
            cancellationToken);

        var wallet = await LoadWalletAsync(connection, transaction, account.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RefreshAuthResponse(
            tokenPair.SessionToken,
            tokenPair.RefreshToken,
            ToAccountDto(account),
            wallet);
    }

    public async Task<LogoutAuthResponse> LogoutAsync(
        string? authorizationHeader,
        LogoutAuthRequest? request,
        CancellationToken cancellationToken)
    {
        var sessionToken = ExtractBearerToken(authorizationHeader);
        var refreshToken = NormalizeNullable(request?.RefreshToken);
        if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthServiceException(
                "missing_auth_token",
                "A session token or refresh token is required.");
        }

        var sessionTokenHash = string.IsNullOrWhiteSpace(sessionToken)
            ? null
            : _tokenService.HashToken(sessionToken);
        var refreshTokenHash = string.IsNullOrWhiteSpace(refreshToken)
            ? null
            : _tokenService.HashToken(refreshToken);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await RevokeCurrentDeviceSessionAsync(
            connection,
            transaction,
            sessionTokenHash,
            refreshTokenHash,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new LogoutAuthResponse(true);
    }

    public async Task<AuthenticatedAccount> GetCurrentAccountAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var sessionToken = ExtractBearerToken(authorizationHeader);
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new AuthServiceException("missing_session_token", "Authorization bearer token is required.");
        }

        var sessionTokenHash = _tokenService.HashToken(sessionToken);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var account = await FindAccountBySessionAsync(connection, transaction, sessionTokenHash, cancellationToken);
        if (account == null)
        {
            throw new AuthServiceException("invalid_session_token", "Session token is invalid, expired, or revoked.");
        }

        if (account.IsBanned ||
            !string.Equals(account.AccountStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthServiceException("account_inactive", "This account is not active.");
        }

        if (string.Equals(account.AccountKind, GuestAccountKind, StringComparison.OrdinalIgnoreCase))
        {
            await RevokeCurrentDeviceSessionAsync(
                connection,
                transaction,
                sessionTokenHash,
                refreshTokenHash: null,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new AuthServiceException(
                "guest_accounts_disabled",
                "Guest accounts are no longer supported. Please sign in with Game ID or Google.");
        }

        var wallet = await LoadWalletAsync(connection, transaction, account.Id, cancellationToken);
        var collectionSummary = await LoadCollectionSummaryAsync(connection, transaction, account.Id, cancellationToken);
        await TouchSessionAsync(connection, transaction, sessionTokenHash, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AuthenticatedAccount(
            ToAccountDto(account),
            wallet,
            collectionSummary);
    }

    public async Task<PurchaseTicketResponse> PurchaseTicketAsync(
        AuthenticatedAccount account,
        PurchaseTicketRequest? request,
        CancellationToken cancellationToken)
    {
        if (account == null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        var ticketCount = request?.TicketCount ?? 1;
        if (ticketCount <= 0 || ticketCount > 100)
        {
            throw new AuthServiceException("invalid_ticket_count", "Ticket purchase count must be between 1 and 100.");
        }

        var unitGoldCost = ResolveLong("PROJECT333_TICKET_PURCHASE_GOLD_COST", 3);
        if (unitGoldCost <= 0)
        {
            unitGoldCost = 3;
        }

        var resourceGoldCost = checked(unitGoldCost * ticketCount);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var wallet = await PurchaseTicketsWithResourceGoldAsync(
            connection,
            transaction,
            account.Account.Id,
            ticketCount,
            resourceGoldCost,
            cancellationToken);
        await InsertTicketPurchaseTransactionAsync(
            connection,
            transaction,
            account.Account.Id,
            ticketCount,
            resourceGoldCost,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new PurchaseTicketResponse(
            account.Account,
            wallet,
            ticketCount,
            resourceGoldCost);
    }

    private static async Task<RefreshTokenRow?> FindRefreshTokenForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string refreshTokenHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select
                rt.id,
                rt.session_id,
                rt.token_family_id,
                rt.expires_at,
                rt.revoked_at,
                a.id,
                a.display_name,
                a.account_kind,
                a.is_banned,
                a.account_status
            from auth_refresh_tokens rt
            join accounts a on a.id = rt.account_id
            where rt.refresh_token_hash = @refreshTokenHash
            limit 1
            for update of rt;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("refreshTokenHash", refreshTokenHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RefreshTokenRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            ReadUtcDateTimeOffset(reader, 3),
            reader.IsDBNull(4) ? null : ReadUtcDateTimeOffset(reader, 4),
            new AccountRow(
                reader.GetGuid(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetBoolean(8),
                reader.GetString(9)));
    }

    private static async Task RevokeRefreshTokenFamilyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tokenFamilyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update auth_sessions s
               set revoked_at = coalesce(s.revoked_at, now())
             where s.id in (
                 select rt.session_id
                   from auth_refresh_tokens rt
                  where rt.token_family_id = @tokenFamilyId
             );

            update auth_refresh_tokens
               set revoked_at = coalesce(revoked_at, now()),
                   last_used_at = now()
             where token_family_id = @tokenFamilyId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("tokenFamilyId", tokenFamilyId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RevokeRefreshTokenAndSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid refreshTokenId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update auth_sessions
               set revoked_at = coalesce(revoked_at, now())
             where id = @sessionId;

            update auth_refresh_tokens
               set revoked_at = coalesce(revoked_at, now()),
                   last_used_at = now()
             where id = @refreshTokenId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("refreshTokenId", refreshTokenId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RotateRefreshTokenAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid previousRefreshTokenId,
        Guid previousSessionId,
        Guid replacementRefreshTokenId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update auth_sessions
               set revoked_at = coalesce(revoked_at, now())
             where id = @previousSessionId;

            update auth_refresh_tokens
               set revoked_at = coalesce(revoked_at, now()),
                   last_used_at = now(),
                   replaced_by_token_id = @replacementRefreshTokenId
             where id = @previousRefreshTokenId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("previousSessionId", previousSessionId);
        command.Parameters.AddWithValue("previousRefreshTokenId", previousRefreshTokenId);
        command.Parameters.AddWithValue("replacementRefreshTokenId", replacementRefreshTokenId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RevokeCurrentDeviceSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string? sessionTokenHash,
        string? refreshTokenHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update auth_refresh_tokens rt
               set revoked_at = coalesce(rt.revoked_at, now()),
                   last_used_at = now()
             where (@refreshTokenHash <> '' and rt.refresh_token_hash = @refreshTokenHash)
                or (@sessionTokenHash <> '' and rt.session_id in (
                    select s.id
                      from auth_sessions s
                     where s.session_token_hash = @sessionTokenHash
                ));

            update auth_sessions s
               set revoked_at = coalesce(s.revoked_at, now())
             where (@sessionTokenHash <> '' and s.session_token_hash = @sessionTokenHash)
                or (@refreshTokenHash <> '' and s.id in (
                    select rt.session_id
                      from auth_refresh_tokens rt
                     where rt.refresh_token_hash = @refreshTokenHash
                ));
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("sessionTokenHash", sessionTokenHash ?? string.Empty);
        command.Parameters.AddWithValue("refreshTokenHash", refreshTokenHash ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<AccountRow?> FindGuestAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string guestTokenHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select
                a.id,
                a.display_name,
                a.account_kind,
                a.is_banned,
                a.account_status
            from auth_identities ai
            join accounts a on a.id = ai.account_id
            where ai.provider = @provider
              and ai.provider_user_id = @providerUserId
              and ai.identity_status = 'active'
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("provider", GuestProvider);
        command.Parameters.AddWithValue("providerUserId", guestTokenHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    private async Task<AccountRow?> FindAccountBySessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sessionTokenHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select
                a.id,
                a.display_name,
                a.account_kind,
                a.is_banned,
                a.account_status
            from auth_sessions s
            join accounts a on a.id = s.account_id
            where s.session_token_hash = @sessionTokenHash
              and s.revoked_at is null
              and s.expires_at > now()
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("sessionTokenHash", sessionTokenHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    private async Task<GameIdIdentityRow?> FindGameIdIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string normalizedGameId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select
                ai.id,
                ai.password_hash,
                a.id,
                a.display_name,
                a.account_kind,
                a.is_banned,
                a.account_status
            from auth_identities ai
            join accounts a on a.id = ai.account_id
            where ai.provider = @provider
              and ai.normalized_provider_user_id = @normalizedProviderUserId
              and ai.identity_status = 'active'
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("provider", GameIdProvider);
        command.Parameters.AddWithValue("normalizedProviderUserId", normalizedGameId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new GameIdIdentityRow(
            reader.GetGuid(0),
            reader.GetString(1),
            new AccountRow(
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                reader.GetString(6)));
    }

    private static async Task<ExternalIdentityRow?> FindExternalIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string provider,
        string providerUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select
                ai.id,
                a.id,
                a.display_name,
                a.account_kind,
                a.is_banned,
                a.account_status
            from auth_identities ai
            join accounts a on a.id = ai.account_id
            where ai.provider = @provider
              and ai.normalized_provider_user_id = @normalizedProviderUserId
              and ai.identity_status = 'active'
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("normalizedProviderUserId", providerUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ExternalIdentityRow(
            reader.GetGuid(0),
            new AccountRow(
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetString(5)));
    }

    private static async Task<AccountProviderIdentityRow?> FindAccountProviderIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string provider,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select id, provider_user_id
            from auth_identities
            where account_id = @accountId
              and provider = @provider
              and identity_status = 'active'
            order by created_at
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("provider", provider);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AccountProviderIdentityRow(reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    private static async Task<bool> GameIdIdentityExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string normalizedGameId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select exists (
                select 1
                from auth_identities
                where provider = @provider
                  and normalized_provider_user_id = @normalizedProviderUserId
                  and identity_status = 'active');
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("provider", GameIdProvider);
        command.Parameters.AddWithValue("normalizedProviderUserId", normalizedGameId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private async Task<AccountRow> CreateGameIdAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string normalizedGameId,
        string displayName,
        string passwordHash,
        long startingResourceGold,
        int startingTickets,
        CancellationToken cancellationToken)
    {
        const string accountSql = """
            insert into accounts(display_name, account_kind, last_login_at)
            values (@displayName, @accountKind, now())
            returning id, display_name, account_kind, is_banned, account_status;
            """;

        await using var accountCommand = new NpgsqlCommand(accountSql, connection, transaction);
        accountCommand.Parameters.AddWithValue("displayName", displayName);
        accountCommand.Parameters.AddWithValue("accountKind", RegisteredAccountKind);
        await using var reader = await accountCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Failed to create Game ID account.");
        }

        var account = ReadAccount(reader);
        await reader.CloseAsync();

        var identityId = await InsertGameIdIdentityAsync(
            connection,
            transaction,
            account.Id,
            normalizedGameId,
            passwordHash,
            cancellationToken);

        const string walletSql = """
            insert into user_wallets(account_id, resource_gold, tickets)
            values (@accountId, @resourceGold, @tickets);
            """;
        await using (var walletCommand = new NpgsqlCommand(walletSql, connection, transaction))
        {
            walletCommand.Parameters.AddWithValue("accountId", account.Id);
            walletCommand.Parameters.AddWithValue("resourceGold", startingResourceGold);
            walletCommand.Parameters.AddWithValue("tickets", startingTickets);
            await walletCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertIdentityEventAsync(
            connection,
            transaction,
            account.Id,
            identityId,
            GameIdProvider,
            "created",
            previousAccountId: null,
            cancellationToken);

        return account;
    }

    private async Task<CreatedExternalAccount> CreateGoogleAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        VerifiedGoogleIdentity googleIdentity,
        long startingResourceGold,
        int startingTickets,
        CancellationToken cancellationToken)
    {
        const string accountSql = """
            insert into accounts(display_name, account_kind, last_login_at)
            values (@displayName, @accountKind, now())
            returning id, display_name, account_kind, is_banned, account_status;
            """;

        await using var accountCommand = new NpgsqlCommand(accountSql, connection, transaction);
        accountCommand.Parameters.AddWithValue("displayName", googleIdentity.DisplayName);
        accountCommand.Parameters.AddWithValue("accountKind", RegisteredAccountKind);
        await using var reader = await accountCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Failed to create Google account.");
        }

        var account = ReadAccount(reader);
        await reader.CloseAsync();

        var identityId = await InsertGoogleIdentityAsync(
            connection,
            transaction,
            account.Id,
            googleIdentity,
            cancellationToken);

        const string walletSql = """
            insert into user_wallets(account_id, resource_gold, tickets)
            values (@accountId, @resourceGold, @tickets);
            """;
        await using (var walletCommand = new NpgsqlCommand(walletSql, connection, transaction))
        {
            walletCommand.Parameters.AddWithValue("accountId", account.Id);
            walletCommand.Parameters.AddWithValue("resourceGold", startingResourceGold);
            walletCommand.Parameters.AddWithValue("tickets", startingTickets);
            await walletCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertIdentityEventAsync(
            connection,
            transaction,
            account.Id,
            identityId,
            GoogleProvider,
            "created",
            previousAccountId: null,
            cancellationToken);

        return new CreatedExternalAccount(account, identityId);
    }

    private static async Task<Guid> InsertGoogleIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        VerifiedGoogleIdentity googleIdentity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into auth_identities(
                account_id,
                provider,
                provider_user_id,
                normalized_provider_user_id,
                email,
                email_verified_at,
                external_profile,
                last_used_at)
            values (
                @accountId,
                @provider,
                @providerUserId,
                @normalizedProviderUserId,
                @email,
                case when @emailVerified then now() else null end,
                cast(@externalProfile as jsonb),
                now())
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("provider", GoogleProvider);
        command.Parameters.AddWithValue("providerUserId", googleIdentity.Subject);
        command.Parameters.AddWithValue("normalizedProviderUserId", googleIdentity.Subject);
        command.Parameters.AddWithValue("email", (object?)googleIdentity.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("emailVerified", googleIdentity.EmailVerified);
        command.Parameters.AddWithValue("externalProfile", BuildGoogleExternalProfileJson(googleIdentity));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid identityId
            ? identityId
            : throw new InvalidOperationException("Failed to create Google identity.");
    }

    private static async Task UpdateGoogleIdentityProfileAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid identityId,
        VerifiedGoogleIdentity googleIdentity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update auth_identities
               set email = @email,
                   email_verified_at = case
                       when @emailVerified then coalesce(email_verified_at, now())
                       else email_verified_at
                   end,
                   external_profile = cast(@externalProfile as jsonb),
                   last_used_at = now(),
                   updated_at = now()
             where id = @identityId
               and provider = @provider
               and identity_status = 'active';
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("identityId", identityId);
        command.Parameters.AddWithValue("provider", GoogleProvider);
        command.Parameters.AddWithValue("email", (object?)googleIdentity.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("emailVerified", googleIdentity.EmailVerified);
        command.Parameters.AddWithValue("externalProfile", BuildGoogleExternalProfileJson(googleIdentity));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildGoogleExternalProfileJson(VerifiedGoogleIdentity googleIdentity)
    {
        return JsonSerializer.Serialize(new
        {
            googleIdentity.EmailVerified,
            googleIdentity.DisplayName,
            googleIdentity.PictureUrl,
            googleIdentity.Locale,
            googleIdentity.Audience
        });
    }

    private static async Task<Guid> InsertGameIdIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string normalizedGameId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into auth_identities(
                account_id,
                provider,
                provider_user_id,
                normalized_provider_user_id,
                password_hash,
                password_algorithm,
                password_updated_at,
                last_used_at)
            values (
                @accountId,
                @provider,
                @providerUserId,
                @normalizedProviderUserId,
                @passwordHash,
                @passwordAlgorithm,
                now(),
                now())
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("provider", GameIdProvider);
        command.Parameters.AddWithValue("providerUserId", normalizedGameId);
        command.Parameters.AddWithValue("normalizedProviderUserId", normalizedGameId);
        command.Parameters.AddWithValue("passwordHash", passwordHash);
        command.Parameters.AddWithValue("passwordAlgorithm", PasswordAlgorithm);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid identityId
            ? identityId
            : throw new InvalidOperationException("Failed to create Game ID identity.");
    }

    private static async Task<AccountRow> PromoteAccountToRegisteredAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string displayName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update accounts
               set account_kind = @accountKind,
                   display_name = @displayName,
                   updated_at = now()
             where id = @accountId
             returning id, display_name, account_kind, is_banned, account_status;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("accountKind", RegisteredAccountKind);
        command.Parameters.AddWithValue("displayName", displayName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Account was not found for account '{accountId}'.");
        }

        return ReadAccount(reader);
    }

    private async Task<AccountRow> CreateGuestAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string guestTokenHash,
        long startingResourceGold,
        int startingTickets,
        CancellationToken cancellationToken)
    {
        var displayName = $"Guest{RandomNumberGenerator.GetInt32(1000, 10000)}";

        const string accountSql = """
            insert into accounts(display_name, account_kind, last_login_at)
            values (@displayName, @accountKind, now())
            returning id, display_name, account_kind, is_banned, account_status;
            """;

        await using var accountCommand = new NpgsqlCommand(accountSql, connection, transaction);
        accountCommand.Parameters.AddWithValue("displayName", displayName);
        accountCommand.Parameters.AddWithValue("accountKind", GuestAccountKind);
        await using var reader = await accountCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Failed to create guest account.");
        }

        var account = ReadAccount(reader);
        await reader.CloseAsync();

        const string identitySql = """
            insert into auth_identities(
                account_id,
                provider,
                provider_user_id,
                normalized_provider_user_id,
                last_used_at)
            values (
                @accountId,
                @provider,
                @providerUserId,
                @normalizedProviderUserId,
                now());
            """;
        await using (var identityCommand = new NpgsqlCommand(identitySql, connection, transaction))
        {
            identityCommand.Parameters.AddWithValue("accountId", account.Id);
            identityCommand.Parameters.AddWithValue("provider", GuestProvider);
            identityCommand.Parameters.AddWithValue("providerUserId", guestTokenHash);
            identityCommand.Parameters.AddWithValue("normalizedProviderUserId", guestTokenHash);
            await identityCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertIdentityEventAsync(
            connection,
            transaction,
            account.Id,
            authIdentityId: null,
            GuestProvider,
            "created",
            previousAccountId: null,
            cancellationToken);

        const string walletSql = """
            insert into user_wallets(account_id, resource_gold, tickets)
            values (@accountId, @resourceGold, @tickets);
            """;
        await using (var walletCommand = new NpgsqlCommand(walletSql, connection, transaction))
        {
            walletCommand.Parameters.AddWithValue("accountId", account.Id);
            walletCommand.Parameters.AddWithValue("resourceGold", startingResourceGold);
            walletCommand.Parameters.AddWithValue("tickets", startingTickets);
            await walletCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return account;
    }

    private static async Task<WalletDto> LoadWalletAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select resource_gold, tickets
            from user_wallets
            where account_id = @accountId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Wallet was not found for account '{accountId}'.");
        }

        return new WalletDto(
            reader.GetInt64(0),
            reader.GetInt32(1));
    }

    private static async Task<WalletDto> PurchaseTicketsWithResourceGoldAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        int ticketCount,
        long resourceGoldCost,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update user_wallets
               set resource_gold = resource_gold - @resourceGoldCost,
                   tickets = tickets + @ticketCount,
                   updated_at = now()
             where account_id = @accountId
               and resource_gold >= @resourceGoldCost
             returning resource_gold, tickets;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("resourceGoldCost", resourceGoldCost);
        command.Parameters.AddWithValue("ticketCount", ticketCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new AuthServiceException(
                "insufficient_resource_gold",
                $"Not enough resource gold. Ticket purchase requires {resourceGoldCost} gold.");
        }

        return new WalletDto(
            reader.GetInt64(0),
            reader.GetInt32(1));
    }

    private static async Task InsertTicketPurchaseTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        int ticketCount,
        long resourceGoldCost,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into account_transactions(
                account_id,
                transaction_type,
                resource_gold_delta,
                ticket_delta,
                source_table)
            values (
                @accountId,
                'purchase',
                @resourceGoldDelta,
                @ticketDelta,
                'ticket_purchase');
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("resourceGoldDelta", -resourceGoldCost);
        command.Parameters.AddWithValue("ticketDelta", ticketCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<WalletDto> EnsureDevMinimumTicketsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        WalletDto wallet,
        CancellationToken cancellationToken)
    {
        var devMinimumTickets = ResolveInt("PROJECT333_DEV_MIN_TICKETS", -1);
        if (devMinimumTickets < 0 || wallet.Tickets >= devMinimumTickets)
        {
            return wallet;
        }

        const string sql = """
            update user_wallets
            set
                tickets = @tickets,
                updated_at = now()
            where account_id = @accountId
            returning resource_gold, tickets;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("tickets", devMinimumTickets);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Wallet was not found for account '{accountId}'.");
        }

        return new WalletDto(
            reader.GetInt64(0),
            reader.GetInt32(1));
    }

    private static async Task<CollectionSummaryDto> LoadCollectionSummaryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select card_id, copy_count, upgrade_level
            from user_card_collection
            where account_id = @accountId
              and (copy_count > 0 or upgrade_level > 0)
            order by card_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var ownedCards = new List<OwnedCardDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            ownedCards.Add(new OwnedCardDto(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));
        }

        return new CollectionSummaryDto(ownedCards.Count, ownedCards);
    }

    private static async Task<IssuedSessionRow> InsertSessionPairAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        SessionTokenPair tokenPair,
        ClientRequestContext clientContext,
        Guid tokenFamilyId,
        CancellationToken cancellationToken)
    {
        var sessionId = await InsertSessionAsync(
            connection,
            transaction,
            accountId,
            tokenPair.SessionTokenHash,
            tokenPair.SessionExpiresAt,
            clientContext.UserAgent,
            clientContext.ClientVersion,
            clientContext.RemoteIp,
            cancellationToken);
        var refreshTokenId = await InsertRefreshTokenAsync(
            connection,
            transaction,
            accountId,
            sessionId,
            tokenFamilyId,
            tokenPair.RefreshTokenHash,
            tokenPair.RefreshExpiresAt,
            clientContext.UserAgent,
            clientContext.ClientVersion,
            clientContext.RemoteIp,
            cancellationToken);

        return new IssuedSessionRow(sessionId, refreshTokenId);
    }

    private static async Task<Guid> InsertSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string sessionTokenHash,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? clientVersion,
        string? remoteIp,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into auth_sessions(
                account_id,
                session_token_hash,
                expires_at,
                user_agent,
                client_version,
                ip_address)
            values (
                @accountId,
                @sessionTokenHash,
                @expiresAt,
                @userAgent,
                @clientVersion,
                cast(@ipAddress as inet))
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("sessionTokenHash", sessionTokenHash);
        command.Parameters.AddWithValue("expiresAt", expiresAt);
        command.Parameters.AddWithValue("userAgent", (object?)userAgent ?? DBNull.Value);
        command.Parameters.AddWithValue("clientVersion", (object?)clientVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("ipAddress", (object?)remoteIp ?? DBNull.Value);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid sessionId
            ? sessionId
            : throw new InvalidOperationException("Failed to create an auth session.");
    }

    private static async Task<Guid> InsertRefreshTokenAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid sessionId,
        Guid tokenFamilyId,
        string refreshTokenHash,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? clientVersion,
        string? remoteIp,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into auth_refresh_tokens(
                account_id,
                session_id,
                token_family_id,
                refresh_token_hash,
                expires_at,
                user_agent,
                client_version,
                ip_address)
            values (
                @accountId,
                @sessionId,
                @tokenFamilyId,
                @refreshTokenHash,
                @expiresAt,
                @userAgent,
                @clientVersion,
                cast(@ipAddress as inet))
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("tokenFamilyId", tokenFamilyId);
        command.Parameters.AddWithValue("refreshTokenHash", refreshTokenHash);
        command.Parameters.AddWithValue("expiresAt", expiresAt);
        command.Parameters.AddWithValue("userAgent", (object?)userAgent ?? DBNull.Value);
        command.Parameters.AddWithValue("clientVersion", (object?)clientVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("ipAddress", (object?)remoteIp ?? DBNull.Value);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid refreshTokenId
            ? refreshTokenId
            : throw new InvalidOperationException("Failed to create an auth refresh token.");
    }

    private static async Task TouchLoginAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string guestTokenHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update accounts
               set last_login_at = now(),
                   updated_at = now()
             where id = @accountId;

            update auth_identities
               set last_used_at = now(),
                   updated_at = now()
             where provider = @provider
               and provider_user_id = @providerUserId
               and identity_status = 'active';
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("provider", GuestProvider);
        command.Parameters.AddWithValue("providerUserId", guestTokenHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task TouchIdentityLoginAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string provider,
        string normalizedProviderUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update accounts
               set last_login_at = now(),
                   updated_at = now()
             where id = @accountId;

            update auth_identities
               set last_used_at = now(),
                   updated_at = now()
             where provider = @provider
               and normalized_provider_user_id = @normalizedProviderUserId
               and identity_status = 'active';
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("normalizedProviderUserId", normalizedProviderUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertIdentityEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid? authIdentityId,
        string provider,
        string eventType,
        Guid? previousAccountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into auth_identity_link_events(
                account_id,
                auth_identity_id,
                provider,
                event_type,
                previous_account_id)
            values (
                @accountId,
                @authIdentityId,
                @provider,
                @eventType,
                @previousAccountId);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("authIdentityId", (object?)authIdentityId ?? DBNull.Value);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("previousAccountId", (object?)previousAccountId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task TouchSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sessionTokenHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update auth_sessions
               set last_used_at = now()
             where session_token_hash = @sessionTokenHash;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("sessionTokenHash", sessionTokenHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private TimeSpan ResolveSessionLifetime()
    {
        var days = ResolveInt("PROJECT333_SESSION_TOKEN_DAYS", 7);
        return TimeSpan.FromDays(Math.Max(1, days));
    }

    private TimeSpan ResolveRefreshTokenLifetime()
    {
        var days = ResolveInt("PROJECT333_REFRESH_TOKEN_DAYS", 30);
        return TimeSpan.FromDays(Math.Max(1, days));
    }

    private SessionTokenPair CreateSessionTokenPair()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionToken = _tokenService.GenerateToken();
        var refreshToken = _tokenService.GenerateToken();
        return new SessionTokenPair(
            sessionToken,
            _tokenService.HashToken(sessionToken),
            now + ResolveSessionLifetime(),
            refreshToken,
            _tokenService.HashToken(refreshToken),
            now + ResolveRefreshTokenLifetime());
    }

    private int ResolveInt(string key, int fallback)
    {
        return int.TryParse(_configuration[key], out var value) ? value : fallback;
    }

    private long ResolveLong(string key, long fallback)
    {
        return long.TryParse(_configuration[key], out var value) ? value : fallback;
    }

    private static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        return authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[bearerPrefix.Length..].Trim()
            : null;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRegistrationGameId(string? gameId)
    {
        if (!GameIdRegistrationRules.TryNormalizeGameId(
                gameId ?? string.Empty,
                out var normalized,
                out var errorMessage))
        {
            throw new AuthServiceException("invalid_game_id", errorMessage);
        }

        return normalized;
    }

    private static string NormalizeLoginGameId(string? gameId)
    {
        var normalized = gameId?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length < LegacyGameIdMinLength ||
            normalized.Length > LegacyGameIdMaxLength)
        {
            throw new AuthServiceException(
                "invalid_game_id",
                $"Game ID must be {LegacyGameIdMinLength}-{LegacyGameIdMaxLength} characters.");
        }

        for (var i = 0; i < normalized.Length; i++)
        {
            var c = normalized[i];
            var isValid =
                c is >= 'a' and <= 'z' ||
                c is >= '0' and <= '9' ||
                c == '_' ||
                c == '-' ||
                c == '.';
            if (!isValid)
            {
                throw new AuthServiceException(
                    "invalid_game_id",
                    "Game ID contains an unsupported character.");
            }
        }

        return normalized;
    }

    private static string ValidateRegistrationPassword(string? password)
    {
        if (!GameIdRegistrationRules.TryValidatePassword(
                password ?? string.Empty,
                out var errorMessage))
        {
            throw new AuthServiceException("invalid_password", errorMessage);
        }

        return password!;
    }

    private static string ValidateLoginPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > PasswordMaxLength)
        {
            throw new AuthServiceException(
                "invalid_password",
                "Password is invalid.");
        }

        return password;
    }

    private static string NormalizeDisplayName(string? displayName, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(displayName)
            ? fallback
            : displayName.Trim();
        if (normalized.Length > DisplayNameMaxLength)
        {
            throw new AuthServiceException(
                "invalid_display_name",
                $"Display name must be {DisplayNameMaxLength} characters or fewer.");
        }

        return normalized;
    }

    private static ClientRequestContext BuildClientContext(string? clientVersion, HttpContext httpContext)
    {
        return new ClientRequestContext(
            NormalizeNullable(clientVersion),
            NormalizeNullable(httpContext.Request.Headers["User-Agent"].ToString()),
            httpContext.Connection.RemoteIpAddress?.ToString());
    }

    private static string HashPassword(string password)
    {
        var salt = new byte[PasswordSaltByteLength];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            PasswordHashByteLength);
        return $"{PasswordHashIterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedPasswordHash)
    {
        var parts = storedPasswordHash.Split(':');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var iterations) ||
            iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static AccountRow ReadAccount(NpgsqlDataReader reader)
    {
        return new AccountRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetString(4));
    }

    private static AuthAccountDto ToAccountDto(AccountRow row)
    {
        return new AuthAccountDto(row.Id, row.DisplayName, row.AccountKind);
    }

    private static DateTimeOffset ReadUtcDateTimeOffset(NpgsqlDataReader reader, int ordinal)
    {
        var value = reader.GetDateTime(ordinal);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private sealed record AccountRow(
        Guid Id,
        string DisplayName,
        string AccountKind,
        bool IsBanned,
        string AccountStatus);

    private sealed record GameIdIdentityRow(
        Guid IdentityId,
        string PasswordHash,
        AccountRow Account);

    private sealed record ExternalIdentityRow(
        Guid IdentityId,
        AccountRow Account);

    private sealed record AccountProviderIdentityRow(
        Guid IdentityId,
        string ProviderUserId);

    private sealed record CreatedExternalAccount(
        AccountRow Account,
        Guid IdentityId);

    private sealed record SessionTokenPair(
        string SessionToken,
        string SessionTokenHash,
        DateTimeOffset SessionExpiresAt,
        string RefreshToken,
        string RefreshTokenHash,
        DateTimeOffset RefreshExpiresAt);

    private sealed record IssuedSessionRow(
        Guid SessionId,
        Guid RefreshTokenId);

    private sealed record RefreshTokenRow(
        Guid Id,
        Guid SessionId,
        Guid TokenFamilyId,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RevokedAt,
        AccountRow Account);

    private sealed record ClientRequestContext(
        string? ClientVersion,
        string? UserAgent,
        string? RemoteIp);
}
