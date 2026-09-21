using Npgsql;
using NpgsqlTypes;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;

namespace Project333.PvpServer.Challenges;

/// <summary>Server-owned, durable daily admissions. Results are settled by BattleResultStore.</summary>
public sealed class AiChallengeService(DbConnectionFactory connectionFactory)
{
    public const int DailyLimit = 3;
    public const int WinResourceGold = 10;
    private static readonly TimeSpan KoreanOffset = TimeSpan.FromHours(9);

    public static (DateOnly Day, DateTimeOffset ResetsAtUtc) GetDayWindow(DateTimeOffset now)
    {
        var local = now.ToOffset(KoreanOffset);
        var day = DateOnly.FromDateTime(local.Date);
        return (day, new DateTimeOffset(local.Date.AddDays(1), KoreanOffset).ToUniversalTime());
    }

    public async Task<AiChallengeStartResponse> StartAsync(
        Guid accountId, AiChallengeStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (accountId == Guid.Empty || request.RequestId == Guid.Empty ||
            !Guid.TryParse(request.RunId, out var runId) || runId == Guid.Empty ||
            !Guid.TryParse(request.DeckId, out var deckId) || deckId == Guid.Empty)
            throw Error("ai_challenge_request_invalid", "A request ID and the current run/deck are required.");

        await using var db = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var tx = await db.BeginTransactionAsync(cancellationToken);
        // Shared with matchmaking: a challenge and normal queue cannot reserve this account together.
        await LockMatchmakingAsync(db, tx, cancellationToken);
        await LockWalletAsync(db, tx, accountId, cancellationToken);
        var now = await ReadClockAsync(db, tx, cancellationToken);
        var window = GetDayWindow(now);

        var previous = await ReadAttemptAsync(db, tx, accountId, requestId: request.RequestId, cancellationToken: cancellationToken);
        if (previous != null)
        {
            EnsureBinding(previous, runId, deckId);
            var replay = await ResponseAsync(db, tx, accountId, previous, window, true, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return replay;
        }

        var pending = await ReadAttemptAsync(db, tx, accountId, pendingOnly: true, cancellationToken: cancellationToken);
        if (pending != null)
        {
            EnsureBinding(pending, runId, deckId);
            // Persist the alias too: replaying this second request after completion must still return this attempt.
            await SaveRequestAsync(db, tx, accountId, request.RequestId, pending.AttemptId, cancellationToken);
            var replay = await ResponseAsync(db, tx, accountId, pending, window, true, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return replay;
        }
        await using (var valid = new NpgsqlCommand("""
            select count(*) from draft_runs r join decks d on d.id=r.completed_deck_id
            where r.id=@run and r.account_id=@account and r.completed_deck_id=@deck
              and r.status in ('ready','in_progress') and d.account_id=@account
              and d.source_run_id=r.id and not d.is_archived
              and (select count(*) from deck_cards where deck_id=d.id)=33;
            """, db, tx))
        {
            valid.Parameters.AddWithValue("account", accountId);
            valid.Parameters.AddWithValue("run", runId);
            valid.Parameters.AddWithValue("deck", deckId);
            if ((long)(await valid.ExecuteScalarAsync(cancellationToken))! != 1)
                throw Error("ai_challenge_deck_invalid", "The challenge requires this account's current 33-card run deck.");
        }
        await using (var busy = new NpgsqlCommand("""
            select exists(select 1 from pvp_match_reservations
                    where account_id=@account and expires_at>@now)
                or exists(select 1 from pvp_match_queue where account_id=@account and queue_status='queued'
                    and (expires_at is null or expires_at>@now))
                or exists(select 1 from pvp_match_players p join pvp_matches m on m.id=p.match_id
                    where p.account_id=@account and m.match_status in ('waiting','active','reconnect_grace')
                      and (m.opponent_kind<>'ai' or m.ai_fallback_account_id=@account)
                      and (exists(select 1 from pvp_match_connections c
                          where c.match_id=m.id and c.account_id=@account and c.connection_status='active')
                          or (m.match_status in ('active','reconnect_grace') and p.reconnect_deadline_at>@now)));
            """, db, tx))
        {
            busy.Parameters.AddWithValue("account", accountId);
            busy.Parameters.AddWithValue("now", now);
            if (await busy.ExecuteScalarAsync(cancellationToken) is true)
                throw Error("battle_already_active", "Finish or reconnect to the current battle before starting a challenge.");
        }
        if (await RemainingAsync(db, tx, accountId, window.Day, cancellationToken) <= 0)
            throw Error("ai_challenge_daily_limit", "All three AI challenge attempts for today have been used.");

        var attempt = new AiChallengeAttemptDto(Guid.NewGuid(), "challenge-" + Guid.NewGuid().ToString("N"), runId, deckId, "reserved");
        await using (var insert = new NpgsqlCommand("""
            insert into ai_challenge_attempts(id,account_id,request_id,run_id,deck_id,match_id,challenge_day,created_at)
            values (@id,@account,@request,@run,@deck,@match,@day,@now);
            """, db, tx))
        {
            insert.Parameters.AddWithValue("id", attempt.AttemptId);
            insert.Parameters.AddWithValue("account", accountId);
            insert.Parameters.AddWithValue("request", request.RequestId);
            insert.Parameters.AddWithValue("run", runId);
            insert.Parameters.AddWithValue("deck", deckId);
            insert.Parameters.AddWithValue("match", attempt.MatchId);
            insert.Parameters.AddWithValue("day", window.Day);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await SaveRequestAsync(db, tx, accountId, request.RequestId, attempt.AttemptId, cancellationToken);
        var response = await ResponseAsync(db, tx, accountId, attempt, window, false, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<AiChallengeStatusResponse> GetStatusAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var tx = await db.BeginTransactionAsync(cancellationToken);
        await LockWalletAsync(db, tx, accountId, cancellationToken);
        var window = GetDayWindow(await ReadClockAsync(db, tx, cancellationToken));
        var pending = await ReadAttemptAsync(db, tx, accountId, pendingOnly: true, cancellationToken: cancellationToken);
        var remaining = await RemainingAsync(db, tx, accountId, window.Day, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new(DailyLimit, remaining, window.ResetsAtUtc, pending);
    }

    public async Task<AiChallengeAttemptDto?> FindAttemptAsync(Guid accountId, Guid attemptId, CancellationToken cancellationToken)
    {
        await using var db = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await ReadAttemptAsync(db, null, accountId, attemptId: attemptId, cancellationToken: cancellationToken);
    }

    public async Task<AiChallengeAttemptDto> MarkBattleJoinedAsync(
        Guid accountId, Guid attemptId, string matchId, CancellationToken cancellationToken)
    {
        await using var db = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update ai_challenge_attempts set status='active', joined_at=coalesce(joined_at,clock_timestamp())
            where id=@id and account_id=@account and match_id=@match and status in ('reserved','active')
            returning id,match_id,run_id,deck_id,status,resource_gold_awarded,result_id,won;
            """, db);
        command.Parameters.AddWithValue("id", attemptId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("match", matchId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw Error("ai_challenge_not_available", "This challenge admission is missing, canceled, or already completed.");
        return ReadAttempt(reader);
    }

    public async Task<bool> CancelAsync(Guid accountId, Guid attemptId, bool refundAttempt,
        string reason, CancellationToken cancellationToken)
    {
        if (refundAttempt && reason != "server_cancel")
            throw new InvalidOperationException("Only a server outage/maintenance cancellation refunds an AI challenge.");
        if (reason is not ("server_cancel" or "join_failed"))
            throw new InvalidOperationException("Invalid challenge cancellation reason.");
        await using var db = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var tx = await db.BeginTransactionAsync(cancellationToken);
        await LockWalletAsync(db, tx, accountId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            update ai_challenge_attempts set status='canceled', ended_reason=@reason, completed_at=clock_timestamp(),
                refunded_at=case when @refund then clock_timestamp() else null end
            where id=@id and account_id=@account and status in ('reserved','active');
            """, db, tx);
        command.Parameters.AddWithValue("id", attemptId);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("refund", refundAttempt);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        await tx.CommitAsync(cancellationToken);
        return changed;
    }

    // Invoke only after replaying durable final results. Exclude any result whose replay remains pending.
    public async Task<int> CancelStaleOnStartupAsync(bool refundAttempt, CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? protectedAttemptIds = null)
    {
        await using var db = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update ai_challenge_attempts set status='canceled', ended_reason='server_cancel',
                completed_at=clock_timestamp(),
                refunded_at=case when @refund then clock_timestamp() else null end
            where status in ('reserved','active') and not(id=any(@protected));
            """, db);
        command.Parameters.AddWithValue("refund", refundAttempt);
        command.Parameters.AddWithValue("protected", NpgsqlDbType.Array | NpgsqlDbType.Uuid,
            protectedAttemptIds?.ToArray() ?? Array.Empty<Guid>());
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<AiChallengeStartResponse> ResponseAsync(NpgsqlConnection db, NpgsqlTransaction tx,
        Guid account, AiChallengeAttemptDto attempt, (DateOnly Day, DateTimeOffset ResetsAtUtc) window,
        bool replayed, CancellationToken ct) =>
        new(attempt.AttemptId, attempt.MatchId, attempt.RunId, attempt.DeckId, attempt.Status,
            await RemainingAsync(db, tx, account, window.Day, ct), window.ResetsAtUtc, replayed);

    private static async Task<int> RemainingAsync(NpgsqlConnection db, NpgsqlTransaction tx, Guid account,
        DateOnly day, CancellationToken ct)
    {
        await using var count = new NpgsqlCommand("""
            select count(*) from ai_challenge_attempts
            where account_id=@account and challenge_day=@day and refunded_at is null;
            """, db, tx);
        count.Parameters.AddWithValue("account", account);
        count.Parameters.AddWithValue("day", day);
        return Math.Max(0, DailyLimit - checked((int)(long)(await count.ExecuteScalarAsync(ct))!));
    }

    private static async Task<AiChallengeAttemptDto?> ReadAttemptAsync(NpgsqlConnection db, NpgsqlTransaction? tx,
        Guid accountId, Guid? requestId = null, Guid? attemptId = null, bool pendingOnly = false,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand("""
            select id,match_id,run_id,deck_id,status,resource_gold_awarded,result_id,won from ai_challenge_attempts a
            where account_id=@account
              and (@request is null or exists(select 1 from ai_challenge_requests q
                  where q.account_id=@account and q.request_id=@request and q.attempt_id=a.id))
              and (@id is null or id=@id)
              and (not @pending or status in ('reserved','active'))
            order by created_at desc limit 1;
            """, db, tx);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("request", NpgsqlDbType.Uuid, requestId.HasValue ? requestId.Value : DBNull.Value);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, attemptId.HasValue ? attemptId.Value : DBNull.Value);
        command.Parameters.AddWithValue("pending", pendingOnly);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAttempt(reader) : null;
    }

    private static AiChallengeAttemptDto ReadAttempt(NpgsqlDataReader reader) =>
        new(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4),
            checked((int)reader.GetInt64(5)), reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetBoolean(7));

    private static async Task SaveRequestAsync(NpgsqlConnection db, NpgsqlTransaction tx, Guid account,
        Guid request, Guid attempt, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            insert into ai_challenge_requests(account_id,request_id,attempt_id) values (@account,@request,@attempt);
            """, db, tx);
        command.Parameters.AddWithValue("account", account);
        command.Parameters.AddWithValue("request", request);
        command.Parameters.AddWithValue("attempt", attempt);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void EnsureBinding(AiChallengeAttemptDto attempt, Guid runId, Guid deckId)
    {
        if (attempt.RunId != runId || attempt.DeckId != deckId)
            throw Error("ai_challenge_request_conflict", "A challenge request cannot be reused with a different run or deck.");
    }

    private static async Task LockWalletAsync(NpgsqlConnection db, NpgsqlTransaction tx, Guid account, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            select w.account_id from user_wallets w join accounts a on a.id=w.account_id
            where w.account_id=@account and a.account_status='active' and not a.is_banned for update of w;
            """, db, tx);
        command.Parameters.AddWithValue("account", account);
        if (await command.ExecuteScalarAsync(ct) is not Guid)
            throw Error("ai_challenge_account_unavailable", "The account wallet is unavailable.");
    }

    private static async Task LockMatchmakingAsync(NpgsqlConnection db, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select pg_advisory_xact_lock(333,13);", db, tx);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<DateTimeOffset> ReadClockAsync(NpgsqlConnection db, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select clock_timestamp();", db, tx);
        return new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync(ct))!);
    }

    private static RunServiceException Error(string code, string message) => new(code, message);
}
