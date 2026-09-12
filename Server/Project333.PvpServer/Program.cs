using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.PvpServer.Ads;
using Project333.PvpServer.AccountOperations;
using Project333.PvpServer.Auth;
using Project333.PvpServer.BattlePersistence;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.BattleResults;
using Project333.PvpServer.Cards;
using Project333.PvpServer.Matchmaking;
using Project333.PvpServer.Messages;
using Project333.PvpServer.Observability;
using Project333.PvpServer.Persistence;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;
using Project333.PvpServer.Security;

var builder = WebApplication.CreateBuilder(args);
var listenUrl = builder.Configuration["PROJECT333_PVP_SERVER_URL"] ?? "http://127.0.0.1:7333";
var publicServerUrl = ResolvePublicServerUrl(
    builder.Configuration["PROJECT333_PUBLIC_SERVER_URL"],
    listenUrl);
var disconnectReconnectGracePeriod = TimeSpan.FromSeconds(60);
var clientHeartbeatTimeout = TimeSpan.FromSeconds(12);
var clientHeartbeatPollInterval = TimeSpan.FromSeconds(2);
var rateLimitSettings = Project333RateLimitSettings.FromConfiguration(builder.Configuration);
var battleLimits = BattleConnectionLimitSettings.FromConfiguration(builder.Configuration);
var battleAccounts = new BattleAccountConnections(battleLimits.MaxConnectionsPerAccount);
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddSingleton<AuthTokenService>();
builder.Services.AddSingleton<GoogleIdentityTokenValidator>();
builder.Services.AddHttpClient<GoogleDesktopAuthorizationCodeExchanger>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<GuestAuthService>();
builder.Services.AddSingleton<RewardedAdService>();
builder.Services.AddSingleton<RunStartService>();
builder.Services.AddSingleton<CardUpgradeService>();
builder.Services.AddSingleton<BattleCardUpgradeLevelService>();
builder.Services.AddSingleton<PvpMatchmakingService>();
builder.Services.AddSingleton<PvpBattlePersistenceService>();
builder.Services.AddSingleton<AuditLogService>();
builder.Services.AddSingleton<IBattleResultStore, BattleResultStore>();
builder.Services.AddSingleton<BattleResultOutbox>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<BattleResultOutbox>());
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
});
builder.Services.AddRateLimiter(options =>
    Project333RateLimiting.Configure(options, rateLimitSettings));
builder.WebHost.UseUrls(listenUrl);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);

await DbMigrationRunner.RunFromConfigurationAsync(builder.Configuration, builder.Environment, CancellationToken.None);
_ = PrototypeCardDefinitions.LoadCardDefinitionProvider();
Console.WriteLine("[cards] cards.json validation passed.");
Console.WriteLine($"[pve-ai] {PveAiDeckCatalog.Default.Decks.Count} fixed decks validated (33 cards; L1/U4/R9/UC11/C8).");

var app = builder.Build();
var guestAuthService = app.Services.GetRequiredService<GuestAuthService>();
var runStartService = app.Services.GetRequiredService<RunStartService>();
var battleCardUpgradeLevelService = app.Services.GetRequiredService<BattleCardUpgradeLevelService>();
var pvpMatchmakingService = app.Services.GetRequiredService<PvpMatchmakingService>();
var pvpBattlePersistenceService = app.Services.GetRequiredService<PvpBattlePersistenceService>();
var sessionManager = new BattleSessionManager(app.Services.GetRequiredService<BattleResultOutbox>());
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};
jsonOptions.Converters.Add(new JsonStringEnumConverter());

if (IsPvpStartupRecoveryEnabled(builder.Configuration))
{
    var startupRecoveryResult = await pvpMatchmakingService.MarkRecoverableMatchesReconnectGraceOnServerStartupAsync(
        disconnectReconnectGracePeriod,
        CancellationToken.None);
    if (startupRecoveryResult.MatchCount > 0 ||
        startupRecoveryResult.ClosedConnectionCount > 0 ||
        startupRecoveryResult.ReconnectablePlayerCount > 0)
    {
        Console.WriteLine(
            $"[pvp-recovery] Startup marked {startupRecoveryResult.MatchCount} match(es), {startupRecoveryResult.ClosedConnectionCount} connection(s), {startupRecoveryResult.ReconnectablePlayerCount} player seat(s) as reconnectable.");
    }
}
else
{
    Console.WriteLine("[pvp-recovery] Startup recovery is disabled. Set PROJECT333_ENABLE_PVP_STARTUP_RECOVERY=1 to enable server-restart battle recovery.");
}

app.UseForwardedHeaders();
app.UseRateLimiter();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(20)
});

app.MapGet("/", () => Results.Json(new
{
    Name = "After333 PvP Server",
    Status = "Running",
    Health = "/health",
    ServerStatus = "/server/status",
    BattleWebSocket = "/battle",
    Sessions = "/sessions"
}));

app.MapGet("/health", () => Results.Json(new
{
    Name = "After333 PvP Server",
    Status = "Ok",
    ServerTimeUtc = DateTimeOffset.UtcNow.ToString("O")
}, jsonOptions));

app.MapGet("/server/status", async (
    DbConnectionFactory dbConnectionFactory,
    AuditLogService auditLogService,
    GoogleDesktopAuthorizationCodeExchanger googleDesktopCodeExchanger,
    RewardedAdService rewardedAdService,
    HttpContext context) =>
{
    var databaseStatus = await GetDatabaseStatusAsync(
        dbConnectionFactory,
        context.RequestAborted);
    var cardDatabaseStatus = GetCardDatabaseStatus();
    var isOk = string.Equals(cardDatabaseStatus.Status, "Ok", StringComparison.OrdinalIgnoreCase) &&
               (!databaseStatus.Configured ||
                string.Equals(databaseStatus.Status, "Ok", StringComparison.OrdinalIgnoreCase));

    return Results.Json(new
    {
        Name = "After333 PvP Server",
        Status = isOk ? "Ok" : "Degraded",
        ServerTimeUtc = DateTimeOffset.UtcNow.ToString("O"),
        Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
        Environment = app.Environment.EnvironmentName,
        ListenUrl = listenUrl,
        PublicUrl = publicServerUrl,
        Endpoints = new
        {
            Health = "/health",
            ServerStatus = "/server/status",
            BattleWebSocket = "/battle",
            Sessions = "/sessions",
            DatabaseHealth = "/health/db",
            GameIdRegister = "/auth/register",
            GameIdLogin = "/auth/login",
            RefreshLogin = "/auth/refresh",
            Logout = "/auth/logout",
            GoogleDesktopTokenExchange = "/auth/google/desktop-token",
            GoogleLogin = "/auth/google",
            RewardedAdAttempt = "/ads/rewarded-ticket/attempt",
            LevelPlayRewardCallback = "/ads/levelplay/rewarded-callback"
        },
        Database = databaseStatus,
        Cards = cardDatabaseStatus,
        Deployment = GetDeploymentStatus(
            builder.Configuration,
            app.Environment,
            listenUrl,
            publicServerUrl,
            databaseStatus,
            cardDatabaseStatus,
            rateLimitSettings),
        Audit = new
        {
            auditLogService.Enabled,
            auditLogService.LogDirectory,
            auditLogService.RetentionDays
        },
        PvP = new
        {
            ReconnectGraceSeconds = (int)Math.Ceiling(disconnectReconnectGracePeriod.TotalSeconds),
            StartupRecoveryEnabled = IsPvpStartupRecoveryEnabled(builder.Configuration),
            VerboseTransportLogsEnabled = ShouldLogVerboseTransport()
        },
        ClientCompatibility = new
        {
            RequiredClientVersion = builder.Configuration["PROJECT333_REQUIRED_CLIENT_VERSION"] ?? string.Empty,
            RecommendedClientVersion = builder.Configuration["PROJECT333_RECOMMENDED_CLIENT_VERSION"] ?? string.Empty,
            CardDefinitionVersion = builder.Configuration["PROJECT333_CARD_DEFINITION_VERSION"] ?? string.Empty,
            VersionEnforcementEnabled = IsClientVersionEnforced(builder.Configuration)
        },
        Authentication = new
        {
            Google = new
            {
                Configured = guestAuthService.IsGoogleAuthConfigured,
                AcceptedClientIdCount = guestAuthService.GoogleAcceptedClientIdCount,
                DesktopCodeExchangeConfigured = googleDesktopCodeExchanger.IsConfigured
            }
        },
        RewardedAds = new
        {
            Provider = RewardedAdService.ProviderName,
            CallbackConfigured = rewardedAdService.IsCallbackConfigured,
            AppKeyConfigured = rewardedAdService.IsAppKeyConfigured,
            rewardedAdService.PlacementName,
            rewardedAdService.RewardTicketCount,
            rewardedAdService.DailyLimit,
            rewardedAdService.CooldownSeconds
        },
        RateLimits = new
        {
            rateLimitSettings.Enabled,
            rateLimitSettings.WindowSeconds,
            rateLimitSettings.AuthPermitLimit,
            rateLimitSettings.AccountApiPermitLimit,
            rateLimitSettings.BattleConnectPermitLimit,
            PartitionKey = "client_ip"
        }
    }, jsonOptions);
});

app.MapGet("/sessions", () => Results.Json(sessionManager.SnapshotSessions()));

app.MapGet("/pvp/reconnect-status", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    PvpMatchmakingService pvpMatchmakingService,
    PvpBattlePersistenceService pvpBattlePersistenceService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !pvpMatchmakingService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var accountId = account.Account.Id.ToString("D");
        var dbReconnectStatus = await pvpMatchmakingService.GetReconnectStatusForAccountAsync(
            accountId,
            context.RequestAborted);
        var memoryReconnectStatus = sessionManager.FindReconnectStatus(accountId);
        PvpReconnectStatusResponse response;
        if (dbReconnectStatus != null &&
            memoryReconnectStatus != null &&
            string.Equals(dbReconnectStatus.MatchId, memoryReconnectStatus.MatchId, StringComparison.Ordinal))
        {
            response = new PvpReconnectStatusResponse(
                true,
                memoryReconnectStatus.MatchId,
                memoryReconnectStatus.SeatId.ToString(),
                memoryReconnectStatus.OnlineSeatId.ToString(),
                Math.Min(memoryReconnectStatus.RemainingSeconds, dbReconnectStatus.RemainingSeconds),
                memoryReconnectStatus.ReconnectDeadlineUtc < dbReconnectStatus.ReconnectDeadlineUtc
                    ? memoryReconnectStatus.ReconnectDeadlineUtc.ToString("O")
                    : dbReconnectStatus.ReconnectDeadlineUtc.ToString("O"));
        }
        else if (dbReconnectStatus != null &&
                 await HasRestorablePvpBattleSnapshotAsync(
                     dbReconnectStatus.MatchId,
                     pvpBattlePersistenceService,
                     jsonOptions,
                     context.RequestAborted))
        {
            response = new PvpReconnectStatusResponse(
                true,
                dbReconnectStatus.MatchId,
                dbReconnectStatus.RuntimePlayerId,
                dbReconnectStatus.OnlineSeatId,
                dbReconnectStatus.RemainingSeconds,
                dbReconnectStatus.ReconnectDeadlineUtc.ToString("O"));
        }
        else
        {
            response = new PvpReconnectStatusResponse(false, null, null, null, 0, null);
        }

        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, AuthStatusCode(ex));
    }
});

