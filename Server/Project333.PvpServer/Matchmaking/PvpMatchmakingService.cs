using Npgsql;
using Project333.PvpServer.Persistence.Db;

namespace Project333.PvpServer.Matchmaking;

public sealed class PvpMatchmakingService
{
    private readonly DbConnectionFactory _connectionFactory;

    public PvpMatchmakingService(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public bool IsDatabaseConfigured => _connectionFactory.IsConfigured;

    public async Task<PvpMatchmakingStartupRecoveryResult> MarkRecoverableMatchesReconnectGraceOnServerStartupAsync(
        TimeSpan reconnectGracePeriod,
        CancellationToken cancellationToken)
    {
        if (!IsDatabaseConfigured)
        {
            return new PvpMatchmakingStartupRecoveryResult(0, 0, 0);
        }

        var safeGraceSeconds = Math.Max(1, (int)Math.Ceiling(reconnectGracePeriod.TotalSeconds));

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with recoverable_matches as (
                update pvp_matches matches
                set match_status = 'reconnect_grace',
                    updated_at = now()
                where matches.match_status in ('active', 'reconnect_grace')
                  and matches.completed_at is null
                returning matches.id
            ),
            closed_connections as (
                update pvp_match_connections connections
                set connection_status = 'closed',
                    disconnected_at = coalesce(connections.disconnected_at, now()),
                    last_seen_at = now()
                where connections.match_id in (select id from recoverable_matches)
                  and connections.connection_status = 'active'
                returning connections.id
            ),
            disconnected_players as (
                update pvp_match_players players
                set player_status = 'disconnected',
                    disconnected_at = coalesce(players.disconnected_at, now()),
                    reconnect_deadline_at = case
                        when players.reconnect_deadline_at is not null and players.reconnect_deadline_at > now()
                            then players.reconnect_deadline_at
                        else now() + (@grace_seconds * interval '1 second')
                    end,
                    last_seen_at = now()
                where players.match_id in (select id from recoverable_matches)
                  and (
                      players.player_status = 'active' or
                      (
                          players.player_status = 'disconnected' and
                          (
                              players.reconnect_deadline_at is null or
                              players.reconnect_deadline_at > now()
                          )
                      )
                  )
                  and players.final_result is null
                returning players.match_id, players.seat
            )
            select
                (select count(*) from recoverable_matches),
                (select count(*) from closed_connections),
                (select count(*) from disconnected_players);
            """;
        command.Parameters.AddWithValue("grace_seconds", safeGraceSeconds);

        var result = new PvpMatchmakingStartupRecoveryResult(0, 0, 0);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                result = new PvpMatchmakingStartupRecoveryResult(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PvpMatchmakingReconnectStatus?> GetReconnectStatusForAccountAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        var parsedAccountId = ParseRequiredGuid(accountId, nameof(accountId));

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select matches.match_id,
                   players.runtime_player_id,
                   players.seat,
                   players.reconnect_deadline_at
            from pvp_match_players players
            join pvp_matches matches on matches.id = players.match_id
            where players.account_id = @account_id
              and players.player_status = 'disconnected'
              and players.reconnect_deadline_at is not null
              and players.reconnect_deadline_at > now()
              and matches.match_status = 'reconnect_grace'
            order by players.reconnect_deadline_at
            limit 1;
            """;
        command.Parameters.AddWithValue("account_id", parsedAccountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var matchId = reader.GetString(0);
        var runtimePlayerId = reader.GetString(1);
        var onlineSeatId = reader.GetString(2);
        var reconnectDeadlineUtc = reader.GetFieldValue<DateTimeOffset>(3).ToUniversalTime();
        var remainingSeconds = Math.Max(
            0,
            (int)Math.Ceiling((reconnectDeadlineUtc - DateTimeOffset.UtcNow).TotalSeconds));

        return remainingSeconds <= 0
            ? null
            : new PvpMatchmakingReconnectStatus(
                matchId,
                runtimePlayerId,
                onlineSeatId,
                remainingSeconds,
                reconnectDeadlineUtc);
    }

    public async Task<PvpMatchmakingMatchAssignment> ResolveMatchForQueueAsync(
        PvpMatchmakingQueueRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var accountId = ParseRequiredGuid(request.AccountId, nameof(request.AccountId));
        var runId = ParseOptionalGuid(request.RunId);
        var deckId = ParseOptionalGuid(request.DeckId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existingMatch = await FindWaitingMatchForAccountAsync(
            connection,
            transaction,
            accountId,
            cancellationToken);
        if (existingMatch != null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existingMatch;
        }

        await UpsertQueuedRequestAsync(
            connection,
            transaction,
            accountId,
            runId,
            deckId,
            request.ClientVersion,
            cancellationToken);

        var opponentMatch = await FindWaitingOpponentMatchAsync(
            connection,
            transaction,
            accountId,
            cancellationToken);
        if (opponentMatch != null)
        {
            await MarkQueueMatchedAsync(
                connection,
                transaction,
                accountId,
                runId,
                deckId,
                opponentMatch.MatchDbId,
                request.ClientVersion,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new PvpMatchmakingMatchAssignment(
                opponentMatch.MatchId,
                CreatedMatch: false,
                MatchedOpponent: true);
        }

        var newMatchId = CreateMatchId();
        var newMatchDbId = await UpsertMatchAsync(
            connection,
            transaction,
            newMatchId,
            request.ClientVersion,
            cancellationToken);
        await MarkQueueMatchedAsync(
            connection,
            transaction,
            accountId,
            runId,
            deckId,
            newMatchDbId,
            request.ClientVersion,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new PvpMatchmakingMatchAssignment(
            newMatchId,
            CreatedMatch: true,
            MatchedOpponent: false);
    }

    public async Task RecordConnectionJoinedAsync(
        PvpMatchmakingConnectionRecord record,
        CancellationToken cancellationToken)
    {
        if (record == null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var accountId = ParseRequiredGuid(record.AccountId, nameof(record.AccountId));
        var runId = ParseOptionalGuid(record.RunId);
        var deckId = ParseOptionalGuid(record.DeckId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var matchDbId = await UpsertMatchAsync(
            connection,
            transaction,
            record.MatchId,
            record.ClientVersion,
            cancellationToken);
        await MarkQueueMatchedAsync(
            connection,
            transaction,
            accountId,
            runId,
            deckId,
            matchDbId,
            record.ClientVersion,
            cancellationToken);
        await UpsertMatchPlayerAsync(
            connection,
            transaction,
            matchDbId,
            record.Seat,
            accountId,
            runId,
            deckId,
            record.RuntimePlayerId,
            cancellationToken);
        await InsertConnectionAsync(
            connection,
            transaction,
            matchDbId,
            record.Seat,
            accountId,
            record.ConnectionId,
            record.RemoteEndpoint,
            record.UserAgent,
            cancellationToken);
        await MarkReconnectGraceCancelledIfReadyAsync(
            connection,
            transaction,
            matchDbId,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordBattleStartedAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update pvp_matches
            set match_status = 'active',
                started_at = coalesce(started_at, now()),
                updated_at = now()
            where match_id = @match_id
              and match_status in ('waiting', 'reconnect_grace', 'active');
            """;
        command.Parameters.AddWithValue("match_id", matchId.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordConnectionDisconnectedAsync(
        PvpMatchmakingDisconnectRecord record,
        CancellationToken cancellationToken)
    {
        if (record == null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var accountId = ParseRequiredGuid(record.AccountId, nameof(record.AccountId));

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var matchDbId = await FindMatchDbIdAsync(
            connection,
            transaction,
            record.MatchId,
            cancellationToken);
        if (matchDbId == null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await CloseConnectionAsync(
            connection,
            transaction,
            matchDbId.Value,
            accountId,
            record.ConnectionId,
            cancellationToken);

        if (record.ReconnectDeadlineUtc.HasValue)
        {
            await MarkPlayerDisconnectedAsync(
                connection,
                transaction,
                matchDbId.Value,
                record.Seat,
                accountId,
                record.ReconnectDeadlineUtc.Value,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordMatchCompletedAsync(
        string matchId,
        string winnerSeat,
        string endedReason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matchId) ||
            string.IsNullOrWhiteSpace(winnerSeat))
        {
            return;
        }

        var normalizedReason = NormalizeEndedReason(endedReason);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with winner as (
                select match_id, account_id
                from pvp_match_players
                where match_id = (select id from pvp_matches where match_id = @match_id)
                  and seat = @winner_seat
                limit 1
            ),
            update_players as (
                update pvp_match_players players
                set player_status = case when players.seat = @winner_seat then 'winner' else 'loser' end,
                    final_result = case when players.seat = @winner_seat then 'win' else 'loss' end,
                    reconnect_deadline_at = null,
                    disconnected_at = null,
                    last_seen_at = now()
                where players.match_id = (select match_id from winner)
                returning players.match_id
            ),
            closed_connections as (
                update pvp_match_connections connections
                set connection_status = 'closed',
                    disconnected_at = coalesce(connections.disconnected_at, now()),
                    last_seen_at = now()
                where connections.match_id = (select match_id from winner)
                  and connections.connection_status = 'active'
                returning connections.id
            )
            update pvp_matches matches
            set match_status = 'completed',
                winner_account_id = (select account_id from winner),
                ended_reason = @ended_reason,
                completed_at = coalesce(completed_at, now()),
                updated_at = now()
            where matches.id = (select match_id from winner);
            """;
        command.Parameters.AddWithValue("match_id", matchId.Trim());
        command.Parameters.AddWithValue("winner_seat", winnerSeat.Trim());
        command.Parameters.AddWithValue("ended_reason", normalizedReason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> UpsertMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string matchId,
        string? clientVersion,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into pvp_matches(match_id, match_status, client_version, updated_at)
            values (@match_id, 'waiting', @client_version, now())
            on conflict (match_id) do update
            set client_version = coalesce(excluded.client_version, pvp_matches.client_version),
                updated_at = now()
            returning id;
            """;
        command.Parameters.AddWithValue("match_id", NormalizeRequiredText(matchId, nameof(matchId)));
        command.Parameters.AddWithValue("client_version", ToDbText(clientVersion));
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is Guid id
            ? id
            : throw new InvalidOperationException("Could not resolve PvP match id.");
    }

    private static async Task<PvpMatchmakingMatchAssignment?> FindWaitingMatchForAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select matches.match_id,
                   (
                       select count(*)
                       from pvp_match_players players
                       where players.match_id = matches.id
                   ) as player_count
            from pvp_matches matches
            join pvp_match_players own_player on own_player.match_id = matches.id
            where own_player.account_id = @account_id
              and matches.match_status = 'waiting'
            order by matches.created_at desc
            for update of matches skip locked
            limit 1;
            """;
        command.Parameters.AddWithValue("account_id", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var matchId = reader.GetString(0);
        var playerCount = reader.GetInt64(1);
        return new PvpMatchmakingMatchAssignment(
            matchId,
            CreatedMatch: false,
            MatchedOpponent: playerCount >= 2);
    }

    private static async Task UpsertQueuedRequestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid? runId,
        Guid? deckId,
        string? clientVersion,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into pvp_match_queue(
                account_id,
                run_id,
                deck_id,
                queue_status,
                client_version,
                requested_at,
                updated_at)
            values (
                @account_id,
                @run_id,
                @deck_id,
                'queued',
                @client_version,
                now(),
                now())
            on conflict (account_id) where queue_status = 'queued' do update
            set run_id = excluded.run_id,
                deck_id = excluded.deck_id,
                client_version = coalesce(excluded.client_version, pvp_match_queue.client_version),
                requested_at = now(),
                updated_at = now(),
                expires_at = null;
            """;
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("run_id", ToDbGuid(runId));
        command.Parameters.AddWithValue("deck_id", ToDbGuid(deckId));
        command.Parameters.AddWithValue("client_version", ToDbText(clientVersion));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PvpMatchmakingWaitingMatch?> FindWaitingOpponentMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select matches.id, matches.match_id
            from pvp_matches matches
            where matches.match_status = 'waiting'
              and exists (
                  select 1
                  from pvp_match_players players
                  where players.match_id = matches.id
                    and players.account_id <> @account_id
              )
              and not exists (
                  select 1
                  from pvp_match_players players
                  where players.match_id = matches.id
                    and players.account_id = @account_id
              )
              and (
                  select count(*)
                  from pvp_match_players players
                  where players.match_id = matches.id
              ) = 1
            order by matches.created_at
            for update of matches skip locked
            limit 1;
            """;
        command.Parameters.AddWithValue("account_id", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PvpMatchmakingWaitingMatch(
            reader.GetGuid(0),
            reader.GetString(1));
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

    private static async Task MarkQueueMatchedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid? runId,
        Guid? deckId,
        Guid matchDbId,
        string? clientVersion,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update pvp_match_queue
            set queue_status = 'matched',
                matched_match_id = @match_id,
                matched_at = coalesce(matched_at, now()),
                updated_at = now()
            where account_id = @account_id
              and queue_status = 'queued';

            insert into pvp_match_queue(
                account_id,
                run_id,
                deck_id,
                queue_status,
                client_version,
                matched_match_id,
                matched_at,
                requested_at,
                updated_at)
            select
                @account_id,
                @run_id,
                @deck_id,
                'matched',
                @client_version,
                @match_id,
                now(),
                now(),
                now()
            where not exists (
                select 1
                from pvp_match_queue
                where account_id = @account_id
                  and matched_match_id = @match_id
            );
            """;
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("run_id", ToDbGuid(runId));
        command.Parameters.AddWithValue("deck_id", ToDbGuid(deckId));
        command.Parameters.AddWithValue("match_id", matchDbId);
        command.Parameters.AddWithValue("client_version", ToDbText(clientVersion));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertMatchPlayerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid matchDbId,
        string seat,
        Guid accountId,
        Guid? runId,
        Guid? deckId,
        string runtimePlayerId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into pvp_match_players(
                match_id,
                seat,
                account_id,
                run_id,
                deck_id,
                runtime_player_id,
                player_status,
                joined_at,
                last_seen_at)
            values (
                @match_id,
                @seat,
                @account_id,
                @run_id,
                @deck_id,
                @runtime_player_id,
                'active',
                now(),
                now())
            on conflict (match_id, seat) do update
            set account_id = excluded.account_id,
                run_id = coalesce(excluded.run_id, pvp_match_players.run_id),
                deck_id = coalesce(excluded.deck_id, pvp_match_players.deck_id),
                runtime_player_id = excluded.runtime_player_id,
                player_status = 'active',
                last_seen_at = now(),
                disconnected_at = null,
                reconnect_deadline_at = null,
                session_version = pvp_match_players.session_version + case
                    when pvp_match_players.player_status = 'disconnected' then 1
                    else 0
                end;
            """;
        command.Parameters.AddWithValue("match_id", matchDbId);
        command.Parameters.AddWithValue("seat", NormalizeSeat(seat));
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("run_id", ToDbGuid(runId));
        command.Parameters.AddWithValue("deck_id", ToDbGuid(deckId));
        command.Parameters.AddWithValue("runtime_player_id", NormalizeRuntimePlayerId(runtimePlayerId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertConnectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid matchDbId,
        string seat,
        Guid accountId,
        string connectionId,
        string? remoteEndpoint,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update pvp_match_connections
            set connection_status = 'replaced',
                disconnected_at = coalesce(disconnected_at, now()),
                last_seen_at = now()
            where match_id = @match_id
              and account_id = @account_id
              and connection_status = 'active';

            insert into pvp_match_connections(
                match_id,
                seat,
                account_id,
                connection_id,
                connection_status,
                connected_at,
                last_seen_at,
                remote_endpoint,
                user_agent)
            values (
                @match_id,
                @seat,
                @account_id,
                @connection_id,
                'active',
                now(),
                now(),
                @remote_endpoint,
                @user_agent);
            """;
        command.Parameters.AddWithValue("match_id", matchDbId);
        command.Parameters.AddWithValue("seat", NormalizeSeat(seat));
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("connection_id", NormalizeRequiredText(connectionId, nameof(connectionId)));
        command.Parameters.AddWithValue("remote_endpoint", ToDbText(remoteEndpoint));
        command.Parameters.AddWithValue("user_agent", ToDbText(userAgent));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkReconnectGraceCancelledIfReadyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid matchDbId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update pvp_matches matches
            set match_status = 'active',
                updated_at = now()
            where matches.id = @match_id
              and matches.match_status = 'reconnect_grace'
              and not exists (
                  select 1
                  from pvp_match_players players
                  where players.match_id = matches.id
                    and players.player_status = 'disconnected'
              );
            """;
        command.Parameters.AddWithValue("match_id", matchDbId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CloseConnectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid matchDbId,
        Guid accountId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update pvp_match_connections
            set connection_status = 'closed',
                disconnected_at = coalesce(disconnected_at, now()),
                last_seen_at = now()
            where match_id = @match_id
              and account_id = @account_id
              and connection_id = @connection_id
              and connection_status = 'active';
            """;
        command.Parameters.AddWithValue("match_id", matchDbId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("connection_id", NormalizeRequiredText(connectionId, nameof(connectionId)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkPlayerDisconnectedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid matchDbId,
        string seat,
        Guid accountId,
        DateTimeOffset reconnectDeadlineUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update pvp_match_players
            set player_status = 'disconnected',
                disconnected_at = now(),
                reconnect_deadline_at = @reconnect_deadline_at,
                last_seen_at = now()
            where match_id = @match_id
              and seat = @seat
              and account_id = @account_id
              and player_status in ('active', 'disconnected');

            update pvp_matches
            set match_status = 'reconnect_grace',
                updated_at = now()
            where id = @match_id
              and match_status in ('active', 'reconnect_grace');
            """;
        command.Parameters.AddWithValue("match_id", matchDbId);
        command.Parameters.AddWithValue("seat", NormalizeSeat(seat));
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("reconnect_deadline_at", ToDbTimestamp(reconnectDeadlineUtc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeSeat(string value)
    {
        var normalized = NormalizeRequiredText(value, nameof(value));
        return normalized is "PlayerA" or "PlayerB"
            ? normalized
            : throw new ArgumentException($"Unsupported PvP seat '{value}'.", nameof(value));
    }

    private static string NormalizeRuntimePlayerId(string value)
    {
        var normalized = NormalizeRequiredText(value, nameof(value));
        return normalized is "Player" or "AI"
            ? normalized
            : throw new ArgumentException($"Unsupported runtime player id '{value}'.", nameof(value));
    }

    private static string NormalizeEndedReason(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "normal" : value.Trim();
        return normalized is "normal" or "forfeit" or "reconnect_timeout" or "server_cancel" or "error"
            ? normalized
            : "normal";
    }

    private static Guid ParseRequiredGuid(string value, string parameterName)
    {
        if (Guid.TryParse(value, out var id))
        {
            return id;
        }

        throw new ArgumentException($"{parameterName} must be a valid UUID.", parameterName);
    }

    private static Guid? ParseOptionalGuid(string value)
    {
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static object ToDbGuid(Guid? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object ToDbText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static object ToDbTimestamp(DateTimeOffset? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static string CreateMatchId()
    {
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        return $"pvp-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{suffix}";
    }
}

public sealed record PvpMatchmakingQueueRequest(
    string AccountId,
    string RunId,
    string DeckId,
    string ClientVersion);

public sealed record PvpMatchmakingStartupRecoveryResult(
    long MatchCount,
    long ClosedConnectionCount,
    long ReconnectablePlayerCount);

public sealed record PvpMatchmakingReconnectStatus(
    string MatchId,
    string RuntimePlayerId,
    string OnlineSeatId,
    int RemainingSeconds,
    DateTimeOffset ReconnectDeadlineUtc);

public sealed record PvpMatchmakingMatchAssignment(
    string MatchId,
    bool CreatedMatch,
    bool MatchedOpponent);

internal sealed record PvpMatchmakingWaitingMatch(
    Guid MatchDbId,
    string MatchId);

public sealed record PvpMatchmakingConnectionRecord(
    string MatchId,
    string AccountId,
    string ConnectionId,
    string Seat,
    string RuntimePlayerId,
    string RunId,
    string DeckId,
    string ClientVersion,
    string RemoteEndpoint,
    string UserAgent);

public sealed record PvpMatchmakingDisconnectRecord(
    string MatchId,
    string AccountId,
    string ConnectionId,
    string Seat,
    DateTimeOffset? ReconnectDeadlineUtc);
