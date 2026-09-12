using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Persistence.Db;

namespace Project333.PvpServer.Ads;

public sealed class RewardedAdService
{
    public const string ProviderName = "levelplay";
    public const string DefaultPlacementName = "start_ticket_reward";
    public const int DefaultDailyLimit = 10;
    public const int DefaultCooldownSeconds = 30;
    public const int DefaultRewardTicketCount = 1;
    public const int DefaultAttemptTtlMinutes = 30;

    private readonly DbConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;

    public RewardedAdService(
        DbConnectionFactory connectionFactory,
        IConfiguration configuration)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public bool IsDatabaseConfigured => _connectionFactory.IsConfigured;
    public bool IsCallbackConfigured => !string.IsNullOrWhiteSpace(PrivateKey);
    public bool IsAppKeyConfigured => !string.IsNullOrWhiteSpace(ExpectedAppKey);
    public int DailyLimit => ResolveBoundedInt(
        "PROJECT333_REWARDED_AD_DAILY_LIMIT",
        DefaultDailyLimit,
        1,
        100);
    public int CooldownSeconds => ResolveBoundedInt(
        "PROJECT333_REWARDED_AD_COOLDOWN_SECONDS",
        DefaultCooldownSeconds,
        0,
        86400);
    public int RewardTicketCount => ResolveBoundedInt(
        "PROJECT333_REWARDED_AD_REWARD_TICKETS",
        DefaultRewardTicketCount,
        1,
        100);
    public string PlacementName => ResolveString(
        "PROJECT333_LEVELPLAY_REWARDED_PLACEMENT",
        DefaultPlacementName);

    // LevelPlay signs the exact private-key bytes. Do not trim or otherwise normalize this secret.
    private string PrivateKey => _configuration["PROJECT333_LEVELPLAY_PRIVATE_KEY"] ?? string.Empty;
    private string ExpectedAppKey => _configuration["PROJECT333_LEVELPLAY_APP_KEY"]?.Trim() ?? string.Empty;
    private int AttemptTtlMinutes => ResolveBoundedInt(
        "PROJECT333_REWARDED_AD_ATTEMPT_TTL_MINUTES",
        DefaultAttemptTtlMinutes,
        5,
        1440);

    public async Task<RewardedAdAttemptResponse> CreateAttemptAsync(
        AuthenticatedAccount account,
        CreateRewardedAdAttemptRequest? request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var providerUserId = request?.ProviderUserId?.Trim() ?? string.Empty;
        if (!IsValidProviderUserId(providerUserId))
        {
            throw new RewardedAdServiceException(
                "invalid_provider_user_id",
                "Rewarded ad provider user id must contain 1 to 64 letters or digits.");
        }

        if (!IsCallbackConfigured)
        {
            return BuildUnavailableResponse(
                account.Wallet,
                "server_not_configured",
                "Rewarded ads are not configured on the server.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var wallet = await LoadWalletForUpdateAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);

        await ExpireOldAttemptsAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);

        var dailyRewardCount = await CountTodayRewardsAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);
        if (dailyRewardCount >= DailyLimit)
        {
            await transaction.CommitAsync(cancellationToken);
            return BuildEligibilityResponse(
                wallet,
                canShow: false,
                attempt: null,
                reasonCode: "daily_limit_reached",
                message: "Daily rewarded ad limit reached.",
                remainingDailyRewards: 0,
                cooldownSeconds: 0);
        }

        var lastRewardedAt = await LoadLastRewardedAtAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);
        var cooldownRemaining = CalculateCooldownRemaining(lastRewardedAt);
        if (cooldownRemaining > 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return BuildEligibilityResponse(
                wallet,
                canShow: false,
                attempt: null,
                reasonCode: "reward_cooldown",
                message: "Rewarded ad cooldown is active.",
                remainingDailyRewards: DailyLimit - dailyRewardCount,
                cooldownSeconds: cooldownRemaining);
        }

        var existingAttempt = await LoadPendingAttemptAsync(
            connection,
            transaction,
            account.Account.Id,
            providerUserId,
            cancellationToken);
        if (existingAttempt != null)
        {
            await transaction.CommitAsync(cancellationToken);
            return BuildEligibilityResponse(
                wallet,
                canShow: true,
                attempt: existingAttempt,
                reasonCode: null,
                message: "Rewarded ad attempt is ready.",
                remainingDailyRewards: DailyLimit - dailyRewardCount,
                cooldownSeconds: 0);
        }

