using Npgsql;

namespace Project333.PvpServer.Auth;

public sealed partial class GuestAuthService
{
    // Refresh rotates tokens within a family. Only an explicit login replaces it.
    public event Action<Guid>? LoginSessionsChanged;

    private static AuthServiceException SessionReplaced() => new(
        "session_replaced", "다른 기기에서 로그인하여 연결이 종료되었습니다.");

    private void NotifyLoginSessionsChanged(Guid accountId)
    {
        var observers = LoginSessionsChanged;
        if (observers == null) return;
        foreach (Action<Guid> observer in observers.GetInvocationList())
        {
            // The login has committed. A transport observer cannot roll it back or
            // turn a valid response into an error; transports also recheck the DB.
            try { observer(accountId); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Login-session observer failed ({0}).", ex.GetType().Name); }
        }
    }

    public async Task ValidateSessionAsync(string? authorizationHeader, CancellationToken cancellationToken)
    {
        var token = ExtractBearerToken(authorizationHeader);
        if (string.IsNullOrWhiteSpace(token))
            throw new AuthServiceException("missing_session_token", "Bearer session token is required.");

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var session = await FindAccountBySessionAsync(connection, transaction, _tokenService.HashToken(token), cancellationToken);
        if (session?.IsReplaced == true) throw SessionReplaced();
        if (session == null || !session.IsValid)
            throw new AuthServiceException("invalid_session_token", "Session token is invalid, expired, or revoked.");
        EnsureLoginAccountActive(session.Account);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> IsLoginFamilyCurrentAsync(Guid accountId, Guid loginFamilyId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        const string sql = """
            select exists (
                select 1 from accounts a
                where a.id=@accountId and a.active_login_family_id=@familyId
                  and a.account_status='active' and not a.is_banned and a.account_kind<>'guest'
                  and exists (select 1 from auth_sessions s where s.account_id=a.id
                      and s.login_family_id=@familyId and s.revoked_at is null and s.expires_at>now())
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("familyId", loginFamilyId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<T> ExecuteWithCurrentLoginFamilyAsync<T>(Guid accountId, Guid loginFamilyId,
        Func<T> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        const string sql = """
            select a.active_login_family_id, a.is_banned, a.account_status, a.account_kind,
                exists (select 1 from auth_sessions s where s.account_id=a.id
                    and s.login_family_id=@familyId and s.revoked_at is null and s.expires_at>now())
            from accounts a where a.id=@accountId for share of a;
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("accountId", accountId);
            command.Parameters.AddWithValue("familyId", loginFamilyId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.GetGuid(0)!=loginFamilyId)
                throw SessionReplaced();
            if (reader.GetBoolean(1) || reader.GetString(2)!="active")
                throw new AuthServiceException("account_inactive", "This account is not active.");
            if (reader.GetString(3)==GuestAccountKind)
                throw new AuthServiceException("guest_accounts_disabled", "Guest accounts are no longer supported. Please sign in with Game ID or Google.");
            if (!reader.GetBoolean(4))
                throw new AuthServiceException("invalid_session_token", "Session token is invalid, expired, or revoked.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        // Keep the account lock until this short synchronous battle mutation ends.
        // Login/refresh/logout acquire FOR UPDATE before touching token rows.
        var result = action();
        await transaction.CommitAsync(CancellationToken.None);
        return result;
    }

    private static void EnsureLoginAccountActive(AccountRow account)
    {
        if (account.IsBanned || !string.Equals(account.AccountStatus, "active", StringComparison.OrdinalIgnoreCase))
            throw new AuthServiceException("account_inactive", "This account is not active.");
        if (string.Equals(account.AccountKind, GuestAccountKind, StringComparison.OrdinalIgnoreCase))
            throw new AuthServiceException("guest_accounts_disabled", "Guest accounts are no longer supported. Please sign in with Game ID or Google.");
    }

    private static async Task StartIndependentLoginAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid accountId, SessionTokenPair tokenPair, ClientRequestContext clientContext, CancellationToken cancellationToken)
    {
        const string lockSql = """
            select id,display_name,account_kind,is_banned,account_status
            from accounts where id=@accountId for update;
            """;
        await using (var command = new NpgsqlCommand(lockSql, connection, transaction))
        {
            command.Parameters.AddWithValue("accountId", accountId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new AuthServiceException("invalid_credentials", "This account does not exist.");
            EnsureLoginAccountActive(ReadAccount(reader));
        }
        var familyId = Guid.NewGuid();
        // now() is the transaction start, which can precede a newer session's
        // creation while this login waits for the account lock. Record the actual
        // revocation time so parallel logins preserve revoked_at >= created_at.
        const string revokeSql = """
            update auth_sessions set revoked_at=coalesce(revoked_at,clock_timestamp()),
                superseded_at=coalesce(superseded_at,clock_timestamp()) where account_id=@accountId;
            update auth_refresh_tokens set revoked_at=coalesce(revoked_at,clock_timestamp()) where account_id=@accountId;
            update accounts set active_login_family_id=@familyId,updated_at=clock_timestamp() where id=@accountId;
            """;
        await using (var command = new NpgsqlCommand(revokeSql, connection, transaction))
        {
            command.Parameters.AddWithValue("accountId", accountId);
            command.Parameters.AddWithValue("familyId", familyId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertSessionPairAsync(connection, transaction, accountId, tokenPair, clientContext, familyId, cancellationToken);
    }

    private static async Task<Guid?> LockRefreshAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string refreshTokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            select a.active_login_family_id from accounts a
            where a.id=(select rt.account_id from auth_refresh_tokens rt where rt.refresh_token_hash=@hash)
            for update of a;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("hash", refreshTokenHash);
        return await command.ExecuteScalarAsync(cancellationToken) is Guid familyId ? familyId : null;
    }

    private static async Task LockLogoutAccountsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string? sessionTokenHash, string? refreshTokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            select a.id from accounts a where a.id in (
                select s.account_id from auth_sessions s where s.session_token_hash=@sessionHash
                union select rt.account_id from auth_refresh_tokens rt where rt.refresh_token_hash=@refreshHash
            ) order by a.id for update of a;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("sessionHash", sessionTokenHash ?? string.Empty);
        command.Parameters.AddWithValue("refreshHash", refreshTokenHash ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record SessionAccountRow(AccountRow Account, Guid? LoginFamilyId, bool IsReplaced, bool IsValid);
}