app.MapGet("/health/db", async (
    DbConnectionFactory dbConnectionFactory,
    HttpContext context) =>
{
    if (!dbConnectionFactory.IsConfigured)
    {
        return Results.Json(new
        {
            Status = "NotConfigured",
            Message = "PROJECT333_DB_CONNECTION is not set."
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var connection = await dbConnectionFactory.OpenConnectionAsync(context.RequestAborted);
        await using var command = connection.CreateCommand();
        command.CommandText = "select version from schema_migrations order by version;";
        var migrations = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        while (await reader.ReadAsync(context.RequestAborted))
        {
            migrations.Add(reader.GetString(0));
        }

        return Results.Json(new
        {
            Status = "Ok",
            AppliedMigrations = migrations
        }, jsonOptions);
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            Status = "Error",
            Message = ex.Message
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/auth/register", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    AuditLogService auditLogService) =>
{
    if (!guestAuthService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    GameIdAuthRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<GameIdAuthRequest>(
            context.Request.Body,
            jsonOptions,
            context.RequestAborted);
    }
    catch (JsonException ex)
    {
        return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
    }

    try
    {
        var response = await guestAuthService.RegisterGameIdAsync(request, context, context.RequestAborted);
        await auditLogService.LogAsync(
            "auth.game_id.register.success",
            new
            {
                accountId = response.Account.Id,
                response.Account.DisplayName,
                response.Account.AccountKind,
                gameId = request?.GameId,
                clientVersion = request?.ClientVersion,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        await auditLogService.LogAsync(
            "auth.game_id.register.failed",
            new
            {
                ex.Code,
                gameId = request?.GameId,
                clientVersion = request?.ClientVersion,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return AuthErrorResult(ex.Code, ex.Message, AuthStatusCode(ex));
    }
});

app.MapPost("/auth/login", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    AuditLogService auditLogService) =>
{
    if (!guestAuthService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    GameIdAuthRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<GameIdAuthRequest>(
            context.Request.Body,
            jsonOptions,
            context.RequestAborted);
    }
    catch (JsonException ex)
    {
        return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
    }

    try
    {
        var response = await guestAuthService.LoginGameIdAsync(request, context, context.RequestAborted);
        await auditLogService.LogAsync(
            "auth.game_id.login.success",
            new
            {
                accountId = response.Account.Id,
                response.Account.DisplayName,
                response.Account.AccountKind,
                gameId = request?.GameId,
                clientVersion = request?.ClientVersion,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        await auditLogService.LogAsync(
            "auth.game_id.login.failed",
            new
            {
                ex.Code,
                gameId = request?.GameId,
                clientVersion = request?.ClientVersion,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return AuthErrorResult(ex.Code, ex.Message, AuthStatusCode(ex));
    }
});

app.MapPost("/auth/google/desktop-token", async (
    HttpContext context,
    GoogleDesktopAuthorizationCodeExchanger googleDesktopCodeExchanger,
    AuditLogService auditLogService) =>
{
    GoogleDesktopCodeExchangeRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<GoogleDesktopCodeExchangeRequest>(
            context.Request.Body,
            jsonOptions,
            context.RequestAborted);
    }
    catch (JsonException ex)
    {
        return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
    }

    try
    {
        var response = await googleDesktopCodeExchanger.ExchangeAsync(
            request,
            context.RequestAborted);
        await auditLogService.LogAsync(
            "auth.google.desktop_code_exchange.success",
            new
            {
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        await auditLogService.LogAsync(
            "auth.google.desktop_code_exchange.failed",
            new
            {
                ex.Code,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return AuthErrorResult(ex.Code, ex.Message, AuthStatusCode(ex));
    }
});

app.MapPost("/auth/google", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    AuditLogService auditLogService) =>
{
    if (!guestAuthService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    GoogleAuthRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<GoogleAuthRequest>(
            context.Request.Body,
            jsonOptions,
            context.RequestAborted);
    }
    catch (JsonException ex)
    {
        return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
    }

    try
    {
        var response = await guestAuthService.AuthenticateGoogleAsync(
            request,
            context,
            context.RequestAborted);
        await auditLogService.LogAsync(
            "auth.google.login.success",
            new
            {
                accountId = response.Account.Id,
                response.Account.DisplayName,
                response.Account.AccountKind,
                clientVersion = request?.ClientVersion,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        await auditLogService.LogAsync(
            "auth.google.login.failed",
            new
            {
                ex.Code,
                clientVersion = request?.ClientVersion,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return AuthErrorResult(ex.Code, ex.Message, AuthStatusCode(ex));
    }
});

app.MapPost("/auth/refresh", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    AuditLogService auditLogService) =>
{
    if (!guestAuthService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    RefreshAuthRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<RefreshAuthRequest>(
            context.Request.Body,
            jsonOptions,
            context.RequestAborted);
    }
    catch (JsonException ex)
    {
        return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
    }

    try
    {
        var response = await guestAuthService.RefreshSessionAsync(
            request,
            context,
            context.RequestAborted);
        await auditLogService.LogAsync(
            "auth.session.refresh.success",
            new
            {
                accountId = response.Account.Id,
                response.Account.DisplayName,
                response.Account.AccountKind,
                clientVersion = request?.ClientVersion,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        await auditLogService.LogAsync(
            "auth.session.refresh.failed",
            new
            {
                ex.Code,
                clientVersion = request?.ClientVersion,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
});

app.MapPost("/auth/logout", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    AuditLogService auditLogService) =>
{
    if (!guestAuthService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    LogoutAuthRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<LogoutAuthRequest>(
            context.Request.Body,
            jsonOptions,
            context.RequestAborted);
    }
    catch (JsonException ex)
    {
        return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
    }

    try
    {
        var response = await guestAuthService.LogoutAsync(
            context.Request.Headers.Authorization.ToString(),
            request,
            context.RequestAborted);
        await auditLogService.LogAsync(
            "auth.session.logout",
            new
            {
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
});

app.MapGet("/me", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    RunStartService runStartService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !runStartService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var activeRunState = await runStartService.GetLatestRunStateForAccountAsync(
            account.Account.Id,
            context.RequestAborted);
        return Results.Json(new MeResponse(
            account.Account,
            account.Wallet,
            account.CollectionSummary,
            activeRunState.ActiveRun,
            activeRunState.ActiveDeck,
            activeRunState.ActiveDeckCardIds,
            activeRunState.ActiveDraftPickCardIds,
            activeRunState.ActiveDraftOfferCardIds,
            activeRunState.LatestRun,
            activeRunState.LatestDeck,
            activeRunState.LatestDeckCardIds,
            activeRunState.LatestDraftPickCardIds,
            activeRunState.LatestDraftOfferCardIds), jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
});


app.MapPost("/account/operations/resolve", async (HttpContext context, GuestAuthService auth, DbConnectionFactory db) =>
{
    if (!db.IsConfigured)
        return AuthErrorResult("db_not_configured", "Account APIs require PostgreSQL.", StatusCodes.Status503ServiceUnavailable);
    try
    {
        var account = await auth.GetCurrentAccountAsync(context.Request.Headers.Authorization.ToString(), context.RequestAborted);
        var request = await JsonSerializer.DeserializeAsync<ResolveAccountOperationRequest>(
            context.Request.Body, jsonOptions, context.RequestAborted);
        await using var connection = await db.OpenConnectionAsync(context.RequestAborted);
        return Results.Json(await AccountOperationReceipt.ResolveAsync(connection, account.Account.Id,
            request?.RequestId, context.RequestAborted), jsonOptions);
    }
    catch (JsonException) { return AuthErrorResult("invalid_json", "Invalid JSON.", StatusCodes.Status400BadRequest); }
    catch (AuthServiceException ex) { return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized); }
    catch (AccountOperationException ex) { return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status400BadRequest); }
});

app.MapPost("/wallet/purchase-ticket", async (
    HttpContext context,
    GuestAuthService guestAuthService) =>
{
    if (!guestAuthService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    PurchaseTicketRequest? request = null;
    if (context.Request.ContentLength != 0)
    {
        try
        {
            request = await JsonSerializer.DeserializeAsync<PurchaseTicketRequest>(
                context.Request.Body,
                jsonOptions,
                context.RequestAborted);
        }
        catch (JsonException ex)
        {
            return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var response = await guestAuthService.PurchaseTicketAsync(
            account,
            request,
            context.RequestAborted);
        return Results.Json(response, jsonOptions);
    }
    catch (AccountOperationException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, ex.Code == "request_id_conflict"
            ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest);
    }
    catch (AuthServiceException ex)
    {
        var statusCode = ex.Code switch
        {
            "insufficient_resource_gold" => StatusCodes.Status409Conflict,
            "invalid_ticket_count" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status401Unauthorized
        };
        return AuthErrorResult(ex.Code, ex.Message, statusCode);
    }
});

app.MapPost("/ads/rewarded-ticket/attempt", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    RewardedAdService rewardedAdService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !rewardedAdService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Rewarded ads require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    CreateRewardedAdAttemptRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<CreateRewardedAdAttemptRequest>(
            context.Request.Body,
            jsonOptions,
            context.RequestAborted);
    }
    catch (JsonException ex)
    {
        return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var response = await rewardedAdService.CreateAttemptAsync(
            account,
            request,
            context.RequestAborted);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
    catch (RewardedAdServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status400BadRequest);
    }
});

app.MapGet("/ads/rewarded-ticket/attempt/{attemptId:guid}", async (
    Guid attemptId,
    HttpContext context,
    GuestAuthService guestAuthService,
    RewardedAdService rewardedAdService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !rewardedAdService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Rewarded ads require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var response = await rewardedAdService.GetAttemptAsync(
            account,
            attemptId,
            context.RequestAborted);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
    catch (RewardedAdServiceException ex)
    {
        var statusCode = ex.Code == "attempt_not_found"
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        return AuthErrorResult(ex.Code, ex.Message, statusCode);
    }
});

app.MapGet("/ads/levelplay/rewarded-callback", async (
    HttpContext context,
    RewardedAdService rewardedAdService) =>
{
    var query = context.Request.Query;
    var request = new LevelPlayRewardCallbackRequest(
        query["eventId"].FirstOrDefault(),
        query["userId"].FirstOrDefault(),
        query["dynamicUserId"].FirstOrDefault(),
        query["rewards"].FirstOrDefault(),
        query["timestamp"].FirstOrDefault(),
        query["signature"].FirstOrDefault(),
        query["placementName"].FirstOrDefault() ?? query["placement"].FirstOrDefault(),
        query["appKey"].FirstOrDefault());

    var callbackEventId = string.IsNullOrWhiteSpace(request.EventId)
        ? "-"
        : request.EventId.Trim();
    Console.WriteLine(
        $"[rewarded-ad] callback received event={callbackEventId} " +
        $"userIdPresent={!string.IsNullOrWhiteSpace(request.UserId)} " +
        $"dynamicUserIdPresent={!string.IsNullOrWhiteSpace(request.DynamicUserId)} " +
        $"rewardsPresent={!string.IsNullOrWhiteSpace(request.Rewards)} " +
        $"timestampPresent={!string.IsNullOrWhiteSpace(request.Timestamp)} " +
        $"signaturePresent={!string.IsNullOrWhiteSpace(request.Signature)} " +
        $"placement={request.Placement?.Trim() ?? "-"} " +
        $"appKeyPresent={!string.IsNullOrWhiteSpace(request.AppKey)}");

    var callbackStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
    // Finish durable webhook processing even if LevelPlay closes its short-lived HTTP request first.
    using var callbackProcessingTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    try
    {
        var result = await rewardedAdService.ProcessLevelPlayCallbackAsync(
            request,
            callbackProcessingTimeout.Token);
        var elapsedMilliseconds = System.Diagnostics.Stopwatch
            .GetElapsedTime(callbackStartedAt)
            .TotalMilliseconds;
        Console.WriteLine(
            $"[rewarded-ad] event={result.EventId} result={result.ResultCode} account={result.AccountId?.ToString() ?? "-"} " +
            $"granted={result.RewardGranted} elapsedMs={elapsedMilliseconds:F0}");
        return Results.Text($"{result.EventId}:OK", "text/plain", Encoding.UTF8);
    }
    catch (OperationCanceledException) when (callbackProcessingTimeout.IsCancellationRequested)
    {
        Console.WriteLine(
            $"[rewarded-ad] callback timed out event={callbackEventId} processingLimitSeconds=10");
        return AuthErrorResult(
            "callback_processing_timeout",
            "Reward callback processing timed out. LevelPlay may retry this event.",
            StatusCodes.Status503ServiceUnavailable);
    }
    catch (RewardedAdServiceException ex)
    {
        Console.WriteLine(
            $"[rewarded-ad] callback rejected event={callbackEventId} code={ex.Code} message={ex.Message}");
        var statusCode = ex.Code switch
        {
            "callback_not_configured" => StatusCodes.Status503ServiceUnavailable,
            "invalid_callback_signature" => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest
        };
        return AuthErrorResult(ex.Code, ex.Message, statusCode);
    }
});

app.MapPost("/cards/upgrade", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    CardUpgradeService cardUpgradeService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !cardUpgradeService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    UpgradeCardRequest? request = null;
    if (context.Request.ContentLength != 0)
    {
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpgradeCardRequest>(
                context.Request.Body,
                jsonOptions,
                context.RequestAborted);
        }
        catch (JsonException ex)
        {
            return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var response = await cardUpgradeService.UpgradeCardAsync(
            account,
            request,
            context.RequestAborted);
        return Results.Json(response, jsonOptions);
    }
    catch (AccountOperationException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, ex.Code == "request_id_conflict"
            ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
    catch (CardUpgradeServiceException ex)
    {
        var statusCode = ex.Code switch
        {
            "card_definition_not_found" => StatusCodes.Status404NotFound,
            "insufficient_card_copies" => StatusCodes.Status409Conflict,
            "insufficient_resource_gold" => StatusCodes.Status409Conflict,
            "max_level_reached" => StatusCodes.Status409Conflict,
            "card_upgrade_conflict" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return AuthErrorResult(ex.Code, ex.Message, statusCode);
    }
});

app.MapPost("/runs/start", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    RunStartService runStartService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !runStartService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    StartRunRequest? request = null;
    if (context.Request.ContentLength is > 0)
    {
        try
        {
            request = await JsonSerializer.DeserializeAsync<StartRunRequest>(
                context.Request.Body,
                jsonOptions,
                context.RequestAborted);
        }
        catch (JsonException ex)
        {
            return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var response = await runStartService.StartDraftRunAsync(
            account,
            request,
            context.RequestAborted);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
    catch (RunServiceException ex)
    {
        var statusCode = ex.Code is "insufficient_tickets" or "active_run_exists" or "unclaimed_run_rewards"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        return AuthErrorResult(ex.Code, ex.Message, statusCode);
    }
});

app.MapPost("/runs/draft-state", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    RunStartService runStartService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !runStartService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    DraftStateRequest? request = null;
    if (context.Request.ContentLength is > 0)
    {
        try
        {
            request = await JsonSerializer.DeserializeAsync<DraftStateRequest>(
                context.Request.Body,
                jsonOptions,
                context.RequestAborted);
        }
        catch (JsonException ex)
        {
            return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var response = await runStartService.GetAuthoritativeDraftStateAsync(
            account,
            request,
            context.RequestAborted);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
    catch (RunServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/runs/select-draft-card", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    RunStartService runStartService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !runStartService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    SelectDraftCardRequest? request = null;
    if (context.Request.ContentLength is > 0)
    {
        try
        {
            request = await JsonSerializer.DeserializeAsync<SelectDraftCardRequest>(
                context.Request.Body,
                jsonOptions,
                context.RequestAborted);
        }
        catch (JsonException ex)
        {
            return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var response = await runStartService.SelectAuthoritativeDraftCardAsync(
            account,
            request,
            context.RequestAborted);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
    catch (RunServiceException ex)
    {
        var statusCode = ex.Code == "stale_draft_pick_index"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        return AuthErrorResult(ex.Code, ex.Message, statusCode);
    }
});

app.MapPost("/runs/save-draft-picks", () =>
    AuthErrorResult(
        "client_authoritative_draft_removed",
        "This endpoint is no longer supported. Use the server-authoritative draft state and selection APIs.",
        StatusCodes.Status410Gone));

app.MapPost("/runs/complete-draft", () =>
    AuthErrorResult(
        "client_authoritative_draft_removed",
        "This endpoint is no longer supported. The server completes a deck after the 33rd authoritative pick.",
        StatusCodes.Status410Gone));

// Keep an explicit tombstone for older clients; never accept client-authored run results.
app.MapPost("/runs/sync-local-record", () =>
    AuthErrorResult(
        "client_authoritative_run_result_removed",
        "Local run results are no longer accepted. Only server-resolved battles count toward run progress and rewards. Refresh /me for the server record.",
        StatusCodes.Status410Gone));

app.MapPost("/runs/claim-rewards", async (
    HttpContext context,
    GuestAuthService guestAuthService,
    RunStartService runStartService,
    AuditLogService auditLogService) =>
{
    if (!guestAuthService.IsDatabaseConfigured || !runStartService.IsDatabaseConfigured)
    {
        return AuthErrorResult(
            "db_not_configured",
            "PROJECT333_DB_CONNECTION is not set. Account APIs require PostgreSQL.",
            StatusCodes.Status503ServiceUnavailable);
    }

    ClaimRunRewardsRequest? request = null;
    if (context.Request.ContentLength is > 0)
    {
        try
        {
            request = await JsonSerializer.DeserializeAsync<ClaimRunRewardsRequest>(
                context.Request.Body,
                jsonOptions,
                context.RequestAborted);
        }
        catch (JsonException ex)
        {
            return AuthErrorResult("invalid_json", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    try
    {
        var account = await guestAuthService.GetCurrentAccountAsync(
            context.Request.Headers.Authorization.ToString(),
            context.RequestAborted);
        var response = await runStartService.ClaimRunRewardsAsync(
            account,
            request,
            context.RequestAborted);
        await auditLogService.LogAsync(
            "runs.claim_rewards.success",
            new
            {
                accountId = response.Account.Id,
                runId = response.Run.Id,
                response.Run.Wins,
                response.Run.Losses,
                resourceGoldDelta = response.Reward.ResourceGoldDelta,
                ticketDelta = response.Reward.TicketDelta,
                cardRewardKinds = response.Reward.CardRewards.Count,
                cardRewardTotal = response.Reward.CardRewards.Values.Sum(),
                packRewardKinds = response.Reward.PackRewards.Count,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return Results.Json(response, jsonOptions);
    }
    catch (AuthServiceException ex)
    {
        await auditLogService.LogAsync(
            "runs.claim_rewards.failed",
            new
            {
                ex.Code,
                requestedRunId = request?.RunId,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return AuthErrorResult(ex.Code, ex.Message, StatusCodes.Status401Unauthorized);
    }
    catch (RunServiceException ex)
    {
        var statusCode = ex.Code == "reward_already_claimed"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        await auditLogService.LogAsync(
            "runs.claim_rewards.failed",
            new
            {
                ex.Code,
                requestedRunId = request?.RunId,
                statusCode,
                remoteEndpoint = ResolveRemoteEndpoint(context),
                userAgent = ResolveUserAgent(context)
            },
            CancellationToken.None);
        return AuthErrorResult(ex.Code, ex.Message, statusCode);
    }
});

app.Map("/battle", async (
    HttpContext context,
    AuditLogService auditLogService) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket connection required.");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connection = new BattleClientConnection(Guid.NewGuid().ToString("N"), socket);
    using var connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    using var guard = new BattleConnectionGuard(battleLimits, battleAccounts);
    using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(connectionLifetime.Token);
    var authenticationTask = MonitorAuthenticationDeadlineAsync(connection, guard, battleLimits, connectionLifetime, heartbeatCancellation.Token);
    var heartbeatTask = MonitorClientHeartbeatAsync(
        connection,
        clientHeartbeatTimeout,
        clientHeartbeatPollInterval,
        heartbeatCancellation.Token);
    BattleSession? session = null;
    var connectionId = connection.ConnectionId;
    Console.WriteLine($"[{connectionId}] connected");

    try
    {
    await SendEnvelopeAsync(connection, new OnlineBattleEnvelope
    {
        MessageType = OnlineBattleMessageType.KeepAlive,
        MatchId = string.Empty,
        BattleEvents =
        {
            new BattleEventDto
            {
                EventType = BattleEventType.StateChanged,
                Message = "Connected to After333 PvP test server."
            }
        }
    }, jsonOptions, connectionLifetime.Token);

        while (connection.IsOpen && !connectionLifetime.Token.IsCancellationRequested)
        {
            var json = await BattleMessageReader.ReadTextAsync(connection, connectionLifetime.Token);
            if (json == null)
            {
                break;
            }

            if (!guard.TryAcceptMessage())
                throw new BattleMessageException(WebSocketCloseStatus.PolicyViolation, "message_rate_limited");
            connection.MarkClientMessageReceived();

            if (ShouldLogVerboseTransport())
            {
                Console.WriteLine($"[{connectionId}] received: {json}");
            }

            OnlineBattleEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<OnlineBattleEnvelope>(json, jsonOptions);
            }
            catch (JsonException ex)
            {
                await SendErrorAsync(connection, "invalid_json", ex.Message, jsonOptions, connectionLifetime.Token);
                continue;
            }

            if (envelope?.MessageType == OnlineBattleMessageType.KeepAlive)
            {
                continue;
            }

            if (connection.IsSuperseded)
            {
                break;
            }

            if (envelope?.MessageType == OnlineBattleMessageType.JoinMatch)
            {
                if (!guard.TryAcceptJoin())
                    throw new BattleMessageException(WebSocketCloseStatus.PolicyViolation, "join_rate_limited");
                Guid? pendingReservationId = null;
                connection.ReservedOnlineSeatId = OnlineBattleSeatId.None;
                try
                {
                if (!envelope.UseMatchmakingQueue && string.IsNullOrWhiteSpace(envelope.MatchId))
                {
                    await SendErrorAsync(connection, "missing_match_id", "JoinMatch requires a MatchId.", jsonOptions, connectionLifetime.Token);
                    continue;
                }

                var clientVersion = ResolveClientVersion(context);
                if (!IsClientVersionCompatible(builder.Configuration, clientVersion))
                {
                    var requiredClientVersion = ResolveRequiredClientVersion(builder.Configuration);
                    Console.WriteLine(
                        $"[online-join] rejected client version actual={FormatConfigValue(clientVersion)} required={FormatConfigValue(requiredClientVersion)}");
                    await SendErrorAsync(connection,
                        "client_version_mismatch",
                        BuildClientVersionMismatchMessage(requiredClientVersion, clientVersion),
                        jsonOptions,
                        connectionLifetime.Token);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(envelope.SessionToken))
                {
                    await SendErrorAsync(connection, "missing_session_token", "JoinMatch requires a valid login session.", jsonOptions, connectionLifetime.Token);
                    continue;
                }

                AuthenticatedAccount? authenticatedAccount = null;
                if (envelope.UseMatchmakingQueue || !string.IsNullOrWhiteSpace(envelope.SessionToken))
                {
                    if (!guestAuthService.IsDatabaseConfigured)
                    {
                        await SendErrorAsync(connection, "db_not_configured", "PVP matchmaking requires PROJECT333_DB_CONNECTION for account authentication.", jsonOptions, connectionLifetime.Token);
                        continue;
                    }

                    try
                    {
                        authenticatedAccount = await guestAuthService.GetCurrentAccountAsync(
                            ToBearerAuthorizationHeader(envelope.SessionToken),
                            connectionLifetime.Token);
                    }
                    catch (AuthServiceException ex)
                    {
                        await SendErrorAsync(connection, ex.Code, ex.Message, jsonOptions, connectionLifetime.Token);
                        continue;
                    }
                }

                if (!connection.IsOpen) break;
                var admission = guard.Authenticate(authenticatedAccount!.Account.Id);
                if (admission == BattleAuthenticationAdmission.IdentityMismatch)
                {
                    await SendErrorAsync(connection, "battle_participant_mismatch",
                        "An authenticated connection cannot change its account.", jsonOptions, connectionLifetime.Token);
                    continue;
                }
                if (admission != BattleAuthenticationAdmission.Accepted)
                    throw new BattleMessageException(WebSocketCloseStatus.PolicyViolation,
                        admission == BattleAuthenticationAdmission.ConnectionLimit ? "account_connection_limit" : "authentication_timeout");
                var accountId = authenticatedAccount?.Account.Id.ToString("D") ??
                                (envelope.AccountId ?? string.Empty).Trim();
                if (connection.HasAssignedSeat &&
                    !string.Equals(connection.AccountId, accountId, StringComparison.Ordinal))
                {
                    await SendErrorAsync(connection, "battle_participant_mismatch",
                        "An existing battle connection cannot change its authenticated account.", jsonOptions, connectionLifetime.Token);
                    continue;
                }
                if (envelope.UseMatchmakingQueue && string.IsNullOrWhiteSpace(accountId))
                {
                    await SendErrorAsync(connection, "account_required", "PVP matchmaking requires an authenticated account.", jsonOptions, connectionLifetime.Token);
                    continue;
                }

                var memoryReconnectStatus = envelope.UseMatchmakingQueue
                    ? sessionManager.FindReconnectStatus(accountId)
                    : null;
                var dbReconnectStatus = envelope.UseMatchmakingQueue && pvpMatchmakingService.IsDatabaseConfigured
                    ? await pvpMatchmakingService.GetReconnectStatusForAccountAsync(accountId, connectionLifetime.Token)
                    : null;
                var isReconnectJoin = memoryReconnectStatus != null || dbReconnectStatus != null;
                var connectedPvpSession = envelope.UseMatchmakingQueue
                    ? sessionManager.FindConnectedPvpSession(accountId)
                    : null;
                var isConnectionTakeover = connectedPvpSession != null &&
                                           envelope.IsReconnectAttempt &&
                                           connectedPvpSession.CanReplaceConnectionForReconnect(
                                               accountId,
                                               envelope.PreviousConnectionId);
                var isRejoiningCurrentBattle = session?.IsBattleStarted == true &&
                    string.Equals(session.MatchId,
                        envelope.UseMatchmakingQueue ? connectedPvpSession?.MatchId : envelope.MatchId,
                        StringComparison.Ordinal);
                var isExistingCustomBattle = !envelope.UseMatchmakingQueue &&
                    sessionManager.TryGet(envelope.MatchId ?? string.Empty, out var existingCustomBattle) &&
                    existingCustomBattle?.IsBattleStarted == true;
                var isResumingExistingBattle = isReconnectJoin || isConnectionTakeover ||
                    isRejoiningCurrentBattle || isExistingCustomBattle;
                if (connectedPvpSession != null && !isConnectionTakeover &&
                    (session == null || !string.Equals(session.MatchId, connectedPvpSession.MatchId, StringComparison.Ordinal)))
                {
                    await SendErrorAsync(connection,
                        "already_in_pvp_match",
                        $"This account is already connected to PvP match {connectedPvpSession.MatchId}. Close the existing connection or wait for reconnect grace.",
                        jsonOptions,
                        connectionLifetime.Token);
                    continue;
                }

                if (envelope.UseMatchmakingQueue &&
                    !isResumingExistingBattle &&
                    (string.IsNullOrWhiteSpace(envelope.RunId) || string.IsNullOrWhiteSpace(envelope.DeckId)))
                {
                    await SendErrorAsync(connection, "battle_deck_required", "PVP matchmaking requires a saved draft run and deck.", jsonOptions, connectionLifetime.Token);
                    continue;
                }

                IReadOnlyList<string>? joinDeckCardIds = envelope.PlayerDeckCardIds;
                if (authenticatedAccount != null &&
                    !isResumingExistingBattle &&
                    !string.IsNullOrWhiteSpace(envelope.RunId) &&
                    !string.IsNullOrWhiteSpace(envelope.DeckId))
                {
                    try
                    {
                        joinDeckCardIds = await runStartService.LoadBattleDeckCardIdsAsync(
                            authenticatedAccount,
                            envelope.RunId,
                            envelope.DeckId,
                            connectionLifetime.Token);
                    }
                    catch (RunServiceException ex)
                    {
                        await SendErrorAsync(connection, ex.Code, ex.Message, jsonOptions, connectionLifetime.Token);
                        continue;
                    }

                    Console.WriteLine($"[online-join] authorized server deck account={accountId} run={envelope.RunId} deck={envelope.DeckId} cards={joinDeckCardIds.Count}");
                }
                else if (isReconnectJoin)
                {
                    var reconnectMatchId = memoryReconnectStatus?.MatchId ?? dbReconnectStatus!.MatchId;
                    var reconnectOnlineSeatId = memoryReconnectStatus?.OnlineSeatId.ToString() ?? dbReconnectStatus!.OnlineSeatId;
                    var reconnectRemainingSeconds = memoryReconnectStatus?.RemainingSeconds ?? dbReconnectStatus!.RemainingSeconds;
                    Console.WriteLine($"[online-join] reconnect account={accountId} match={reconnectMatchId} seat={reconnectOnlineSeatId} remaining={reconnectRemainingSeconds}s");
                }
                else if (isConnectionTakeover)
                {
                    Console.WriteLine(
                        $"[online-join] live connection takeover account={accountId} match={connectedPvpSession!.MatchId} previousConnection={envelope.PreviousConnectionId}");
                }

                var playerToken = string.IsNullOrWhiteSpace(envelope.PlayerToken)
                    ? accountId
                    : envelope.PlayerToken;

                var resolvedMatchId = envelope.MatchId ?? string.Empty;
                if (envelope.UseMatchmakingQueue)
                {
                    if (isRejoiningCurrentBattle)
                    {
                        resolvedMatchId = session!.MatchId;
                    }
                    else if (isConnectionTakeover)
                    {
                        resolvedMatchId = connectedPvpSession!.MatchId;
                    }
                    else if (isReconnectJoin)
                    {
                        resolvedMatchId = memoryReconnectStatus?.MatchId ?? dbReconnectStatus!.MatchId;
                    }
                    else
                    {
                        try
                        {
                            var matchmakingAssignment = await pvpMatchmakingService.ResolveMatchForQueueAsync(
                                new PvpMatchmakingQueueRequest(
                                    accountId,
                                    envelope.RunId,
                                    envelope.DeckId,
                                    clientVersion,
                                    connection.ConnectionId),
                                connectionLifetime.Token);
                            resolvedMatchId = matchmakingAssignment.MatchId;
                            pendingReservationId = matchmakingAssignment.ReservationId;
                            connection.MatchmakingReservationId = pendingReservationId;
                            connection.ReservedOnlineSeatId = Enum.Parse<OnlineBattleSeatId>(matchmakingAssignment.Seat);
                            Console.WriteLine(
                                $"[matchmaking] account={accountId} assigned match={resolvedMatchId} created={matchmakingAssignment.CreatedMatch} matchedOpponent={matchmakingAssignment.MatchedOpponent}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[matchmaking] failed to assign match for account={accountId}: {ex.Message}");
                            await SendErrorAsync(connection,
                                "matchmaking_failed",
                                "PVP matchmaking failed. Please try again.",
                                jsonOptions,
                                connectionLifetime.Token);
                            continue;
                        }
                    }
                }

                using var joinSessionLease = envelope.UseMatchmakingQueue
                    ? sessionManager.AcquireForJoin(resolvedMatchId, useServerAiOpponent: false)
                    : null;
                BattleSession? targetSession = envelope.UseMatchmakingQueue
                    ? joinSessionLease!.Session
                    : envelope.IsReconnectAttempt
                        ? sessionManager.FindReconnectableServerAiBattle(resolvedMatchId)
                        : sessionManager.GetOrCreate(resolvedMatchId, envelope.UseServerAiOpponent);
                if (targetSession == null)
                {
                    await SendErrorAsync(connection,
                        "battle_reconnect_expired",
                        "The previous PVE battle has already ended.",
                        jsonOptions,
                        connectionLifetime.Token);
                    continue;
                }

                if (envelope.UseMatchmakingQueue &&
                    isReconnectJoin &&
                    dbReconnectStatus != null)
                {
                    if (!targetSession.IsBattleStarted)
                    {
                        var restored = await RestorePvpBattleSessionFromLatestSnapshotAsync(
                            targetSession,
                            dbReconnectStatus,
                            accountId,
                            playerToken,
                            pvpBattlePersistenceService,
                            jsonOptions,
                            connectionLifetime.Token);
                        if (!restored)
                        {
                            await SendErrorAsync(connection,
                                "battle_restore_failed",
                                "The previous PVP battle could not be restored. Please try again later.",
                                jsonOptions,
                                connectionLifetime.Token);
                            sessionManager.RemoveIfEmpty(targetSession);
                            continue;
                        }
                    }
                    else if (memoryReconnectStatus == null &&
                             !targetSession.TryRestorePendingReconnectReservation(
                                 dbReconnectStatus.RuntimePlayerId,
                                 dbReconnectStatus.OnlineSeatId,
                                 accountId,
                                 playerToken,
                                 dbReconnectStatus.ReconnectDeadlineUtc))
                    {
                        await SendErrorAsync(connection,
                            "battle_reconnect_reservation_failed",
                            "The previous PVP battle was restored, but this seat could not be reserved for reconnection.",
                            jsonOptions,
                            connectionLifetime.Token);
                        continue;
                    }
                }

                BattleClientConnection? replacedConnection = null;
                if (session == null || !string.Equals(session.MatchId, targetSession.MatchId, StringComparison.Ordinal))
                {
                    if (session != null)
                    {
                        await DisconnectBattleConnectionAsync(session, connection, sessionManager,
                            runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService,
                            disconnectReconnectGracePeriod, jsonOptions);
                        sessionManager.RemoveIfEmpty(session);
                    }

                    connection.MatchId = targetSession.MatchId;
                    connection.PlayerToken = playerToken;
                    ApplyAuthenticatedAccount(connection, authenticatedAccount, accountId);
                    if (!targetSession.IsBattleStarted)
                    {
                        CopyPlayerDeckCardIds(connection, joinDeckCardIds);
                        CopyRunDeckMetadata(connection, envelope.RunId, envelope.DeckId);
                        await RefreshConnectionCardUpgradeLevelsAsync(
                            connection,
                            battleCardUpgradeLevelService,
                            connectionLifetime.Token);
                    }
                    session = targetSession;
                    connection.AssignedSeatId = default;
                    connection.AssignedOnlineSeatId = OnlineBattleSeatId.None;
                    connection.HasAssignedSeat = false;
                    connection.MatchmakingJoinPending = envelope.UseMatchmakingQueue;

                    var reconnectIdentityKey = string.IsNullOrWhiteSpace(connection.AccountId)
                        ? connection.PlayerToken
                        : connection.AccountId;
                    var shouldReplaceConnection = envelope.IsReconnectAttempt &&
                                                  session.CanReplaceConnectionForReconnect(
                                                      reconnectIdentityKey,
                                                      envelope.PreviousConnectionId);
                    string joinError;
                    bool connectionAdded;
                    if (shouldReplaceConnection)
                    {
                        connectionAdded = session.TryReplaceConnectionForReconnect(
                            connection,
                            envelope.PreviousConnectionId,
                            out replacedConnection,
                            out joinError);
                    }
                    else
                    {
                        connectionAdded = session.TryAddConnection(connection, out joinError);
                    }

                    if (!connectionAdded)
                    {
                        await SendErrorAsync(connection, "match_full", joinError, jsonOptions, connectionLifetime.Token);
                        sessionManager.RemoveIfEmpty(session);
                        session = null;
                        continue;
                    }

                    var joinAccountId = string.IsNullOrWhiteSpace(connection.AccountId) ? "-" : connection.AccountId;
                    Console.WriteLine($"[match:{session.MatchId}] joined connection={connectionId} seat={connection.AssignedOnlineSeatId} runtimeSeat={connection.AssignedSeatId} account={joinAccountId} token={connection.PlayerToken} connections={session.ConnectionCount}");
                    Console.WriteLine($"[match:{session.MatchId}] join metadata {FormatJoinMetadata(connection)}");
                }
                else
                {
                    connection.PlayerToken = playerToken;
                    ApplyAuthenticatedAccount(connection, authenticatedAccount, accountId);
                    if (!session.IsBattleStarted)
                    {
                        CopyPlayerDeckCardIds(connection, joinDeckCardIds);
                        CopyRunDeckMetadata(connection, envelope.RunId, envelope.DeckId);
                        await RefreshConnectionCardUpgradeLevelsAsync(
                            connection,
                            battleCardUpgradeLevelService,
                            connectionLifetime.Token);
                    }
                    session.RefreshConnectionMetadata(connection);
                    Console.WriteLine($"[match:{session.MatchId}] refreshed metadata connection={connectionId} {FormatJoinMetadata(connection)}");
                }

                if (envelope.UseMatchmakingQueue)
                {
                    try
                    {
                        await RecordPvpMatchmakingJoinAsync(
                            session,
                            connection,
                            envelope,
                            pvpMatchmakingService,
                            context,
                            connectionLifetime.Token);
                        session.ConfirmMatchmakingJoin(connection);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[match:{session.MatchId}] PvP matchmaking persistence failed on join: {ex.Message}");
                        await DisconnectBattleConnectionAsync(session, connection, sessionManager,
                            runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService,
                            disconnectReconnectGracePeriod, jsonOptions);
                        sessionManager.RemoveIfEmpty(session);
                        session = null;
                        await SendErrorAsync(connection,
                            "matchmaking_persistence_failed",
                            "PVP matchmaking state could not be saved. Please try again.",
                            jsonOptions,
                            connectionLifetime.Token);
                        continue;
                    }
                }

                if (replacedConnection != null)
                {
                    Console.WriteLine(
                        $"[match:{session.MatchId}] replaced stale connection={replacedConnection.ConnectionId} with connection={connection.ConnectionId} for seat={connection.AssignedOnlineSeatId}");
                    replacedConnection.TerminateTransport();
                }

                await BroadcastEnvelopeAsync(session, session.CreateConnectedEnvelope(connection), jsonOptions, connectionLifetime.Token);

                if (session.TryStartBattleIfReady(out var battleStartMessage))
                {
                    await RecordPvpBattleStartedIfNeededAsync(
                        session,
                        pvpMatchmakingService,
                        connectionLifetime.Token);
                    await RecordPvpBattleSnapshotAsync(
                        session,
                        pvpBattlePersistenceService,
                        "battle_started",
                        jsonOptions,
                        connectionLifetime.Token);
                    Console.WriteLine($"[match:{session.MatchId}] {battleStartMessage}");
                    await BroadcastEnvelopeAsync(session, session.CreateBattleStartedEnvelope(battleStartMessage), jsonOptions, connectionLifetime.Token);
                    await BroadcastStateViewsAsync(session, jsonOptions, connectionLifetime.Token);
                    await BroadcastServerAiTurnIfNeededAsync(session, runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService, jsonOptions, connectionLifetime.Token);
                    ScheduleTurnTimeoutIfNeeded(session, sessionManager, runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService, jsonOptions);
                }
                else if (session.IsBattleStarted)
                {
                    if (connection.ReconnectedToPendingSeat)
                    {
                        await BroadcastStateViewsAsync(session, jsonOptions, connectionLifetime.Token);
                        await RecordPvpBattleSnapshotAsync(
                            session,
                            pvpBattlePersistenceService,
                            "reconnect_join",
                            jsonOptions,
                            connectionLifetime.Token);
                    }
                    else
                    {
                        await SendStateViewAsync(session, connection, jsonOptions, connectionLifetime.Token);
                    }

                    ScheduleTurnTimeoutIfNeeded(session, sessionManager, runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService, jsonOptions);
                }

                continue;
                }
                finally
                {
                    if (pendingReservationId.HasValue)
                    {
                        try { await pvpMatchmakingService.ReleaseReservationAsync(pendingReservationId.Value, connection.ConnectionId, CancellationToken.None); }
                        catch (Exception ex) { Console.WriteLine($"[{connectionId}] reservation cleanup failed: {ex.GetType().Name}"); }
                    }
                    connection.MatchmakingReservationId = null;
                    connection.ReservedOnlineSeatId = OnlineBattleSeatId.None;
                }
            }

            if (envelope?.MessageType != OnlineBattleMessageType.ClientCommand || envelope.ClientCommand == null)
            {
                await SendErrorAsync(connection, "unsupported_message", "Only JoinMatch and ClientCommand messages are supported by the test server.", jsonOptions, connectionLifetime.Token);
                continue;
            }

            var command = envelope.ClientCommand;
            if (session == null || !string.Equals(session.MatchId, command.MatchId, StringComparison.Ordinal))
            {
                await SendErrorAsync(connection, "join_required", "Join this match with a valid login session before sending battle commands.", jsonOptions, connectionLifetime.Token);
                continue;
            }

            if (!connection.HasAssignedSeat || command.ActorId != connection.AssignedSeatId)
            {
                await SendErrorAsync(connection,
                    "actor_mismatch",
                    $"Command actor {session.FormatSeatForLog(command.ActorId)} does not match assigned seat {connection.AssignedOnlineSeatId}.",
                    jsonOptions,
                    connectionLifetime.Token);
                continue;
            }

            if (command.ActorId == PlayerIdDto.AI)
            {
                if (session.UseServerAiOpponent)
                {
                    await SendErrorAsync(connection,
                        "ai_seat_server_controlled",
                        "The ServerAI seat is controlled by the server in this prototype.",
                        jsonOptions,
                        connectionLifetime.Token);
                    continue;
                }
            }

            if (!string.Equals(command.PlayerToken, connection.PlayerToken, StringComparison.Ordinal))
            {
                await SendErrorAsync(connection,
                    "player_token_mismatch",
                    "Command player token does not match this connection.",
                    jsonOptions,
                    connectionLifetime.Token);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(connection.AccountId) &&
                !string.IsNullOrWhiteSpace(command.AccountId) &&
                !string.Equals(command.AccountId, connection.AccountId, StringComparison.OrdinalIgnoreCase))
            {
                await SendErrorAsync(connection,
                    "account_mismatch",
                    "Command account id does not match this authenticated connection.",
                    jsonOptions,
                    connectionLifetime.Token);
                continue;
            }

            if (!session.IsBattleStarted)
            {
                await SendErrorAsync(connection, "battle_not_started", "Battle commands cannot resolve until all required players have joined and the battle has started.", jsonOptions, connectionLifetime.Token);
                continue;
            }

            Console.WriteLine($"[match:{session.MatchId}] command #{command.Sequence} {command.CommandType} actor={session.FormatSeatForLog(command.ActorId)} runtimeActor={command.ActorId} connection={connectionId}");
            IReadOnlyList<BattleEventDto> commandEvents;
            try
            {
                commandEvents = session.ApplyCommand(command);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                await RecordPvpBattleCommandAsync(
                    session,
                    connection,
                    command,
                    events: null,
                    accepted: false,
                    rejectionCode: "command_rejected",
                    rejectionMessage: ex.Message,
                    pvpBattlePersistenceService,
                    jsonOptions,
                    connectionLifetime.Token);
                await SendErrorAsync(connection, "command_rejected", ex.Message, jsonOptions, connectionLifetime.Token);
                continue;
            }

            await RecordPvpBattleCommandAsync(
                session,
                connection,
                command,
                commandEvents,
                accepted: true,
                rejectionCode: string.Empty,
                rejectionMessage: string.Empty,
                pvpBattlePersistenceService,
                jsonOptions,
                connectionLifetime.Token);
            await RecordPvpBattleSnapshotAsync(
                session,
                pvpBattlePersistenceService,
                "client_command",
                jsonOptions,
                connectionLifetime.Token);
            await BroadcastEnvelopeAsync(session, session.CreateAckEnvelope(connection, command, commandEvents), jsonOptions, connectionLifetime.Token);
            await RecordRunResultsIfBattleEndedAsync(session);
            await BroadcastStateViewsAsync(session, jsonOptions, connectionLifetime.Token);
            await BroadcastServerAiTurnIfNeededAsync(session, runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService, jsonOptions, connectionLifetime.Token);
            ScheduleTurnTimeoutIfNeeded(session, sessionManager, runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService, jsonOptions);
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (BattleMessageException ex)
    {
        Console.WriteLine($"[{connectionId}] rejected WebSocket input: {(int)ex.CloseStatus}");
        try { await connection.CloseTransportAsync(ex.CloseStatus, ex.Message, CancellationToken.None, waitForPeerClose: true); }
        catch (Exception closeError) when (closeError is WebSocketException or OperationCanceledException)
        {
            Console.WriteLine($"[{connectionId}] input rejection close failed: {closeError.GetType().Name}");
        }
    }
    catch (WebSocketException ex)
    {
        Console.WriteLine($"[{connectionId}] websocket error: {ex.Message}");
    }
    finally
    {
        guard.Dispose(); // Release capacity before potentially slow DB disconnect cleanup.
        heartbeatCancellation.Cancel();
        try
        {
            await Task.WhenAll(heartbeatTask, authenticationTask);
        }
        catch (OperationCanceledException)
        {
        }

        connection.TerminateTransport();
        if (session != null)
            await DisconnectBattleConnectionAsync(session, connection, sessionManager,
                runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService,
                disconnectReconnectGracePeriod, jsonOptions);
        Console.WriteLine($"[{connectionId}] disconnected");
    }
});

Console.WriteLine($"After333 PvP test server listening on {listenUrl}");
await app.RunAsync();

static async Task DisconnectBattleConnectionAsync(
    BattleSession session, BattleClientConnection connection, BattleSessionManager sessionManager,
    RunStartService runStartService, PvpMatchmakingService matchmaking,
    PvpBattlePersistenceService persistence, AuditLogService audit,
    TimeSpan gracePeriod, JsonSerializerOptions options)
{
    var detached = session.DetachConnection(connection, gracePeriod);
    if (detached == null) return; // Already cleaned up, or replaced by another socket.
    try
    {
        // Schedule before any DB/network await: neither failure can suppress forfeiture.
        if (detached.Reserved)
            _ = ResolveDisconnectForfeitAfterGraceAsync(session, sessionManager, detached.SeatId,
                detached.IdentityKey, detached.DeadlineUtc, runStartService, matchmaking, persistence, audit, options);
        await RecordPvpConnectionDisconnectedIfNeededAsync(session, connection,
            detached.Reserved ? detached.DeadlineUtc : null, matchmaking, CancellationToken.None);
        await RecordPvpBattleSnapshotAsync(session, persistence, "disconnect", options, CancellationToken.None);
        if (detached.Envelope != null && session.CanReconnectIdentityKey(detached.IdentityKey))
            await BroadcastEnvelopeAsync(session, detached.Envelope, options, CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{connection.ConnectionId}] disconnect persistence/notification failed: {ex.Message}");
    }
    finally { sessionManager.RemoveIfEmpty(session); }
}
static async Task MonitorAuthenticationDeadlineAsync(
    BattleClientConnection connection, BattleConnectionGuard guard, BattleConnectionLimitSettings settings,
    CancellationTokenSource lifetime, CancellationToken cancellationToken)
{
    try
    {
        await Task.Delay(TimeSpan.FromSeconds(settings.AuthenticationTimeoutSeconds), cancellationToken);
        if (!guard.ExpireAuthenticationIfDue()) return;
        Console.WriteLine($"[{connection.ConnectionId}] authentication deadline expired.");
        try { await connection.CloseTransportAsync(WebSocketCloseStatus.PolicyViolation, "authentication_timeout", CancellationToken.None, waitForPeerClose: true, readerActive: true); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        { Console.WriteLine($"[{connection.ConnectionId}] authentication close failed: {ex.GetType().Name}"); }
        finally { lifetime.Cancel(); }
    }
    catch (OperationCanceledException) { }
}
static async Task MonitorClientHeartbeatAsync(
    BattleClientConnection connection,
    TimeSpan heartbeatTimeout,
    TimeSpan pollInterval,
    CancellationToken cancellationToken)
{
    var safeTimeout = heartbeatTimeout <= TimeSpan.Zero
        ? TimeSpan.FromSeconds(12)
        : heartbeatTimeout;
    var safePollInterval = pollInterval <= TimeSpan.Zero
        ? TimeSpan.FromSeconds(2)
        : pollInterval;

    try
    {
        while (!cancellationToken.IsCancellationRequested && connection.IsOpen)
        {
            await Task.Delay(safePollInterval, cancellationToken);
            if (connection.IsSuperseded)
            {
                return;
            }

            if (DateTime.UtcNow - connection.LastClientMessageUtc <= safeTimeout)
            {
                continue;
            }

            Console.WriteLine(
                $"[{connection.ConnectionId}] client heartbeat timed out after {safeTimeout.TotalSeconds:0} seconds; closing stale WebSocket.");
            connection.TerminateTransport();
            return;
        }
    }
    catch (OperationCanceledException)
    {
    }
}

static async Task BroadcastStateViewsAsync(
    BattleSession session,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    var connections = session.SnapshotConnections();
    Console.WriteLine($"[match:{session.MatchId}] StateView -> {connections.Count} client(s)");
    foreach (var connection in connections)
    {
        await SendStateViewAsync(session, connection, jsonOptions, CancellationToken.None);
    }
}

static async Task BroadcastServerAiTurnIfNeededAsync(
    BattleSession session,
    RunStartService runStartService,
    PvpMatchmakingService pvpMatchmakingService,
    PvpBattlePersistenceService pvpBattlePersistenceService,
    AuditLogService auditLogService,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    var initialActionDelay = session.GetServerAiActionStartDelay();
    if (initialActionDelay > TimeSpan.Zero)
    {
        Console.WriteLine(
            $"[match:{session.MatchId}] waiting {initialActionDelay.TotalSeconds:0.00}s for the mulligan result presentation before server AI actions.");
        await Task.Delay(initialActionDelay, cancellationToken);
    }

    var pendingPlayerTurnPresentationDelay = TimeSpan.Zero;

    for (var actionIndex = 0; actionIndex < 64; actionIndex++)
    {
        IReadOnlyList<BattleEventDto> aiEvents;
        try
        {
            aiEvents = session.RunServerAiActionIfNeeded(forceEndTurn: actionIndex == 63);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            Console.WriteLine($"[match:{session.MatchId}] server AI failed: {ex.Message}");
            return;
        }

        if (aiEvents.Count == 0)
        {
            return;
        }

        Console.WriteLine($"[match:{session.MatchId}] server AI action events={FormatBattleEventSummary(aiEvents)}");
        pendingPlayerTurnPresentationDelay += EstimateServerAiPresentationDelay(aiEvents);
        var cappedPresentationDelay = pendingPlayerTurnPresentationDelay <= TimeSpan.FromSeconds(30)
            ? pendingPlayerTurnPresentationDelay
            : TimeSpan.FromSeconds(30);
        if (session.TryDelayCurrentPlayerTurnTimerForServerAiPresentation(cappedPresentationDelay))
        {
            Console.WriteLine($"[match:{session.MatchId}] delayed player turn timer by {cappedPresentationDelay.TotalSeconds:0.0}s for server AI presentation.");
            pendingPlayerTurnPresentationDelay = TimeSpan.Zero;
        }

        await BroadcastEnvelopeAsync(session, session.CreateServerAiTurnEnvelope(aiEvents), jsonOptions, cancellationToken);
        await RecordPvpBattleSnapshotAsync(
            session,
            pvpBattlePersistenceService,
            "server_ai_action",
            jsonOptions,
            cancellationToken);
        await RecordRunResultsIfBattleEndedAsync(session);
        await BroadcastStateViewsAsync(session, jsonOptions, cancellationToken);
    }

    Console.WriteLine($"[match:{session.MatchId}] server AI reached its action limit; safe end-turn was requested.");
}

static TimeSpan EstimateServerAiPresentationDelay(IReadOnlyList<BattleEventDto> battleEvents)
{
    if (battleEvents == null || battleEvents.Count == 0)
    {
        return TimeSpan.Zero;
    }

    var hasAttack = false;
    var hasSpell = false;
    var hasMove = false;
    var hasImpact = false;
    var hasRemoval = false;
    var hasCardPlayed = false;
    var hasAiAction = false;
    var hasTerminalEvent = false;

    foreach (var battleEvent in battleEvents)
    {
        if (battleEvent == null)
        {
            continue;
        }

        hasTerminalEvent |= battleEvent.EventType == BattleEventType.TurnEnded || battleEvent.EventType == BattleEventType.BattleEnded;
        hasAiAction |= battleEvent.SourceOwnerId == PlayerIdDto.AI &&
            (battleEvent.EventType == BattleEventType.AttackStarted || battleEvent.EventType == BattleEventType.OccupantMoved ||
             battleEvent.EventType == BattleEventType.CardPlayed || battleEvent.EventType == BattleEventType.SpellCast ||
             battleEvent.EventType == BattleEventType.RobotFusionResolved);
        switch (battleEvent.EventType)
        {
            case BattleEventType.AttackStarted:
                hasAttack = true;
                break;

            case BattleEventType.SpellCast:
                hasSpell = true;
                break;

            case BattleEventType.OccupantMoved:
                hasMove = true;
                break;

            case BattleEventType.DamageApplied:
            case BattleEventType.HealingApplied:
                hasImpact = true;
                break;

            case BattleEventType.OccupantRemoved:
                hasRemoval = true;
                break;

            case BattleEventType.CardPlayed:
                hasCardPlayed = true;
                break;
        }
    }

    var seconds = 0.10;
    if (hasAttack)
    {
        seconds += 1.15;
    }
    else if (hasSpell)
    {
        seconds += 0.85;
    }
    else if (hasMove)
    {
        seconds += 0.25;
    }
    else if (hasCardPlayed)
    {
        seconds += 0.20;
    }

    if (hasImpact && !hasAttack && !hasSpell)
    {
        seconds += 0.35;
    }

    if (hasRemoval)
    {
        seconds += 0.45;
    }

    var estimatedSeconds = Math.Min(seconds, 2.0);
    if (hasAiAction && !hasTerminalEvent)
    {
        estimatedSeconds += AiActionTiming.CalculatePostActionPauseSeconds(hasCardPlayed || hasSpell, estimatedSeconds);
    }

    return TimeSpan.FromSeconds(estimatedSeconds);
}

static void ScheduleTurnTimeoutIfNeeded(
    BattleSession session,
    BattleSessionManager sessionManager,
    RunStartService runStartService,
    PvpMatchmakingService pvpMatchmakingService,
    PvpBattlePersistenceService pvpBattlePersistenceService,
    AuditLogService auditLogService,
    JsonSerializerOptions jsonOptions)
{
    if (!session.TryGetCurrentTurnTimer(out var activeSeatId, out var timerVersion, out var deadlineUtc))
    {
        return;
    }

    _ = ResolveTurnTimeoutAsync(
        session,
        sessionManager,
        runStartService,
        pvpMatchmakingService,
        pvpBattlePersistenceService,
        auditLogService,
        activeSeatId,
        timerVersion,
        deadlineUtc,
        jsonOptions);
}

static async Task ResolveTurnTimeoutAsync(
    BattleSession session,
    BattleSessionManager sessionManager,
    RunStartService runStartService,
    PvpMatchmakingService pvpMatchmakingService,
    PvpBattlePersistenceService pvpBattlePersistenceService,
    AuditLogService auditLogService,
    PlayerIdDto activeSeatId,
    long timerVersion,
    DateTimeOffset deadlineUtc,
    JsonSerializerOptions jsonOptions)
{
    try
    {
        await WaitUntilTurnTimerDeadlineAsync(deadlineUtc);

        if (session.TryCreateTurnTimeoutEnvelope(activeSeatId, timerVersion, deadlineUtc, out var turnTimeoutEnvelope) &&
            turnTimeoutEnvelope != null)
        {
            var isMulliganTimeout = turnTimeoutEnvelope.BattleEvents.Any(battleEvent =>
                string.Equals(battleEvent.Message, "MulliganTimerExpired", StringComparison.Ordinal));
            var timeoutLabel = isMulliganTimeout ? "mulligan timer" : "turn timer";
            var timeoutAction = isMulliganTimeout ? "automatic keep-hand confirmation" : "automatic EndTurn";
            Console.WriteLine($"[match:{session.MatchId}] {timeoutLabel} expired. Broadcasting {timeoutAction}.");
            await auditLogService.LogAsync(
                isMulliganTimeout ? "battle.mulligan_timer.expired" : "battle.turn_timer.expired",
                new
                {
                    session.MatchId,
                    ActiveSeatId = activeSeatId.ToString(),
                    TimerVersion = timerVersion,
                    DeadlineUtc = deadlineUtc
                });
            await BroadcastEnvelopeAsync(session, turnTimeoutEnvelope, jsonOptions, CancellationToken.None);
            await RecordPvpBattleSnapshotAsync(
                session,
                pvpBattlePersistenceService,
                isMulliganTimeout ? "mulligan_timeout" : "turn_timeout",
                jsonOptions,
                CancellationToken.None);
            await RecordRunResultsIfBattleEndedAsync(session);
            await BroadcastStateViewsAsync(session, jsonOptions, CancellationToken.None);
            await BroadcastServerAiTurnIfNeededAsync(session, runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService, jsonOptions, CancellationToken.None);
            ScheduleTurnTimeoutIfNeeded(session, sessionManager, runStartService, pvpMatchmakingService, pvpBattlePersistenceService, auditLogService, jsonOptions);
        }
        else
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var hasCurrentTimer = session.TryGetCurrentTurnTimer(
                out var currentActiveSeatId,
                out var currentTimerVersion,
                out var currentDeadlineUtc);
            var isSameCurrentTimer = hasCurrentTimer &&
                                     currentActiveSeatId == activeSeatId &&
                                     currentTimerVersion == timerVersion &&
                                     currentDeadlineUtc == deadlineUtc;
            var shouldRetryEarlyTimer = isSameCurrentTimer && nowUtc < deadlineUtc;
            Console.WriteLine(
                $"[match:{session.MatchId}] turn timer skipped active={activeSeatId} version={timerVersion} deadline={deadlineUtc:O} now={nowUtc:O} currentActive={(hasCurrentTimer ? currentActiveSeatId.ToString() : "-")} currentVersion={(hasCurrentTimer ? currentTimerVersion.ToString() : "-")} currentDeadline={(hasCurrentTimer ? currentDeadlineUtc.ToString("O") : "-")} retry={shouldRetryEarlyTimer}.");
            await auditLogService.LogAsync(
                "battle.turn_timer.skipped",
                new
                {
                    session.MatchId,
                    ExpectedActiveSeatId = activeSeatId.ToString(),
                    ExpectedTimerVersion = timerVersion,
                    ExpectedDeadlineUtc = deadlineUtc,
                    HasCurrentTimer = hasCurrentTimer,
                    CurrentActiveSeatId = hasCurrentTimer ? currentActiveSeatId.ToString() : string.Empty,
                    CurrentTimerVersion = hasCurrentTimer ? currentTimerVersion : 0,
                    CurrentDeadlineUtc = hasCurrentTimer ? currentDeadlineUtc : default(DateTimeOffset),
                    NowUtc = nowUtc,
                    RetryScheduled = shouldRetryEarlyTimer
                });

            if (shouldRetryEarlyTimer)
            {
                ScheduleTurnTimeoutIfNeeded(
                    session,
                    sessionManager,
                    runStartService,
                    pvpMatchmakingService,
                    pvpBattlePersistenceService,
                    auditLogService,
                    jsonOptions);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[match:{session.MatchId}] turn timer handling failed: {ex.Message}");
    }
    finally
    {
        sessionManager.RemoveIfEmpty(session);
    }
}

static async Task WaitUntilTurnTimerDeadlineAsync(DateTimeOffset deadlineUtc)
{
    while (true)
    {
        var remaining = deadlineUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        // Task.Delay may resume a few milliseconds early or the wall clock may be adjusted.
        await Task.Delay(remaining + TimeSpan.FromMilliseconds(5));
    }
}

static async Task SendStateViewAsync(
    BattleSession session,
    BattleClientConnection connection,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    if (!connection.IsOpen)
    {
        connection.TerminateTransport();
        return;
    }

    try
    {
        await connection.SendEnvelopeAsync(() => session.CreateStateViewEnvelope(connection), jsonOptions, cancellationToken);
        if (ShouldLogVerboseTransport())
        {
            Console.WriteLine($"[{connection.ConnectionId}] sent StateView viewer={connection.AssignedOnlineSeatId} runtimeViewer={connection.AssignedSeatId}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{connection.ConnectionId}] state view send failed: {ex.Message}");
        connection.TerminateTransport();
    }
}

static async Task ResolveDisconnectForfeitAfterGraceAsync(
    BattleSession session,
    BattleSessionManager sessionManager,
    PlayerIdDto disconnectedSeatId,
    string disconnectedReconnectIdentityKey,
    DateTimeOffset reconnectDeadlineUtc,
    RunStartService runStartService,
    PvpMatchmakingService pvpMatchmakingService,
    PvpBattlePersistenceService pvpBattlePersistenceService,
    AuditLogService auditLogService,
    JsonSerializerOptions jsonOptions)
{
    try
    {
        var delay = reconnectDeadlineUtc - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        if (session.TryCreateExpiredDisconnectForfeitEnvelope(
                disconnectedSeatId,
                disconnectedReconnectIdentityKey,
                reconnectDeadlineUtc,
                out var disconnectForfeitEnvelope) &&
            disconnectForfeitEnvelope != null)
        {
            Console.WriteLine($"[match:{session.MatchId}] {disconnectedSeatId} reconnect grace expired. Broadcasting forfeit result.");
            await BroadcastEnvelopeAsync(session, disconnectForfeitEnvelope, jsonOptions, CancellationToken.None);
            await RecordPvpBattleSnapshotAsync(
                session,
                pvpBattlePersistenceService,
                "disconnect_forfeit",
                jsonOptions,
                CancellationToken.None);
            await RecordRunResultsIfBattleEndedAsync(session);
            await BroadcastStateViewsAsync(session, jsonOptions, CancellationToken.None);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[match:{session.MatchId}] disconnect grace handling failed: {ex.Message}");
    }
    finally
    {
        sessionManager.RemoveIfEmpty(session);
    }
}

static Task SendErrorAsync(
    BattleClientConnection connection,
    string code,
    string message,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    return SendEnvelopeAsync(connection, new OnlineBattleEnvelope
    {
        MessageType = OnlineBattleMessageType.Error,
        Error = new OnlineBattleErrorDto
        {
            Code = code,
            Message = message
        }
    }, jsonOptions, cancellationToken);
}

static async Task<ServerDatabaseStatus> GetDatabaseStatusAsync(
    DbConnectionFactory dbConnectionFactory,
    CancellationToken cancellationToken)
{
    if (dbConnectionFactory == null || !dbConnectionFactory.IsConfigured)
    {
        return new ServerDatabaseStatus(
            "NotConfigured",
            Configured: false,
            Reachable: false,
            AppliedMigrationCount: 0,
            LatestMigration: string.Empty,
            Message: "PROJECT333_DB_CONNECTION is not set.");
    }

    try
    {
        await using var connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                count(*)::int as migration_count,
                coalesce(max(version), '') as latest_migration
            from schema_migrations;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new ServerDatabaseStatus(
                "Ok",
                Configured: true,
                Reachable: true,
                AppliedMigrationCount: reader.GetInt32(0),
                LatestMigration: reader.GetString(1),
                Message: "PostgreSQL connection is reachable.");
        }

        return new ServerDatabaseStatus(
            "Ok",
            Configured: true,
            Reachable: true,
            AppliedMigrationCount: 0,
            LatestMigration: string.Empty,
            Message: "PostgreSQL connection is reachable, but no migration rows were returned.");
    }
    catch (Exception ex)
    {
        return new ServerDatabaseStatus(
            "Error",
            Configured: true,
            Reachable: false,
            AppliedMigrationCount: 0,
            LatestMigration: string.Empty,
            Message: ex.Message);
    }
}

static ServerCardDatabaseStatus GetCardDatabaseStatus()
{
    try
    {
        var database = PrototypeCardDefinitions.LoadCardDefinitionDatabase();
        return new ServerCardDatabaseStatus(
            "Ok",
            database.SchemaVersion,
            database.Cards?.Count ?? 0,
            "cards.json loaded and validated.");
    }
    catch (Exception ex)
    {
        return new ServerCardDatabaseStatus(
            "Error",
            SchemaVersion: 0,
            CardCount: 0,
            Message: ex.Message);
    }
}

static ServerDeploymentStatus GetDeploymentStatus(
    IConfiguration configuration,
    IHostEnvironment environment,
    string listenUrl,
    string publicServerUrl,
    ServerDatabaseStatus databaseStatus,
    ServerCardDatabaseStatus cardDatabaseStatus,
    Project333RateLimitSettings rateLimitSettings)
{
    var warnings = new List<string>();

    if (databaseStatus == null || !databaseStatus.Configured || !databaseStatus.Reachable)
    {
        warnings.Add("PROJECT333_DB_CONNECTION is missing or unreachable. Account, run, reward, and PvP matchmaking APIs require PostgreSQL.");
    }

    if (cardDatabaseStatus == null || !string.Equals(cardDatabaseStatus.Status, "Ok", StringComparison.OrdinalIgnoreCase))
    {
        warnings.Add("cards.json is not loaded successfully. Battle and reward card data may be unavailable.");
    }

    if (IsLocalServerUrl(publicServerUrl))
    {
        warnings.Add("PROJECT333_PUBLIC_SERVER_URL is missing or using localhost/127.0.0.1. Public clients need a reachable server URL.");
    }

    var requiredClientVersion = configuration["PROJECT333_REQUIRED_CLIENT_VERSION"];
    var recommendedClientVersion = configuration["PROJECT333_RECOMMENDED_CLIENT_VERSION"];
    if (environment.IsProduction() &&
        string.IsNullOrWhiteSpace(requiredClientVersion) &&
        string.IsNullOrWhiteSpace(recommendedClientVersion))
    {
        warnings.Add("No client version hint is configured. Set PROJECT333_REQUIRED_CLIENT_VERSION or PROJECT333_RECOMMENDED_CLIENT_VERSION before a public release.");
    }

    if (ShouldLogVerboseTransport())
    {
        warnings.Add("PROJECT333_VERBOSE_TRANSPORT_LOGS is enabled. Disable it for public deployment unless diagnosing transport issues.");
    }

    if (environment.IsProduction() && !rateLimitSettings.Enabled)
    {
        warnings.Add("PROJECT333_RATE_LIMIT_ENABLED is disabled. Enable request limiting before public deployment.");
    }

    var ready = warnings.Count == 0;
    var mode = IsLocalServerUrl(publicServerUrl)
        ? "LocalDevelopment"
        : IsLocalServerUrl(listenUrl)
            ? "ReverseProxy"
            : "Remote";
    return new ServerDeploymentStatus(
        ready ? "Ok" : "Check",
        mode,
        ready,
        warnings.ToArray(),
        ready ? "Server deployment settings look ready." : "Check deployment warnings before public testing.");
}

static IResult AuthErrorResult(string code, string message, int statusCode)
{
    return Results.Json(new
    {
        Error = new
        {
            Code = code,
            Message = message
        }
    }, statusCode: statusCode);
}

static int AuthStatusCode(AuthServiceException exception)
{
    return exception.Code switch
    {
        "game_id_taken" => StatusCodes.Status409Conflict,
        "google_identity_taken" => StatusCodes.Status409Conflict,
        "google_identity_already_linked" => StatusCodes.Status409Conflict,
        "invalid_game_id" => StatusCodes.Status400BadRequest,
        "invalid_password" => StatusCodes.Status400BadRequest,
        "invalid_display_name" => StatusCodes.Status400BadRequest,
        "invalid_json" => StatusCodes.Status400BadRequest,
        "invalid_credentials" => StatusCodes.Status401Unauthorized,
        "invalid_google_id_token" => StatusCodes.Status401Unauthorized,
        "missing_session_token" => StatusCodes.Status401Unauthorized,
        "invalid_session_token" => StatusCodes.Status401Unauthorized,
        "account_inactive" => StatusCodes.Status401Unauthorized,
        "google_auth_not_configured" => StatusCodes.Status503ServiceUnavailable,
        "google_desktop_exchange_not_configured" => StatusCodes.Status503ServiceUnavailable,
        "google_token_exchange_unavailable" => StatusCodes.Status503ServiceUnavailable,
        "google_token_exchange_failed" => StatusCodes.Status400BadRequest,
        "google_desktop_client_mismatch" => StatusCodes.Status400BadRequest,
        "invalid_google_desktop_code_request" => StatusCodes.Status400BadRequest,
        "google_token_verification_unavailable" => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status401Unauthorized
    };
}

static async Task RecordPvpBattleSnapshotAsync(
    BattleSession session,
    PvpBattlePersistenceService pvpBattlePersistenceService,
    string reason,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    if (session == null ||
        pvpBattlePersistenceService == null ||
        !pvpBattlePersistenceService.IsDatabaseConfigured)
    {
        return;
    }

    try
    {
        await pvpBattlePersistenceService.RecordSnapshotAsync(
            new PvpBattleSnapshotRecord(
                session.MatchId,
                reason,
                session.CreatePersistenceSnapshot(reason)),
            jsonOptions,
            cancellationToken);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[match:{session.MatchId}] PvP battle snapshot persistence failed: {ex.Message}");
    }
}

static async Task<bool> HasRestorablePvpBattleSnapshotAsync(
    string matchId,
    PvpBattlePersistenceService pvpBattlePersistenceService,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(matchId) ||
        pvpBattlePersistenceService == null ||
        !pvpBattlePersistenceService.IsDatabaseConfigured)
    {
        return false;
    }

    try
    {
        var snapshot = await pvpBattlePersistenceService.LoadLatestSnapshotAsync(
            matchId,
            jsonOptions,
            cancellationToken);
        return snapshot?.Snapshot?.DomainState != null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[match:{matchId}] PvP battle snapshot lookup failed: {ex.Message}");
        return false;
    }
}

static async Task<bool> RestorePvpBattleSessionFromLatestSnapshotAsync(
    BattleSession session,
    PvpMatchmakingReconnectStatus reconnectStatus,
    string accountId,
    string playerToken,
    PvpBattlePersistenceService pvpBattlePersistenceService,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    if (session == null ||
        reconnectStatus == null ||
        pvpBattlePersistenceService == null ||
        !pvpBattlePersistenceService.IsDatabaseConfigured)
    {
        return false;
    }

    if (session.IsBattleStarted)
    {
        return true;
    }

    try
    {
        var latestSnapshot = await pvpBattlePersistenceService.LoadLatestSnapshotAsync(
            session.MatchId,
            jsonOptions,
            cancellationToken);
        if (latestSnapshot?.Snapshot?.DomainState == null)
        {
            Console.WriteLine($"[match:{session.MatchId}] no restorable PvP battle snapshot was found.");
            return false;
        }

        if (!session.RestoreFromPersistenceSnapshot(latestSnapshot.Snapshot))
        {
            Console.WriteLine($"[match:{session.MatchId}] latest PvP battle snapshot could not be applied.");
            return false;
        }

        if (!session.TryRestorePendingReconnectReservation(
                reconnectStatus.RuntimePlayerId,
                reconnectStatus.OnlineSeatId,
                accountId,
                playerToken,
                reconnectStatus.ReconnectDeadlineUtc))
        {
            Console.WriteLine($"[match:{session.MatchId}] restored PvP battle snapshot but could not restore reconnect reservation for account={accountId}.");
            return false;
        }

        Console.WriteLine(
            $"[match:{session.MatchId}] restored PvP battle from snapshot version={latestSnapshot.SnapshotVersion} reason={latestSnapshot.Reason} for account={accountId}.");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[match:{session.MatchId}] PvP battle restore failed: {ex.Message}");
        return false;
    }
}

static async Task RecordPvpBattleCommandAsync(
    BattleSession session,
    BattleClientConnection connection,
    Project333.PvpServer.Messages.ClientBattleCommandMessage command,
    IReadOnlyList<BattleEventDto>? events,
    bool accepted,
    string rejectionCode,
    string rejectionMessage,
    PvpBattlePersistenceService pvpBattlePersistenceService,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    if (session == null ||
        connection == null ||
        command == null ||
        pvpBattlePersistenceService == null ||
        !pvpBattlePersistenceService.IsDatabaseConfigured)
    {
        return;
    }

    try
    {
        await pvpBattlePersistenceService.RecordCommandAsync(
            new PvpBattleCommandLogRecord(
                session.MatchId,
                command.Sequence,
                connection.ConnectionId,
                connection.AccountId,
                connection.AssignedOnlineSeatId.ToString(),
                command.ActorId.ToString(),
                command.CommandType.ToString(),
                command,
                events,
                accepted,
                rejectionCode,
                rejectionMessage),
            jsonOptions,
            cancellationToken);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[match:{session.MatchId}] PvP battle command persistence failed: {ex.Message}");
    }
}

static async Task RecordPvpMatchmakingJoinAsync(
    BattleSession session,
    BattleClientConnection connection,
    OnlineBattleEnvelope envelope,
    PvpMatchmakingService pvpMatchmakingService,
    HttpContext context,
    CancellationToken cancellationToken)
{
    if (session == null ||
        connection == null ||
        envelope == null ||
        pvpMatchmakingService == null ||
        !pvpMatchmakingService.IsDatabaseConfigured ||
        session.UseServerAiOpponent)
    {
        return;
    }

    await pvpMatchmakingService.RecordConnectionJoinedAsync(
        new PvpMatchmakingConnectionRecord(
            session.MatchId,
            connection.AccountId,
            connection.ConnectionId,
            connection.AssignedOnlineSeatId.ToString(),
            connection.AssignedSeatId.ToString(),
            connection.RunId,
            connection.DeckId,
            ResolveClientVersion(context),
            ResolveRemoteEndpoint(context),
            ResolveUserAgent(context),
            connection.MatchmakingReservationId),
        cancellationToken);
}

static async Task RecordPvpBattleStartedIfNeededAsync(
    BattleSession session,
    PvpMatchmakingService pvpMatchmakingService,
    CancellationToken cancellationToken)
{
    if (session == null ||
        pvpMatchmakingService == null ||
        !pvpMatchmakingService.IsDatabaseConfigured ||
        session.UseServerAiOpponent)
    {
        return;
    }

    try
    {
        await pvpMatchmakingService.RecordBattleStartedAsync(
            session.MatchId,
            cancellationToken);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[match:{session.MatchId}] PvP match start persistence failed: {ex.Message}");
    }
}

static async Task RecordPvpConnectionDisconnectedIfNeededAsync(
    BattleSession session,
    BattleClientConnection connection,
    DateTimeOffset? reconnectDeadlineUtc,
    PvpMatchmakingService pvpMatchmakingService,
    CancellationToken cancellationToken)
{
    if (session == null ||
        connection == null ||
        pvpMatchmakingService == null ||
        !pvpMatchmakingService.IsDatabaseConfigured ||
        session.UseServerAiOpponent ||
        !connection.HasAssignedSeat ||
        string.IsNullOrWhiteSpace(connection.AccountId))
    {
        return;
    }

    try
    {
        await pvpMatchmakingService.RecordConnectionDisconnectedAsync(
            new PvpMatchmakingDisconnectRecord(
                session.MatchId,
                connection.AccountId,
                connection.ConnectionId,
                connection.AssignedOnlineSeatId.ToString(),
                reconnectDeadlineUtc),
            cancellationToken);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[match:{session.MatchId}] PvP disconnect persistence failed: {ex.Message}");
    }
}

static string ResolveClientVersion(HttpContext context)
{
    return (context?.Request.Headers["X-Project333-Client-Version"].ToString() ?? string.Empty).Trim();
}

static string ResolveRequiredClientVersion(IConfiguration configuration)
{
    return (configuration["PROJECT333_REQUIRED_CLIENT_VERSION"] ?? string.Empty).Trim();
}

static bool IsClientVersionEnforced(IConfiguration configuration)
{
    return !string.IsNullOrWhiteSpace(ResolveRequiredClientVersion(configuration));
}

static bool IsClientVersionCompatible(IConfiguration configuration, string clientVersion)
{
    var requiredClientVersion = ResolveRequiredClientVersion(configuration);
    if (string.IsNullOrWhiteSpace(requiredClientVersion))
    {
        return true;
    }

    return string.Equals(
        requiredClientVersion,
        (clientVersion ?? string.Empty).Trim(),
        StringComparison.Ordinal);
}

static string BuildClientVersionMismatchMessage(string requiredClientVersion, string actualClientVersion)
{
    var actual = string.IsNullOrWhiteSpace(actualClientVersion) ? "missing" : actualClientVersion.Trim();
    return $"Client version '{actual}' is not compatible with this server. Required version: '{requiredClientVersion}'.";
}

static string FormatConfigValue(string value)
{
    return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}

static string ResolveRemoteEndpoint(HttpContext context)
{
    if (context == null)
    {
        return string.Empty;
    }

    var address = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    return string.IsNullOrWhiteSpace(address)
        ? string.Empty
        : $"{address}:{context.Connection.RemotePort}";
}

static string ResolveUserAgent(HttpContext context)
{
    return context?.Request.Headers["User-Agent"].ToString() ?? string.Empty;
}

static Task RecordRunResultsIfBattleEndedAsync(BattleSession session)
{
    return session.PersistResultAsync(CancellationToken.None);
}

static async Task BroadcastEnvelopeAsync(
    BattleSession session,
    OnlineBattleEnvelope envelope,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    var connections = session.SnapshotConnections();
    if (ShouldLogVerboseTransport() || HasNonStateChangedEvent(envelope.BattleEvents))
    {
        Console.WriteLine($"[match:{session.MatchId}] {envelope.MessageType} events={FormatBattleEventSummary(envelope.BattleEvents)} -> {connections.Count} client(s)");
    }

    foreach (var connection in connections)
    {
        if (!connection.IsOpen)
        {
            connection.TerminateTransport();
            continue;
        }

        try
        {
            await SendEnvelopeAsync(connection, envelope, jsonOptions, CancellationToken.None);
            if (ShouldLogVerboseTransport())
            {
                Console.WriteLine($"[{connection.ConnectionId}] sent {envelope.MessageType}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{connection.ConnectionId}] send failed: {ex.Message}");
            connection.TerminateTransport();
        }
    }
}

static Task SendEnvelopeAsync(
    BattleClientConnection connection,
    OnlineBattleEnvelope envelope,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    return connection.SendEnvelopeAsync(() => envelope, jsonOptions, cancellationToken);
}
static bool ShouldLogVerboseTransport()
{
    var value = Environment.GetEnvironmentVariable("PROJECT333_VERBOSE_TRANSPORT_LOGS");
    return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}

static string ResolvePublicServerUrl(string? configuredPublicServerUrl, string listenUrl)
{
    var resolvedUrl = string.IsNullOrWhiteSpace(configuredPublicServerUrl)
        ? listenUrl
        : configuredPublicServerUrl;
    return resolvedUrl.Trim().TrimEnd('/');
}

static bool IsLocalServerUrl(string? serverUrl)
{
    if (string.IsNullOrWhiteSpace(serverUrl))
    {
        return true;
    }

    return serverUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
           serverUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}

static bool IsPvpStartupRecoveryEnabled(IConfiguration configuration)
{
    var value = configuration["PROJECT333_ENABLE_PVP_STARTUP_RECOVERY"] ??
                Environment.GetEnvironmentVariable("PROJECT333_ENABLE_PVP_STARTUP_RECOVERY");
    return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
}

static bool HasNonStateChangedEvent(IReadOnlyList<BattleEventDto>? battleEvents)
{
    return battleEvents?.Any(battleEvent => battleEvent != null && battleEvent.EventType != BattleEventType.StateChanged) == true;
}

static string FormatBattleEventSummary(IReadOnlyList<BattleEventDto>? battleEvents)
{
    if (battleEvents == null || battleEvents.Count == 0)
    {
        return "none";
    }

    var eventGroups = battleEvents
        .Where(battleEvent => battleEvent != null)
        .GroupBy(battleEvent => battleEvent.EventType)
        .Select(group => group.Count() == 1 ? group.Key.ToString() : $"{group.Key}x{group.Count()}");

    var summary = string.Join(",", eventGroups);
    return string.IsNullOrWhiteSpace(summary) ? "none" : summary;
}

static void CopyPlayerDeckCardIds(BattleClientConnection connection, IReadOnlyList<string>? playerDeckCardIds)
{
    connection.PlayerDeckCardIds.Clear();
    if (playerDeckCardIds == null)
    {
        return;
    }

    foreach (var cardId in playerDeckCardIds)
    {
        if (!string.IsNullOrWhiteSpace(cardId))
        {
            connection.PlayerDeckCardIds.Add(cardId);
        }
    }
}

static void CopyRunDeckMetadata(BattleClientConnection connection, string? runId, string? deckId)
{
    connection.RunId = string.IsNullOrWhiteSpace(runId) ? string.Empty : runId.Trim();
    connection.DeckId = string.IsNullOrWhiteSpace(deckId) ? string.Empty : deckId.Trim();
}

static async Task RefreshConnectionCardUpgradeLevelsAsync(
    BattleClientConnection connection,
    BattleCardUpgradeLevelService battleCardUpgradeLevelService,
    CancellationToken cancellationToken)
{
    connection.CardUpgradeLevels.Clear();

    try
    {
        var levels = await battleCardUpgradeLevelService.LoadUpgradeLevelsAsync(
            connection.RunId,
            connection.DeckId,
            cancellationToken);
        foreach (var pair in levels)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            {
                connection.CardUpgradeLevels[pair.Key] = pair.Value;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[battle-levels] failed to load card upgrade levels for deck={connection.DeckId}: {ex.Message}");
    }
}

static string FormatJoinMetadata(BattleClientConnection connection)
{
    var runId = string.IsNullOrWhiteSpace(connection.RunId) ? "-" : connection.RunId;
    var deckId = string.IsNullOrWhiteSpace(connection.DeckId) ? "-" : connection.DeckId;
    var matchId = string.IsNullOrWhiteSpace(connection.MatchId) ? "-" : connection.MatchId;
    var token = string.IsNullOrWhiteSpace(connection.PlayerToken) ? "-" : connection.PlayerToken;
    var accountId = string.IsNullOrWhiteSpace(connection.AccountId) ? "-" : connection.AccountId;
    var displayName = string.IsNullOrWhiteSpace(connection.DisplayName) ? "-" : connection.DisplayName;
    return $"match={matchId} account={accountId} displayName={displayName} token={token} seat={connection.AssignedOnlineSeatId} runtimeSeat={connection.AssignedSeatId} run={runId} deck={deckId} deckCards={connection.PlayerDeckCardIds.Count} upgradedCards={connection.CardUpgradeLevels.Count}";
}

static string ToBearerAuthorizationHeader(string? sessionToken)
{
    if (string.IsNullOrWhiteSpace(sessionToken))
    {
        return string.Empty;
    }

    var trimmed = sessionToken.Trim();
    return trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? trimmed
        : $"Bearer {trimmed}";
}

static void ApplyAuthenticatedAccount(
    BattleClientConnection connection,
    AuthenticatedAccount? authenticatedAccount,
    string fallbackAccountId)
{
    if (connection == null)
    {
        return;
    }

    if (authenticatedAccount?.Account != null)
    {
        connection.AccountId = authenticatedAccount.Account.Id.ToString("D");
        connection.DisplayName = authenticatedAccount.Account.DisplayName ?? string.Empty;
        connection.AccountKind = authenticatedAccount.Account.AccountKind ?? string.Empty;
        return;
    }

    if (!string.IsNullOrWhiteSpace(fallbackAccountId))
    {
        connection.AccountId = fallbackAccountId.Trim();
    }
}

public sealed record ServerDatabaseStatus(
    string Status,
    bool Configured,
    bool Reachable,
    int AppliedMigrationCount,
    string LatestMigration,
    string Message);

public sealed record ServerCardDatabaseStatus(
    string Status,
    int SchemaVersion,
    int CardCount,
    string Message);

public sealed record ServerDeploymentStatus(
    string Status,
    string Mode,
    bool Ready,
    string[] Warnings,
    string Message);