        var attempt = await InsertAttemptAsync(
            connection,
            transaction,
            account.Account.Id,
            providerUserId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return BuildEligibilityResponse(
            wallet,
            canShow: true,
            attempt,
            reasonCode: null,
            message: "Rewarded ad attempt created.",
            remainingDailyRewards: DailyLimit - dailyRewardCount,
            cooldownSeconds: 0);
    }

    public async Task<RewardedAdAttemptResponse> GetAttemptAsync(
        AuthenticatedAccount account,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var wallet = await LoadWalletForUpdateAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);
        var attempt = await LoadAttemptAsync(
            connection,
            transaction,
            attemptId,
            account.Account.Id,
            cancellationToken);
        if (attempt == null)
        {
            throw new RewardedAdServiceException("attempt_not_found", "Rewarded ad attempt was not found.");
        }

        if (string.Equals(attempt.Status, "pending", StringComparison.OrdinalIgnoreCase) &&
            attempt.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await MarkAttemptExpiredAsync(connection, transaction, attempt.Id, cancellationToken);
            attempt = attempt with { Status = "expired" };
        }

        var dailyRewardCount = await CountTodayRewardsAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);
        var lastRewardedAt = await LoadLastRewardedAtAsync(
            connection,
            transaction,
            account.Account.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var canShow = IsCallbackConfigured &&
                      string.Equals(attempt.Status, "pending", StringComparison.OrdinalIgnoreCase);
        var reasonCode = attempt.Status switch
        {
            "granted" => null,
            "expired" => "attempt_expired",
            "rejected" => attempt.FailureCode ?? "attempt_rejected",
            _ => null
        };
        var message = attempt.Status switch
        {
            "granted" => "Rewarded ad ticket was granted.",
            "expired" => "Rewarded ad attempt expired.",
            "rejected" => "Rewarded ad reward was rejected.",
            _ => "Reward verification is pending."
        };

        return BuildEligibilityResponse(
            wallet,
            canShow,
            attempt,
            reasonCode,
            message,
            Math.Max(0, DailyLimit - dailyRewardCount),
            CalculateCooldownRemaining(lastRewardedAt));
    }

    public async Task<RewardedAdCallbackResult> ProcessLevelPlayCallbackAsync(
        LevelPlayRewardCallbackRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsCallbackConfigured)
        {
            throw new RewardedAdServiceException(
                "callback_not_configured",
                "PROJECT333_LEVELPLAY_PRIVATE_KEY is not configured.");
        }

        var eventId = RequireValue(request.EventId, "missing_event_id", "LevelPlay event id is required.");
        var providerUserId = RequireValue(request.UserId, "missing_user_id", "LevelPlay user id is required.");
        var dynamicUserId = RequireValue(
            request.DynamicUserId,
            "missing_dynamic_user_id",
            "LevelPlay dynamic user id is required.");
        var rewardsText = RequireValue(request.Rewards, "missing_rewards", "LevelPlay reward amount is required.");
        var timestamp = RequireValue(request.Timestamp, "missing_timestamp", "LevelPlay timestamp is required.");
        var signature = RequireValue(request.Signature, "missing_signature", "LevelPlay signature is required.");

        if (!int.TryParse(rewardsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rewardAmount) ||
            rewardAmount != RewardTicketCount)
        {
            throw new RewardedAdServiceException(
                "invalid_reward_amount",
                $"LevelPlay reward amount must be {RewardTicketCount}.");
        }

        if (!VerifySignature(timestamp, eventId, providerUserId, rewardsText, signature))
        {
            throw new RewardedAdServiceException("invalid_callback_signature", "LevelPlay signature is invalid.");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var attempt = await LoadAttemptByTokenForUpdateAsync(
            connection,
            transaction,
            dynamicUserId,
            cancellationToken);
        if (attempt == null ||
            !string.Equals(attempt.ProviderUserId, providerUserId, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new RewardedAdCallbackResult(
                eventId,
                RewardGranted: false,
                AccountId: null,
                TicketCount: 0,
                ResultCode: "attempt_not_found");
        }

        if (!string.IsNullOrWhiteSpace(request.Placement) &&
            !string.Equals(request.Placement.Trim(), attempt.Placement, StringComparison.Ordinal))
        {
            await MarkAttemptRejectedAsync(
                connection,
                transaction,
                attempt.Id,
                "placement_mismatch",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RewardedAdCallbackResult(
                eventId,
                RewardGranted: false,
                attempt.AccountId,
                TicketCount: 0,
                ResultCode: "placement_mismatch");
        }

        if (!string.IsNullOrWhiteSpace(ExpectedAppKey) &&
            (string.IsNullOrWhiteSpace(request.AppKey) ||
             !string.Equals(ExpectedAppKey, request.AppKey.Trim(), StringComparison.Ordinal)))
        {
            await MarkAttemptRejectedAsync(
                connection,
                transaction,
                attempt.Id,
                "app_key_mismatch",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RewardedAdCallbackResult(
                eventId,
                RewardGranted: false,
                attempt.AccountId,
                TicketCount: 0,
                ResultCode: "app_key_mismatch");
        }

        if (string.Equals(attempt.Status, "granted", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            return new RewardedAdCallbackResult(
                eventId,
                RewardGranted: false,
                attempt.AccountId,
                TicketCount: attempt.RewardTicketCount,
                ResultCode: "already_granted");
        }

        if (string.Equals(attempt.Status, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            return new RewardedAdCallbackResult(
                eventId,
                RewardGranted: false,
                attempt.AccountId,
                TicketCount: 0,
                ResultCode: attempt.FailureCode ?? "attempt_rejected");
        }

        var insertedEventId = await TryInsertEventAsync(
            connection,
            transaction,
            eventId,
            attempt,
            providerUserId,
            rewardAmount,
            request.Placement,
            request.AppKey,
            cancellationToken);
        if (insertedEventId == null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RewardedAdCallbackResult(
                eventId,
                RewardGranted: false,
                attempt.AccountId,
                TicketCount: 0,
                ResultCode: "duplicate_event");
        }

        var wallet = await LoadWalletForUpdateAsync(
            connection,
            transaction,
            attempt.AccountId,
            cancellationToken);
        var dailyRewardCount = await CountTodayRewardsAsync(
            connection,
            transaction,
            attempt.AccountId,
            cancellationToken);
        if (dailyRewardCount >= DailyLimit)
        {
            await MarkAttemptRejectedAsync(
                connection,
                transaction,
                attempt.Id,
                "daily_limit_reached",
                cancellationToken);
            await UpdateEventResultAsync(
                connection,
                transaction,
                insertedEventId.Value,
                "daily_limit_reached",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RewardedAdCallbackResult(
                eventId,
                RewardGranted: false,
                attempt.AccountId,
                TicketCount: 0,
                ResultCode: "daily_limit_reached");
        }

        wallet = await GrantTicketAsync(
            connection,
            transaction,
            attempt.AccountId,
            RewardTicketCount,
            cancellationToken);
        await MarkAttemptGrantedAsync(
            connection,
            transaction,
            attempt.Id,
            cancellationToken);
        await RejectOtherPendingAttemptsAsync(
            connection,
            transaction,
            attempt.AccountId,
            attempt.Id,
            cancellationToken);
        await InsertRewardTransactionAsync(
            connection,
            transaction,
            attempt.AccountId,
            attempt.Id,
            RewardTicketCount,
            cancellationToken);
        await UpdateEventResultAsync(
            connection,
            transaction,
            insertedEventId.Value,
            "granted",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RewardedAdCallbackResult(
            eventId,
            RewardGranted: true,
            attempt.AccountId,
            wallet.Tickets,
            ResultCode: "granted");
    }

    private RewardedAdAttemptResponse BuildUnavailableResponse(
        WalletDto wallet,
        string reasonCode,
        string message)
    {
        return BuildEligibilityResponse(
            wallet,
            canShow: false,
            attempt: null,
            reasonCode,
            message,
            DailyLimit,
            cooldownSeconds: 0);
    }

    private RewardedAdAttemptResponse BuildEligibilityResponse(
        WalletDto wallet,
        bool canShow,
        AttemptRow? attempt,
        string? reasonCode,
        string message,
        int remainingDailyRewards,
        int cooldownSeconds)
    {
        return new RewardedAdAttemptResponse(
            canShow,
            attempt?.Id,
            attempt?.OpaqueUserToken,
            ProviderName,
            PlacementName,
            attempt?.Status ?? "unavailable",
            reasonCode,
            message,
            RewardTicketCount,
            Math.Max(0, remainingDailyRewards),
            Math.Max(0, cooldownSeconds),
            attempt?.ExpiresAt.ToString("O"),
            wallet);
    }

    private async Task<AttemptRow> InsertAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string providerUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into rewarded_ad_attempts(
                account_id,
                provider,
                placement,
                provider_user_id,
                opaque_user_token,
                reward_ticket_count,
                expires_at)
            values (
                @accountId,
                @provider,
                @placement,
                @providerUserId,
                @opaqueUserToken,
                @rewardTicketCount,
                now() + (@attemptTtlMinutes * interval '1 minute'))
            returning id, account_id, provider, placement, provider_user_id,
                      opaque_user_token, reward_ticket_count, attempt_status,
                      failure_code, expires_at, rewarded_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("provider", ProviderName);
        command.Parameters.AddWithValue("placement", PlacementName);
        command.Parameters.AddWithValue("providerUserId", providerUserId);
        command.Parameters.AddWithValue("opaqueUserToken", CreateOpaqueUserToken());
        command.Parameters.AddWithValue("rewardTicketCount", RewardTicketCount);
        command.Parameters.AddWithValue("attemptTtlMinutes", AttemptTtlMinutes);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Failed to create rewarded ad attempt.");
        }

        return ReadAttempt(reader);
    }

    private static async Task<AttemptRow?> LoadPendingAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string providerUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select id, account_id, provider, placement, provider_user_id,
                   opaque_user_token, reward_ticket_count, attempt_status,
                   failure_code, expires_at, rewarded_at
            from rewarded_ad_attempts
            where account_id = @accountId
              and provider_user_id = @providerUserId
              and attempt_status = 'pending'
              and expires_at > now()
            order by created_at desc
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("providerUserId", providerUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAttempt(reader) : null;
    }

    private static async Task<AttemptRow?> LoadAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select id, account_id, provider, placement, provider_user_id,
                   opaque_user_token, reward_ticket_count, attempt_status,
                   failure_code, expires_at, rewarded_at
            from rewarded_ad_attempts
            where id = @attemptId
              and account_id = @accountId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("attemptId", attemptId);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAttempt(reader) : null;
    }

    private static async Task<AttemptRow?> LoadAttemptByTokenForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string opaqueUserToken,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select id, account_id, provider, placement, provider_user_id,
                   opaque_user_token, reward_ticket_count, attempt_status,
                   failure_code, expires_at, rewarded_at
            from rewarded_ad_attempts
            where opaque_user_token = @opaqueUserToken
            for update;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("opaqueUserToken", opaqueUserToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAttempt(reader) : null;
    }

    private static AttemptRow ReadAttempt(NpgsqlDataReader reader)
    {
        return new AttemptRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10));
    }

    private static async Task<WalletDto> LoadWalletForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select resource_gold, tickets
            from user_wallets
            where account_id = @accountId
            for update;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Wallet was not found for account '{accountId}'.");
        }

        return new WalletDto(reader.GetInt64(0), reader.GetInt32(1));
    }

    private static async Task<int> CountTodayRewardsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select count(*)::integer
            from rewarded_ad_attempts
            where account_id = @accountId
              and attempt_status = 'granted'
              and rewarded_at >= (
                  date_trunc('day', now() at time zone 'UTC') at time zone 'UTC');
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int count ? count : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<DateTimeOffset?> LoadLastRewardedAtAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select max(rewarded_at)
            from rewarded_ad_attempts
            where account_id = @accountId
              and attempt_status = 'granted';
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            null => null,
            DBNull => null,
            DateTimeOffset value => value,
            DateTime value => new DateTimeOffset(
                DateTime.SpecifyKind(value, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException(
                $"Unexpected rewarded_at value type '{result.GetType().FullName}'.")
        };
    }

    private static async Task ExpireOldAttemptsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update rewarded_ad_attempts
            set attempt_status = 'expired',
                failure_code = 'attempt_expired',
                updated_at = now()
            where account_id = @accountId
              and attempt_status = 'pending'
              and expires_at <= now();
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkAttemptExpiredAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update rewarded_ad_attempts
            set attempt_status = 'expired',
                failure_code = 'attempt_expired',
                updated_at = now()
            where id = @attemptId
              and attempt_status = 'pending';
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("attemptId", attemptId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkAttemptRejectedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        string failureCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update rewarded_ad_attempts
            set attempt_status = 'rejected',
                failure_code = @failureCode,
                updated_at = now()
            where id = @attemptId;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("attemptId", attemptId);
        command.Parameters.AddWithValue("failureCode", failureCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkAttemptGrantedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update rewarded_ad_attempts
            set attempt_status = 'granted',
                failure_code = null,
                rewarded_at = now(),
                updated_at = now()
            where id = @attemptId;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("attemptId", attemptId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RejectOtherPendingAttemptsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid grantedAttemptId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update rewarded_ad_attempts
            set attempt_status = 'rejected',
                failure_code = 'superseded_after_reward',
                updated_at = now()
            where account_id = @accountId
              and id <> @grantedAttemptId
              and attempt_status = 'pending';
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("grantedAttemptId", grantedAttemptId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid?> TryInsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string eventId,
        AttemptRow attempt,
        string providerUserId,
        int rewardAmount,
        string? placement,
        string? appKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into rewarded_ad_events(
                provider,
                provider_event_id,
                attempt_id,
                account_id,
                provider_user_id,
                reward_amount,
                placement,
                app_key,
                signature_verified,
                processing_result)
            values (
                @provider,
                @providerEventId,
                @attemptId,
                @accountId,
                @providerUserId,
                @rewardAmount,
                @placement,
                @appKey,
                true,
                'processing')
            on conflict (provider, provider_event_id) do nothing
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("provider", ProviderName);
        command.Parameters.AddWithValue("providerEventId", eventId);
        command.Parameters.AddWithValue("attemptId", attempt.Id);
        command.Parameters.AddWithValue("accountId", attempt.AccountId);
        command.Parameters.AddWithValue("providerUserId", providerUserId);
        command.Parameters.AddWithValue("rewardAmount", rewardAmount);
        command.Parameters.AddWithValue("placement", placement?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("appKey", appKey?.Trim() ?? string.Empty);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : null;
    }

    private static async Task<WalletDto> GrantTicketAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        int ticketCount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update user_wallets
            set tickets = tickets + @ticketCount,
                updated_at = now()
            where account_id = @accountId
            returning resource_gold, tickets;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("ticketCount", ticketCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Wallet was not found for account '{accountId}'.");
        }

        return new WalletDto(reader.GetInt64(0), reader.GetInt32(1));
    }

    private static async Task InsertRewardTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid attemptId,
        int ticketCount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into account_transactions(
                account_id,
                transaction_type,
                ticket_delta,
                source_table,
                source_id)
            values (
                @accountId,
                'ad_reward',
                @ticketCount,
                'rewarded_ad_attempts',
                @attemptId);
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("ticketCount", ticketCount);
        command.Parameters.AddWithValue("attemptId", attemptId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateEventResultAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid eventRowId,
        string result,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update rewarded_ad_events
            set processing_result = @result,
                processed_at = now()
            where id = @eventRowId;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("eventRowId", eventRowId);
        command.Parameters.AddWithValue("result", result);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private bool VerifySignature(
        string timestamp,
        string eventId,
        string providerUserId,
        string rewards,
        string signature)
    {
        var payload = string.Concat(timestamp, eventId, providerUserId, rewards, PrivateKey);
        var digest = MD5.HashData(Encoding.UTF8.GetBytes(payload));
        var expected = Convert.ToHexString(digest).ToLowerInvariant();
        var actual = signature.Trim().ToLowerInvariant();
        if (actual.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(actual));
    }

    private int CalculateCooldownRemaining(DateTimeOffset? lastRewardedAt)
    {
        if (!lastRewardedAt.HasValue || CooldownSeconds <= 0)
        {
            return 0;
        }

        var remaining = lastRewardedAt.Value.AddSeconds(CooldownSeconds) - DateTimeOffset.UtcNow;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
    }

    private int ResolveBoundedInt(string key, int fallback, int minimum, int maximum)
    {
        return int.TryParse(
                   _configuration[key],
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }

    private string ResolveString(string key, string fallback)
    {
        var value = _configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string RequireValue(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RewardedAdServiceException(code, message);
        }

        return value.Trim();
    }

    private static bool IsValidProviderUserId(string value)
    {
        if (value.Length is < 1 or > 64)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string CreateOpaqueUserToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }

    private sealed record AttemptRow(
        Guid Id,
        Guid AccountId,
        string Provider,
        string Placement,
        string ProviderUserId,
        string OpaqueUserToken,
        int RewardTicketCount,
        string Status,
        string? FailureCode,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RewardedAt);
}
