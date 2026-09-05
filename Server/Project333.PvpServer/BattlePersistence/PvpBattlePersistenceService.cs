using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Persistence.Db;

namespace Project333.PvpServer.BattlePersistence;

public sealed class PvpBattlePersistenceService
{
    private readonly DbConnectionFactory _connectionFactory;

    public PvpBattlePersistenceService(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public bool IsDatabaseConfigured => _connectionFactory.IsConfigured;

    public async Task RecordSnapshotAsync(
        PvpBattleSnapshotRecord record,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        if (record == null ||
            string.IsNullOrWhiteSpace(record.MatchId) ||
            record.Snapshot == null ||
            !IsDatabaseConfigured)
        {
            return;
        }

        var snapshotJson = JsonSerializer.Serialize(record.Snapshot, jsonOptions);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var matchDbId = await FindMatchDbIdAsync(connection, transaction, record.MatchId, cancellationToken);
        if (matchDbId == null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await LockMatchAsync(connection, transaction, matchDbId.Value, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into pvp_battle_snapshots(
                match_id,
                snapshot_version,
                snapshot_reason,
                state_json,
                created_at)
            select
                @match_id,
                coalesce(max(snapshot_version), 0) + 1,
                @snapshot_reason,
                @state_json,
                now()
            from pvp_battle_snapshots
            where match_id = @match_id;
            """;
        command.Parameters.AddWithValue("match_id", matchDbId.Value);
        command.Parameters.AddWithValue("snapshot_reason", NormalizeSnapshotReason(record.Reason));
        command.Parameters.Add("state_json", NpgsqlDbType.Jsonb).Value = snapshotJson;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordCommandAsync(
        PvpBattleCommandLogRecord record,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        if (record == null ||
            string.IsNullOrWhiteSpace(record.MatchId) ||
            record.Command == null ||
            !IsDatabaseConfigured)
        {
            return;
        }

        var commandJson = JsonSerializer.Serialize(record.Command, jsonOptions);
        var eventJson = record.Events == null
            ? null
            : JsonSerializer.Serialize(record.Events, jsonOptions);
        var accountId = ParseOptionalGuid(record.AccountId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var matchDbId = await FindMatchDbIdAsync(connection, transaction, record.MatchId, cancellationToken);
        if (matchDbId == null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into pvp_battle_command_log(
                match_id,
                client_sequence,
                connection_id,
                account_id,
                actor_seat,
                runtime_actor_id,
                command_type,
                command_json,
                event_json,
                accepted,
                rejection_code,
                rejection_message,
                created_at)
            values (
                @match_id,
                @client_sequence,
                @connection_id,
                @account_id,
                @actor_seat,
                @runtime_actor_id,
                @command_type,
                @command_json,
                @event_json,
                @accepted,
                @rejection_code,
                @rejection_message,
                now());
            """;
        command.Parameters.AddWithValue("match_id", matchDbId.Value);
        command.Parameters.AddWithValue("client_sequence", ToDbLong(record.ClientSequence));
        command.Parameters.AddWithValue("connection_id", ToDbText(record.ConnectionId));
        command.Parameters.AddWithValue("account_id", ToDbGuid(accountId));
        command.Parameters.AddWithValue("actor_seat", ToDbText(NormalizeOptionalSeat(record.ActorSeat)));
        command.Parameters.AddWithValue("runtime_actor_id", ToDbText(NormalizeOptionalRuntimeActor(record.RuntimeActorId)));
        command.Parameters.AddWithValue("command_type", NormalizeRequiredText(record.CommandType, nameof(record.CommandType)));
        command.Parameters.Add("command_json", NpgsqlDbType.Jsonb).Value = commandJson;
        command.Parameters.Add("event_json", NpgsqlDbType.Jsonb).Value = eventJson == null ? DBNull.Value : eventJson;
        command.Parameters.AddWithValue("accepted", record.Accepted);
        command.Parameters.AddWithValue("rejection_code", record.Accepted ? DBNull.Value : ToDbText(record.RejectionCode));
        command.Parameters.AddWithValue("rejection_message", record.Accepted ? DBNull.Value : ToDbText(record.RejectionMessage));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PvpBattleSnapshotReadResult?> LoadLatestSnapshotAsync(
        string matchId,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matchId) || !IsDatabaseConfigured)
        {
            return null;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select snapshots.snapshot_version,
                   snapshots.snapshot_reason,
                   snapshots.state_json::text,
                   snapshots.created_at
            from pvp_battle_snapshots snapshots
            join pvp_matches matches on matches.id = snapshots.match_id
            where matches.match_id = @match_id
            order by snapshots.snapshot_version desc
            limit 1;
            """;
        command.Parameters.AddWithValue("match_id", NormalizeRequiredText(matchId, nameof(matchId)));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var snapshotVersion = reader.GetInt64(0);
        var reason = reader.GetString(1);
        var stateJson = reader.GetString(2);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(3).ToUniversalTime();
        var snapshot = JsonSerializer.Deserialize<BattleSessionPersistenceSnapshot>(
            stateJson,
            jsonOptions);

        return snapshot == null
            ? null
            : new PvpBattleSnapshotReadResult(
                matchId.Trim(),
                snapshotVersion,
                reason,
                createdAt,
                snapshot);
    }

    private static async Task<Guid?> FindMatchDbIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string matchId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id from pvp_matches where match_id = @match_id limit 1;";
        command.Parameters.AddWithValue("match_id", NormalizeRequiredText(matchId, nameof(matchId)));
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is Guid id ? id : null;
    }

    private static async Task LockMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid matchDbId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id from pvp_matches where id = @match_id for update;";
        command.Parameters.AddWithValue("match_id", matchDbId);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static string NormalizeSnapshotReason(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "manual" : value.Trim();
        return normalized is "battle_started" or
            "client_command" or
            "server_ai_action" or
            "turn_timeout" or
            "disconnect_forfeit" or
            "reconnect_join" or
            "manual"
            ? normalized
            : "manual";
    }

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalSeat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized is "PlayerA" or "PlayerB" ? normalized : null;
    }

    private static string? NormalizeOptionalRuntimeActor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized is "Player" or "AI" ? normalized : null;
    }

    private static Guid? ParseOptionalGuid(string? value)
    {
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static object ToDbGuid(Guid? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object ToDbLong(long? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object ToDbText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}

public sealed record PvpBattleSnapshotRecord(
    string MatchId,
    string Reason,
    object Snapshot);

public sealed record PvpBattleSnapshotReadResult(
    string MatchId,
    long SnapshotVersion,
    string Reason,
    DateTimeOffset CreatedAtUtc,
    BattleSessionPersistenceSnapshot Snapshot);

public sealed record PvpBattleCommandLogRecord(
    string MatchId,
    long? ClientSequence,
    string ConnectionId,
    string AccountId,
    string ActorSeat,
    string RuntimeActorId,
    string CommandType,
    object Command,
    object? Events,
    bool Accepted,
    string RejectionCode,
    string RejectionMessage);
