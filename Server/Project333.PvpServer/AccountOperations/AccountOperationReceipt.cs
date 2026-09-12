using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Project333.PvpServer.Auth;

namespace Project333.PvpServer.AccountOperations;

public sealed class AccountOperationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

// Callers hold the wallet row lock for the entire receipt lookup, mutation and commit.
// A rollback (including cancellation) leaves neither a charge nor a success receipt.
public static class AccountOperationReceipt
{
    public static Guid ParseRequestId(string? value)
    {
        if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty)
            throw new AccountOperationException("invalid_request_id", "A non-empty UUID RequestId is required. Update the client before retrying.");
        return id;
    }

    public static async Task<WalletDto> LockWalletAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select resource_gold, tickets from user_wallets where account_id=@id for update", connection, transaction);
        command.Parameters.AddWithValue("id", accountId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new AccountOperationException("wallet_not_found", "Account wallet was not found.");
        return new WalletDto(reader.GetInt64(0), reader.GetInt32(1));
    }

    public static async Task<T?> FindAsync<T>(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, Guid requestId, string operation, string fingerprint, CancellationToken ct) where T : class
    {
        await ThrowIfCancelledAsync(connection, transaction, accountId, requestId, ct);
        await using var command = new NpgsqlCommand("""
            select operation_type, request_fingerprint, response_json::text
            from account_operation_receipts where account_id=@id and request_id=@request
            """, connection, transaction);
        command.Parameters.AddWithValue("id", accountId);
        command.Parameters.AddWithValue("request", requestId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (reader.GetString(0) != operation || reader.GetString(1) != fingerprint)
            throw new AccountOperationException("request_id_conflict", "RequestId already belongs to a different operation or payload.");
        return JsonSerializer.Deserialize<T>(reader.GetString(2))
            ?? throw new InvalidOperationException("Stored account operation receipt is invalid.");
    }

    public static async Task SaveAsync<T>(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, Guid requestId, string operation, string fingerprint, T response, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            insert into account_operation_receipts(account_id,request_id,operation_type,request_fingerprint,response_json)
            values(@id,@request,@operation,@fingerprint,@response)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", accountId);
        command.Parameters.AddWithValue("request", requestId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("response", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(response));
        await command.ExecuteNonQueryAsync(ct);
    }
    private static async Task ThrowIfCancelledAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, Guid requestId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "select 1 from account_operation_cancellations where account_id=@id and request_id=@request", connection, transaction);
        command.Parameters.AddWithValue("id", accountId);
        command.Parameters.AddWithValue("request", requestId);
        if (await command.ExecuteScalarAsync(ct) != null)
            throw new AccountOperationException("operation_cancelled", "This operation was cancelled.");
    }

    // The purchase/upgrade wallet lock serializes completion against durable cancellation.
    public static async Task<AccountOperationResolution> ResolveAsync(NpgsqlConnection connection,
        Guid accountId, string? rawRequestId, CancellationToken ct)
    {
        var requestId = ParseRequestId(rawRequestId);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await LockWalletAsync(connection, transaction, accountId, ct);
        await using var query = new NpgsqlCommand(
            "select 1 from account_operation_receipts where account_id=@id and request_id=@request", connection, transaction);
        query.Parameters.AddWithValue("id", accountId);
        query.Parameters.AddWithValue("request", requestId);
        var completed = await query.ExecuteScalarAsync(ct) != null;
        if (!completed)
        {
            await using var cancel = new NpgsqlCommand("""
                insert into account_operation_cancellations(account_id,request_id) values(@id,@request)
                on conflict(account_id,request_id) do nothing
                """, connection, transaction);
            cancel.Parameters.AddWithValue("id", accountId);
            cancel.Parameters.AddWithValue("request", requestId);
            await cancel.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new AccountOperationResolution(accountId.ToString("D"), requestId.ToString("D"), completed ? "completed" : "cancelled");
    }
}
public sealed record ResolveAccountOperationRequest(string? RequestId);
public sealed record AccountOperationResolution(string AccountId, string RequestId, string Status);
