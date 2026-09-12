using Npgsql;

namespace Project333.PvpServer.Matchmaking;

public sealed partial class PvpMatchmakingService
{
    // An internal join lease, not a new battle/reconnect timer. A failed process
    // cannot reserve an unjoined seat forever. No socket I/O holds this DB lock.
    public const int JoinReservationSeconds = 60;
    private static async Task LockMatchmakingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select pg_advisory_xact_lock(333, 13);", connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<PvpMatchmakingMatchAssignment> ResolveMatchForQueueAsync(PvpMatchmakingQueueRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = ParseRequiredGuid(request.AccountId, nameof(request.AccountId));
        var connectionId = NormalizeRequiredText(request.ConnectionId, nameof(request.ConnectionId));
        var run = ParseOptionalGuid(request.RunId);
        var deck = ParseOptionalGuid(request.DeckId);
        await using var db = await _connectionFactory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await LockMatchmakingAsync(db, tx, ct);
        await ExpireReservationsAsync(db, tx, ct);

        // The same account cannot obtain several pending seats through parallel sockets.
        await using (var own = new NpgsqlCommand("""
            select m.match_id, r.id, r.seat, r.connection_id, r.run_id, r.deck_id
            from pvp_match_reservations r join pvp_matches m on m.id=r.match_id
            where r.account_id=@account;
            """, db, tx))
        {
            own.Parameters.AddWithValue("account", account);
            await using var reader = await own.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(3) != connectionId ||
                    (reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4)) != run ||
                    (reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5)) != deck)
                    throw new InvalidOperationException("This account already has a pending matchmaking connection.");
                var result = new PvpMatchmakingMatchAssignment(reader.GetString(0), false, false, reader.GetString(2), reader.GetGuid(1));
                await reader.DisposeAsync(); await tx.CommitAsync(ct); return result;
            }
        }

        await using (var own = new NpgsqlCommand("""
            select m.match_id, p.seat, m.match_status
            from pvp_match_players p join pvp_matches m on m.id=p.match_id
            where p.account_id=@account and m.match_status in ('waiting','active','reconnect_grace')
              and exists (select 1 from pvp_match_connections c where c.match_id=p.match_id and c.account_id=p.account_id and c.connection_status='active')
            order by m.created_at limit 1;
            """, db, tx))
        {
            own.Parameters.AddWithValue("account", account);
            await using var reader = await own.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(2) != "waiting") throw new InvalidOperationException("This account already has an active battle.");
                var result = new PvpMatchmakingMatchAssignment(reader.GetString(0), false, false, reader.GetString(1));
                await reader.DisposeAsync(); await tx.CommitAsync(ct); return result;
            }
        }

        Guid? matchDbId = null;
        string? matchId = null;
        string seat = "PlayerA";
        // Reservations count immediately, even before a WebSocket has joined memory.
        await using (var candidate = new NpgsqlCommand("""
            select m.id, m.match_id, case when 'PlayerA'=any(o.seats) then 'PlayerB' else 'PlayerA' end
            from pvp_matches m
            cross join lateral (
                select array_agg(s.seat) as seats, count(*) as count from (
                    select p.seat from pvp_match_players p where p.match_id=m.id
                      and exists (select 1 from pvp_match_connections c where c.match_id=p.match_id and c.account_id=p.account_id and c.connection_status='active')
                    union select r.seat from pvp_match_reservations r where r.match_id=m.id
                ) s
            ) o
            where m.match_status='waiting' and m.started_at is null and o.count=1
            order by m.created_at, m.id limit 1 for update of m;
            """, db, tx))
        {
            await using var reader = await candidate.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) { matchDbId = reader.GetGuid(0); matchId = reader.GetString(1); seat = reader.GetString(2); }
        }
        var created = !matchDbId.HasValue;
        if (created)
        {
            matchId = CreateMatchId();
            matchDbId = await UpsertMatchAsync(db, tx, matchId, request.ClientVersion, ct);
        }
        await UpsertQueuedRequestAsync(db, tx, account, run, deck, request.ClientVersion, ct);
        await MarkQueueMatchedAsync(db, tx, account, run, deck, matchDbId!.Value, request.ClientVersion, ct);
        await using var reserve = new NpgsqlCommand("""
            insert into pvp_match_reservations(match_id, seat, account_id, connection_id, run_id, deck_id, expires_at)
            values (@match,@seat,@account,@connection,@run,@deck,clock_timestamp()+(@seconds*interval '1 second')) returning id;
            """, db, tx);
        reserve.Parameters.AddWithValue("match", matchDbId.Value);
        reserve.Parameters.AddWithValue("seat", seat);
        reserve.Parameters.AddWithValue("account", account);
        reserve.Parameters.AddWithValue("connection", connectionId);
        reserve.Parameters.AddWithValue("run", ToDbGuid(run));
        reserve.Parameters.AddWithValue("deck", ToDbGuid(deck));
        reserve.Parameters.AddWithValue("seconds", JoinReservationSeconds);
        var reservationId = (Guid)(await reserve.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);
        return new PvpMatchmakingMatchAssignment(matchId!, created, !created, seat, reservationId);
    }

    public async Task ReleaseReservationAsync(Guid reservationId, string connectionId, CancellationToken ct)
    {
        await using var db = await _connectionFactory.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await LockMatchmakingAsync(db, tx, ct);
        await using var command = new NpgsqlCommand("""
            with removed as (delete from pvp_match_reservations where id=@id and connection_id=@connection returning match_id,account_id)
            update pvp_match_queue q set queue_status='canceled', canceled_at=clock_timestamp(), updated_at=clock_timestamp()
            from removed r where q.matched_match_id=r.match_id and q.account_id=r.account_id and q.queue_status='matched';
            """, db, tx);
        command.Parameters.AddWithValue("id", reservationId);
        command.Parameters.AddWithValue("connection", connectionId);
        await command.ExecuteNonQueryAsync(ct);
        await CancelEmptyWaitingMatchesAsync(db, tx, ct);
        await tx.CommitAsync(ct);
    }

    private static async Task ExpireReservationsAsync(NpgsqlConnection db, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            with expired as (delete from pvp_match_reservations r using pvp_matches m
                where r.match_id=m.id and (r.expires_at<=clock_timestamp() or m.match_status<>'waiting') returning r.match_id,r.account_id)
            update pvp_match_queue q set queue_status='expired', updated_at=clock_timestamp()
            from expired r where q.matched_match_id=r.match_id and q.account_id=r.account_id and q.queue_status='matched';
            """, db, tx);
        await command.ExecuteNonQueryAsync(ct);
        await CancelEmptyWaitingMatchesAsync(db, tx, ct);
    }

    private static async Task CancelEmptyWaitingMatchesAsync(NpgsqlConnection db, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            update pvp_matches m set match_status='canceled', ended_reason='server_cancel', completed_at=clock_timestamp(),updated_at=clock_timestamp()
            where m.match_status='waiting' and m.started_at is null
              and not exists (select 1 from pvp_match_reservations r where r.match_id=m.id)
              and not exists (select 1 from pvp_match_connections c where c.match_id=m.id and c.connection_status='active');
            """, db, tx);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ValidateAndConsumeReservationAsync(NpgsqlConnection db, NpgsqlTransaction tx, Guid matchId,
        Guid accountId, PvpMatchmakingConnectionRecord record, CancellationToken ct)
    {
        if (record.ReservationId.HasValue)
        {
            await using var consume = new NpgsqlCommand("""
                delete from pvp_match_reservations r using pvp_matches m
                where r.id=@id and r.match_id=@match and r.account_id=@account and r.connection_id=@connection
                  and r.seat=@seat and r.run_id is not distinct from @run and r.deck_id is not distinct from @deck
                  and r.expires_at>clock_timestamp() and m.id=r.match_id and m.match_status='waiting' and m.started_at is null;
                """, db, tx);
            consume.Parameters.AddWithValue("id", record.ReservationId.Value);
            consume.Parameters.AddWithValue("match", matchId);
            consume.Parameters.AddWithValue("account", accountId);
            consume.Parameters.AddWithValue("connection", record.ConnectionId);
            consume.Parameters.AddWithValue("seat", NormalizeSeat(record.Seat));
            consume.Parameters.AddWithValue("run", ToDbGuid(ParseOptionalGuid(record.RunId)));
            consume.Parameters.AddWithValue("deck", ToDbGuid(ParseOptionalGuid(record.DeckId)));
            if (await consume.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Matchmaking reservation expired or did not match this connection.");
        }
        await using var check = new NpgsqlCommand("""
            select exists(select 1 from pvp_match_reservations where match_id=@match and (seat=@seat or account_id=@account))
                or exists(select 1 from pvp_match_players p join pvp_match_connections c on c.match_id=p.match_id and c.account_id=p.account_id
                    where p.match_id=@match and p.seat=@seat and p.account_id<>@account and c.connection_status='active')
                or exists(select 1 from pvp_matches where id=@match and match_status in ('completed','canceled'));
            """, db, tx);
        check.Parameters.AddWithValue("match", matchId);
        check.Parameters.AddWithValue("account", accountId);
        check.Parameters.AddWithValue("seat", NormalizeSeat(record.Seat));
        if ((bool)(await check.ExecuteScalarAsync(ct))!) throw new InvalidOperationException("The requested match seat is reserved, occupied or closed.");
    }

    private static async Task ReleaseWaitingPlayerAsync(NpgsqlConnection db, NpgsqlTransaction tx, Guid matchId, Guid accountId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            update pvp_match_players p set player_status='disconnected', disconnected_at=clock_timestamp(), last_seen_at=clock_timestamp()
            from pvp_matches m where p.match_id=m.id and m.id=@match and p.account_id=@account and m.match_status='waiting' and m.started_at is null;
            update pvp_match_queue q set queue_status='canceled', canceled_at=clock_timestamp(),updated_at=clock_timestamp()
            from pvp_matches m where q.matched_match_id=m.id and m.id=@match and q.account_id=@account
              and q.queue_status='matched' and m.match_status='waiting' and m.started_at is null;
            """, db, tx);
        command.Parameters.AddWithValue("match", matchId); command.Parameters.AddWithValue("account", accountId);
        await command.ExecuteNonQueryAsync(ct);
        await CancelEmptyWaitingMatchesAsync(db, tx, ct);
    }
}
