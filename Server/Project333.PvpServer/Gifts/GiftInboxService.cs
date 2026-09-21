using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Project333.PvpServer.Auth;
using Project333.PvpServer.Persistence.Db;

namespace Project333.PvpServer.Gifts;

/// <summary>
/// Dispatch is available only to the local administrator tool. Public routes expose
/// account-scoped listing and claiming, never arbitrary wallet adjustments.
/// </summary>
public sealed class GiftInboxService
{
    public const int PageSize = 50;
    private readonly DbConnectionFactory _connectionFactory;

    public GiftInboxService(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<GiftAccountDto>> FindAccountsAsync(
        string? search, int limit, CancellationToken cancellationToken)
    {
        search = (search ?? string.Empty).Trim();
        if (search.Length > 100 || search.Any(char.IsControl))
            throw new GiftInboxException("gift_search_invalid", "Account search is invalid.");
        limit = Math.Clamp(limit, 1, 100);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        const string sql = """
            select id, display_name, account_status, is_banned
            from accounts
            where account_status <> 'deleted'
              and (@search = '' or strpos(lower(display_name), lower(@search)) > 0
                   or strpos(id::text, lower(@search)) > 0)
            order by display_name, id limit @limit;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("search", search);
        command.Parameters.AddWithValue("limit", limit);
        return await ReadAccountsAsync(command, cancellationToken);
    }

    public async Task<GiftPreviewResult> PreviewAsync(GiftSendRequest request, CancellationToken cancellationToken)
    {
        request = Normalize(request);
        var hash = Hash(request);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var existing = await LoadDispatchAsync(connection, transaction, request.RequestId, hash, cancellationToken);
        if (existing != null)
        {
            var previousAccounts = await LoadRecipientPreviewAsync(connection, transaction, request.RequestId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new GiftPreviewResult(request.RequestId, existing.RecipientCount, true, previousAccounts,
                existing.RecipientCount > previousAccounts.Count);
        }

        var now = await ReadClockAsync(connection, transaction, cancellationToken);
        EnsureUnexpired(request, now);
        const string countSql = """
            select count(*) from accounts
            where account_status <> 'deleted' and (@allAccounts or id = any(@accountIds));
            """;
        await using var countCommand = new NpgsqlCommand(countSql, connection, transaction);
        AddTargets(countCommand, request);
        var count = (long)(await countCommand.ExecuteScalarAsync(cancellationToken))!;
        EnsureAllTargetsExist(request, count);
        const string accountsSql = """
            select id, display_name, account_status, is_banned from accounts
            where account_status <> 'deleted' and (@allAccounts or id = any(@accountIds))
            order by display_name, id limit 100;
            """;
        await using var accountsCommand = new NpgsqlCommand(accountsSql, connection, transaction);
        AddTargets(accountsCommand, request);
        var accounts = await ReadAccountsAsync(accountsCommand, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GiftPreviewResult(request.RequestId, count, false, accounts, count > accounts.Count);
    }

    public async Task<GiftSendResult> SendAsync(GiftSendRequest request, CancellationToken cancellationToken)
    {
        request = Normalize(request);
        var hash = Hash(request);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // ON CONFLICT waits for a concurrent dispatch with this ID. A later statement
        // sees its committed snapshot; no recipient is appended on a replay.
        const string campaignSql = """
            with dispatch_clock as (select clock_timestamp() as at_utc)
            insert into gift_campaigns(
                id, request_hash, title, message, tickets, resource_gold,
                all_existing_accounts, target_account_ids, admin_reason, created_at, expires_at)
            select @id, @hash, @title, @message, @tickets, @gold,
                @allAccounts, @accountIds, @reason, at_utc, @expires
            from dispatch_clock where @expires is null or @expires > at_utc
            on conflict (id) do nothing
            returning created_at;
            """;
        DateTimeOffset? createdAt;
        await using (var command = new NpgsqlCommand(campaignSql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", request.RequestId);
            command.Parameters.AddWithValue("hash", hash);
            command.Parameters.AddWithValue("title", request.Title);
            command.Parameters.AddWithValue("message", request.Message);
            command.Parameters.AddWithValue("tickets", request.Tickets);
            command.Parameters.AddWithValue("gold", request.ResourceGold);
            AddTargets(command, request);
            command.Parameters.AddWithValue("reason", request.Reason);
            command.Parameters.AddWithValue("expires", NpgsqlDbType.TimestampTz,
                request.ExpiresAtUtc.HasValue ? request.ExpiresAtUtc.Value : DBNull.Value);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            createdAt = value is DateTime date ? new DateTimeOffset(date) : null;
        }
        if (!createdAt.HasValue)
        {
            var existing = await LoadDispatchAsync(connection, transaction, request.RequestId, hash, cancellationToken);
            if (existing == null)
                throw new GiftInboxException("gift_expiry_invalid", "The expiry must be in the future.");
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        // The recipient set is materialized by this one statement. Accounts created
        // after it are never retroactively added to an all-account dispatch.
        const string recipientSql = """
            with delivered as (
                insert into gift_inbox(gift_id, account_id, delivered_at)
                select @id, id, @created from accounts
                where account_status <> 'deleted' and (@allAccounts or id = any(@accountIds))
                returning account_id)
            select count(*) from delivered;
            """;
        long recipientCount;
        await using (var command = new NpgsqlCommand(recipientSql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", request.RequestId);
            command.Parameters.AddWithValue("created", createdAt.Value);
            AddTargets(command, request);
            recipientCount = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        }
        EnsureAllTargetsExist(request, recipientCount);
        await using (var command = new NpgsqlCommand(
            "update gift_campaigns set recipient_count = @count where id = @id;", connection, transaction))
        {
            command.Parameters.AddWithValue("count", recipientCount);
            command.Parameters.AddWithValue("id", request.RequestId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new GiftSendResult(request.RequestId, false, recipientCount, createdAt.Value, request.ExpiresAtUtc);
    }

    public async Task<GiftInboxResponse> ListAsync(Guid accountId, string? cursor, CancellationToken cancellationToken)
    {
        var position = ParseCursor(cursor);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var now = await ReadClockAsync(connection, transaction, cancellationToken);
        const string countSql = """
            select count(*) from gift_inbox i join gift_campaigns c on c.id = i.gift_id
            where i.account_id = @accountId and i.claimed_at is null
              and (c.expires_at is null or c.expires_at > @now);
            """;
        long unclaimedCount;
        await using (var command = new NpgsqlCommand(countSql, connection, transaction))
        {
            command.Parameters.AddWithValue("accountId", accountId);
            command.Parameters.AddWithValue("now", now);
            unclaimedCount = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        }
        const string sql = """
            select c.id, c.title, c.message, c.tickets, c.resource_gold,
                   i.delivered_at, c.expires_at, i.claimed_at
            from gift_inbox i join gift_campaigns c on c.id = i.gift_id
            where i.account_id = @accountId
              and (not @hasCursor or (i.delivered_at, i.gift_id) < (@cursorDate, @cursorId))
            order by i.delivered_at desc, i.gift_id desc limit 51;
            """;
        var gifts = new List<GiftInboxItemDto>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("accountId", accountId);
            command.Parameters.AddWithValue("hasCursor", position.HasValue);
            command.Parameters.AddWithValue("cursorDate", position?.Date ?? DateTimeOffset.UnixEpoch);
            command.Parameters.AddWithValue("cursorId", position?.GiftId ?? Guid.Empty);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var expires = ReadNullableDate(reader, 6);
                var claimed = ReadNullableDate(reader, 7);
                gifts.Add(new GiftInboxItemDto(reader.GetGuid(0).ToString("D"), reader.GetString(1), reader.GetString(2),
                    reader.GetInt32(3), reader.GetInt64(4), ReadDate(reader, 5), expires, claimed,
                    claimed.HasValue ? "claimed" : expires.HasValue && expires.Value <= now ? "expired" : "unclaimed"));
            }
        }
        string? nextCursor = null;
        if (gifts.Count > PageSize)
        {
            gifts.RemoveAt(PageSize);
            var last = gifts[^1];
            nextCursor = EncodeCursor(last.CreatedAtUtc, Guid.Parse(last.GiftId));
        }
        await transaction.CommitAsync(cancellationToken);
        return new GiftInboxResponse(gifts, unclaimedCount, nextCursor);
    }

    public async Task<GiftClaimResponse> ClaimAsync(Guid accountId, Guid giftId, CancellationToken cancellationToken)
    {
        if (giftId == Guid.Empty)
            throw new GiftInboxException("gift_not_found", "Gift was not found.", 404);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // All economy changes contend on the wallet first; then lock this receipt.
        // Two claims, two devices, or a replay can only commit one credit.
        WalletDto wallet;
        await using (var command = new NpgsqlCommand(
            "select resource_gold, tickets from user_wallets where account_id = @accountId for update;", connection, transaction))
        {
            command.Parameters.AddWithValue("accountId", accountId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new GiftInboxException("gift_wallet_unavailable", "The account wallet is unavailable.", 409);
            wallet = new WalletDto(reader.GetInt64(0), reader.GetInt32(1));
        }
        int tickets;
        long gold;
        DateTimeOffset? claimedAt;
        DateTimeOffset? expiresAt;
        const string giftSql = """
            select c.tickets, c.resource_gold, c.expires_at, i.claimed_at
            from gift_inbox i join gift_campaigns c on c.id = i.gift_id
            where i.account_id = @accountId and i.gift_id = @giftId
            for update of i;
            """;
        await using (var command = new NpgsqlCommand(giftSql, connection, transaction))
        {
            command.Parameters.AddWithValue("accountId", accountId);
            command.Parameters.AddWithValue("giftId", giftId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new GiftInboxException("gift_not_found", "Gift was not found.", 404);
            tickets = reader.GetInt32(0);
            gold = reader.GetInt64(1);
            expiresAt = ReadNullableDate(reader, 2);
            claimedAt = ReadNullableDate(reader, 3);
        }
        if (claimedAt.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return new GiftClaimResponse(giftId.ToString("D"), true, wallet);
        }
        // One server/database clock reading, after lock waits, controls both expiry
        // validation and the resulting receipt/ledger/wallet timestamps.
        var now = await ReadClockAsync(connection, transaction, cancellationToken);
        if (expiresAt.HasValue && expiresAt.Value <= now)
            throw new GiftInboxException("gift_expired", "The gift has expired.", 409);
        WalletDto newWallet;
        try
        {
            newWallet = new WalletDto(checked(wallet.ResourceGold + gold), checked(wallet.Tickets + tickets));
        }
        catch (OverflowException)
        {
            throw new GiftInboxException("gift_wallet_limit", "The wallet cannot hold the gift items.", 409);
        }
        const string creditSql = """
            update user_wallets set resource_gold = @gold, tickets = @tickets, updated_at = @now
            where account_id = @accountId;
            update gift_inbox set claimed_at = @now where gift_id = @giftId and account_id = @accountId;
            insert into account_transactions(account_id, transaction_type, resource_gold_delta, ticket_delta,
                source_table, source_id, created_at)
            values (@accountId, 'admin_adjustment', @goldDelta, @ticketDelta, 'gift_inbox', @giftId, @now);
            """;
        await using (var command = new NpgsqlCommand(creditSql, connection, transaction))
        {
            command.Parameters.AddWithValue("gold", newWallet.ResourceGold);
            command.Parameters.AddWithValue("tickets", newWallet.Tickets);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("accountId", accountId);
            command.Parameters.AddWithValue("giftId", giftId);
            command.Parameters.AddWithValue("goldDelta", gold);
            command.Parameters.AddWithValue("ticketDelta", tickets);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new GiftClaimResponse(giftId.ToString("D"), false, newWallet);
    }

    private static GiftSendRequest Normalize(GiftSendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty)
            throw new GiftInboxException("gift_request_invalid", "A nonempty dispatch request ID is required.");
        var title = ValidateText(request.Title, 80, "title", allowNewlines: false, required: true);
        var message = ValidateText(request.Message, 1000, "message", allowNewlines: true, required: false);
        var reason = ValidateText(request.Reason, 300, "reason", allowNewlines: true, required: true);
        if (request.Tickets < 0 || request.ResourceGold < 0 || request.Tickets == 0 && request.ResourceGold == 0)
            throw new GiftInboxException("gift_items_invalid", "A gift must contain a positive number of tickets or Resource Gold.");
        var targets = (request.AccountIds ?? Array.Empty<Guid>()).OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
        if (targets.Length > 1000 || targets.Any(id => id == Guid.Empty) || targets.Distinct().Count() != targets.Length
            || request.AllExistingAccounts && targets.Length != 0 || !request.AllExistingAccounts && targets.Length == 0)
            throw new GiftInboxException("gift_targets_invalid", "Choose all accounts or between 1 and 1000 distinct account IDs.");
        DateTimeOffset? expires = request.ExpiresAtUtc?.ToUniversalTime();
        if (expires.HasValue)
        {
            // PostgreSQL timestamptz stores microseconds. Canonicalize before hashing.
            expires = new DateTimeOffset(expires.Value.Ticks - expires.Value.Ticks % 10, TimeSpan.Zero);
        }
        return request with { Title = title, Message = message, Reason = reason, AccountIds = targets, ExpiresAtUtc = expires };
    }

    private static string ValidateText(string? value, int maxLength, string field, bool allowNewlines, bool required)
    {
        value = (value ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (value.Length > maxLength || required && value.Length == 0
            || value.Any(c => c == '<' || c == '>' || char.IsControl(c) && !(allowNewlines && (c == '\n' || c == '\t')))
            || !IsWellFormedText(value))
            throw new GiftInboxException("gift_text_invalid", $"Gift {field} must be plain text within {maxLength} characters.");
        return value;
    }

    private static bool IsWellFormedText(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i])) return false;
            }
            else if (char.IsLowSurrogate(value[i]) || char.GetUnicodeCategory(value[i]) == UnicodeCategory.Format)
                return false;
        }
        return true;
    }

    private static string Hash(GiftSendRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    private static void AddTargets(NpgsqlCommand command, GiftSendRequest request)
    {
        command.Parameters.AddWithValue("allAccounts", request.AllExistingAccounts);
        command.Parameters.AddWithValue("accountIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid, request.AccountIds!.ToArray());
    }

    private static void EnsureAllTargetsExist(GiftSendRequest request, long count)
    {
        if (!request.AllExistingAccounts && count != request.AccountIds!.Count)
            throw new GiftInboxException("gift_account_not_found", "One or more target accounts are missing or deleted.", 404);
    }

    private static void EnsureUnexpired(GiftSendRequest request, DateTimeOffset now)
    {
        if (request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc.Value <= now)
            throw new GiftInboxException("gift_expiry_invalid", "The expiry must be in the future.");
    }

    private static async Task<GiftSendResult?> LoadDispatchAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid id, string expectedHash, CancellationToken cancellationToken)
    {
        const string sql = """
            select request_hash, recipient_count, created_at, expires_at from gift_campaigns where id = @id;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        if (!string.Equals(reader.GetString(0), expectedHash, StringComparison.Ordinal))
            throw new GiftInboxException("gift_request_conflict", "This dispatch request ID was already used with different contents.", 409);
        return new GiftSendResult(id, true, reader.GetInt64(1), ReadDate(reader, 2), ReadNullableDate(reader, 3));
    }

    private static async Task<IReadOnlyList<GiftAccountDto>> LoadRecipientPreviewAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            select a.id, a.display_name, a.account_status, a.is_banned
            from gift_inbox i join accounts a on a.id = i.account_id
            where i.gift_id = @id order by a.display_name, a.id limit 100;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        return await ReadAccountsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<GiftAccountDto>> ReadAccountsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var result = new List<GiftAccountDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new GiftAccountDto(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        return result;
    }

    private static async Task<DateTimeOffset> ReadClockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select clock_timestamp();", connection, transaction);
        return new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync(cancellationToken))!);
    }

    private static DateTimeOffset ReadDate(NpgsqlDataReader reader, int ordinal) => new(reader.GetDateTime(ordinal));
    private static DateTimeOffset? ReadNullableDate(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ReadDate(reader, ordinal);

    private readonly record struct CursorPosition(DateTimeOffset Date, Guid GiftId);

    private static string EncodeCursor(DateTimeOffset date, Guid giftId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{date.UtcTicks}:{giftId:N}"));

    private static CursorPosition? ParseCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            if (cursor.Length > 128) throw new FormatException();
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParseExact(parts[1], "N", out var id) || id == Guid.Empty || ticks % 10 != 0)
                throw new FormatException();
            return new CursorPosition(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception error) when (error is FormatException or ArgumentOutOfRangeException)
        {
            throw new GiftInboxException("gift_cursor_invalid", "The gift inbox cursor is invalid.");
        }
    }
}
