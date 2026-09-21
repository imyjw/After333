using Npgsql;
using Project333.PvpServer.BattleSessions;

namespace Project333.PvpServer.Matchmaking;

public sealed partial class PvpMatchmakingService
{
    // Called only by the server's waiting-room timer. No client message grants an AI
    // opponent, and a queue cancel/rejoin starts a new connection's waiting interval.
    public async Task<ServerAiFallbackStartResult> TryStartServerAiFallbackAsync(
        BattleSession session, TimeSpan minimumWait, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (minimumWait <= TimeSpan.Zero || minimumWait > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(minimumWait));
        if (!IsDatabaseConfigured || !session.TryReserveServerAiFallback(out var waitingConnection))
            return new(false, string.Empty);

        var claimed = false;
        var started = false;
        try
        {
            var human = waitingConnection!;
            await using (var db = await _connectionFactory.OpenConnectionAsync(cancellationToken))
            await using (var tx = await db.BeginTransactionAsync(cancellationToken))
            {
                await LockMatchmakingAsync(db, tx, cancellationToken);
                await ExpireReservationsAsync(db, tx, cancellationToken);
                await using var claim = new NpgsqlCommand("""
                    with eligible as (
                        select m.id, p.account_id
                        from pvp_matches m
                        join pvp_match_players p on p.match_id=m.id
                        join pvp_match_connections c on c.match_id=m.id and c.account_id=p.account_id
                        where m.match_id=@match and m.match_status='waiting' and m.started_at is null
                          and m.opponent_kind='human' and p.account_id=@account and p.player_status='active'
                          and p.seat=@seat and c.connection_id=@connection and c.connection_status='active'
                          and c.connected_at+(@seconds*interval '1 second') <= clock_timestamp()
                          and exists (select 1 from pvp_match_queue q where q.matched_match_id=m.id
                              and q.account_id=p.account_id and q.queue_status='matched'
                              and q.requested_at+(@seconds*interval '1 second') <= clock_timestamp())
                          and (select count(*) from pvp_match_connections other
                              where other.match_id=m.id and other.connection_status='active')=1
                          and not exists (select 1 from pvp_match_reservations r where r.match_id=m.id)
                        for update of m
                    ), changed_match as (
                        update pvp_matches m
                        set opponent_kind='ai', ai_fallback_at=clock_timestamp(), ai_fallback_account_id=e.account_id,
                            match_status='active', started_at=clock_timestamp(), updated_at=clock_timestamp()
                        from eligible e where m.id=e.id returning m.id
                    )
                    update pvp_match_players p set runtime_player_id='Player', last_seen_at=clock_timestamp()
                    from eligible e where p.match_id=e.id and p.account_id=e.account_id
                      and exists (select 1 from changed_match m where m.id=e.id);
                    """, db, tx);
                claim.Parameters.AddWithValue("match", session.MatchId);
                claim.Parameters.AddWithValue("account", ParseRequiredGuid(human.AccountId, nameof(human.AccountId)));
                claim.Parameters.AddWithValue("seat", NormalizeSeat(human.AssignedOnlineSeatId.ToString()));
                claim.Parameters.AddWithValue("connection", human.ConnectionId);
                claim.Parameters.AddWithValue("seconds", minimumWait.TotalSeconds);
                claimed = await claim.ExecuteNonQueryAsync(cancellationToken) == 1;
                await tx.CommitAsync(cancellationToken);
            }
            if (!claimed) return new(false, string.Empty);

            // A disconnect is allowed while the database claim is in progress. In that
            // case close the claimed room without a battle/result rather than reuse it.
            started = session.TryActivateServerAiFallback(waitingConnection!, out var message);
            return new(started, message);
        }
        finally
        {
            try
            {
                if (claimed && !started)
                {
                    // A failed initializer or transport lost during the claim cannot leave
                    // a live in-memory room whose durable match has already been canceled.
                    session.CancelForServerShutdown();
                    foreach (var connection in session.SnapshotConnections())
                        connection.TerminateTransport();
                    await CancelUnstartedServerAiFallbackAsync(session.MatchId, CancellationToken.None);
                }
            }
            finally { session.ReleaseServerAiFallbackReservation(); }
        }
    }

    private async Task CancelUnstartedServerAiFallbackAsync(string matchId, CancellationToken ct)
    {
        await using var db = await _connectionFactory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await LockMatchmakingAsync(db, tx, ct);
        await using var cancel = new NpgsqlCommand("""
            with canceled as (
                update pvp_matches set match_status='canceled', winner_account_id=null,
                    ended_reason='server_cancel', completed_at=clock_timestamp(), updated_at=clock_timestamp()
                where match_id=@match and opponent_kind='ai'
                  and match_status in ('waiting','active','reconnect_grace') and completed_at is null
                returning id
            ), closed as (
                update pvp_match_connections c set connection_status='closed',
                    disconnected_at=coalesce(disconnected_at,clock_timestamp()), last_seen_at=clock_timestamp()
                where c.match_id in (select id from canceled) and c.connection_status='active'
                returning c.match_id
            ), players as (
                update pvp_match_players p set player_status='disconnected', final_result=null,
                    reconnect_deadline_at=null, disconnected_at=coalesce(disconnected_at,clock_timestamp()),
                    last_seen_at=clock_timestamp()
                where p.match_id in (select id from canceled) returning p.match_id
            )
            update pvp_match_queue q set queue_status='canceled', canceled_at=clock_timestamp(),updated_at=clock_timestamp()
            where q.matched_match_id in (select id from canceled) and q.queue_status='matched';
            """, db, tx);
        cancel.Parameters.AddWithValue("match", matchId);
        await cancel.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}

public sealed record ServerAiFallbackStartResult(bool Started, string Message);
