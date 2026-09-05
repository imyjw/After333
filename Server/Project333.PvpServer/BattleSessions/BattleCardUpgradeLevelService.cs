using Npgsql;
using Project333.PvpServer.Persistence.Db;

namespace Project333.PvpServer.BattleSessions;

public sealed class BattleCardUpgradeLevelService
{
    private readonly DbConnectionFactory _dbConnectionFactory;

    public BattleCardUpgradeLevelService(DbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
    }

    public bool IsDatabaseConfigured => _dbConnectionFactory.IsConfigured;

    public async Task<IReadOnlyDictionary<string, int>> LoadUpgradeLevelsAsync(
        string? runId,
        string? deckId,
        CancellationToken cancellationToken)
    {
        if (!IsDatabaseConfigured)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var accountId = await ResolveAccountIdAsync(runId, deckId, cancellationToken);
        if (accountId == null)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        await using var connection = await _dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        const string sql = """
            select card_id, upgrade_level
            from user_card_collection
            where account_id = @accountId
              and upgrade_level > 0
            order by card_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accountId", accountId.Value);

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = reader.GetInt32(1);
        }

        return result;
    }

    private async Task<Guid?> ResolveAccountIdAsync(
        string? runId,
        string? deckId,
        CancellationToken cancellationToken)
    {
        if (TryParseGuid(deckId, out var parsedDeckId))
        {
            var accountId = await ResolveAccountIdFromDeckAsync(parsedDeckId, cancellationToken);
            if (accountId != null)
            {
                return accountId;
            }
        }

        if (TryParseGuid(runId, out var parsedRunId))
        {
            return await ResolveAccountIdFromRunAsync(parsedRunId, cancellationToken);
        }

        return null;
    }

    private async Task<Guid?> ResolveAccountIdFromDeckAsync(Guid deckId, CancellationToken cancellationToken)
    {
        await using var connection = await _dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        const string sql = """
            select account_id
            from decks
            where id = @deckId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("deckId", deckId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid accountId ? accountId : null;
    }

    private async Task<Guid?> ResolveAccountIdFromRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var connection = await _dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        const string sql = """
            select account_id
            from draft_runs
            where id = @runId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("runId", runId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid accountId ? accountId : null;
    }

    private static bool TryParseGuid(string? value, out Guid guid)
    {
        return Guid.TryParse(value?.Trim(), out guid);
    }
}
