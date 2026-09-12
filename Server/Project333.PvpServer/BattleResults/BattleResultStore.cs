using Npgsql;
using NpgsqlTypes;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Runs;

namespace Project333.PvpServer.BattleResults;

public interface IBattleResultStore
{
    bool IsConfigured { get; }
    Task<bool> ApplyAsync(BattleResult result, CancellationToken cancellationToken);
}

public sealed class BattleResultStore(DbConnectionFactory connectionFactory) : IBattleResultStore
{
    public bool IsConfigured => connectionFactory.IsConfigured;

    public async Task<bool> ApplyAsync(BattleResult result, CancellationToken cancellationToken)
    {
        result = result.ValidatedCopy();
        var payload = result.ToJson();
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var receipt = new NpgsqlCommand("""
            insert into battle_result_receipts(result_id, match_id, payload)
            values (@id, @match, @payload)
            on conflict (result_id) do nothing;
            """, connection, transaction))
        {
            receipt.Parameters.AddWithValue("id", result.ResultId);
            receipt.Parameters.AddWithValue("match", result.MatchId);
            receipt.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, payload);
            if (await receipt.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await using var existing = new NpgsqlCommand(
                    "select payload = @payload from battle_result_receipts where result_id = @id;", connection, transaction);
                existing.Parameters.AddWithValue("id", result.ResultId);
                existing.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, payload);
                if (await existing.ExecuteScalarAsync(cancellationToken) is not true)
                    throw new InvalidOperationException("A result ID cannot be reused with a different outcome or participant.");
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
        }

        // Stable ordering prevents opposing results on the same pair of runs from deadlocking.
        if (!result.IsDraw)
        foreach (var run in result.Runs.OrderBy(r => r.RunId, StringComparer.Ordinal))
        {
            await using var update = new NpgsqlCommand("""
                update draft_runs
                set wins = least(33, wins + @win),
                    losses = least(3, losses + @loss),
                    status = case when wins + @win >= 33 or losses + @loss >= 3 then 'completed' else 'in_progress' end,
                    ended_reason = case when wins + @win >= 33 then 'wins_33' when losses + @loss >= 3 then 'losses_3' else null end,
                    completed_at = case when wins + @win >= 33 or losses + @loss >= 3 then coalesce(completed_at, now()) else completed_at end,
                    updated_at = now()
                where id = @run and account_id = @account and completed_deck_id = @deck
                  and status in ('ready', 'in_progress');
                """, connection, transaction);
            update.Parameters.AddWithValue("run", Guid.Parse(run.RunId));
            update.Parameters.AddWithValue("account", Guid.Parse(run.AccountId));
            update.Parameters.AddWithValue("deck", Guid.Parse(run.DeckId));
            update.Parameters.AddWithValue("win", run.Won ? 1 : 0);
            update.Parameters.AddWithValue("loss", run.Won ? 0 : 1);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new RunServiceException("run_result_not_recorded",
                    "The result's bound account/run/deck is missing, mismatched or already completed.");
            await using var seatReceipt = new NpgsqlCommand("""
                insert into battle_run_results(result_id, seat, account_id, run_id, deck_id, won)
                values (@id, @seat, @account, @run, @deck, @won);
                """, connection, transaction);
            seatReceipt.Parameters.AddWithValue("id", result.ResultId);
            seatReceipt.Parameters.AddWithValue("seat", run.OnlineSeatId.ToString());
            seatReceipt.Parameters.AddWithValue("account", Guid.Parse(run.AccountId));
            seatReceipt.Parameters.AddWithValue("run", Guid.Parse(run.RunId));
            seatReceipt.Parameters.AddWithValue("deck", Guid.Parse(run.DeckId));
            seatReceipt.Parameters.AddWithValue("won", run.Won);
            await seatReceipt.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!result.UseServerAiOpponent)
            await CompletePvpMatchAsync(connection, transaction, result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task CompletePvpMatchAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        BattleResult result, CancellationToken cancellationToken)
    {
        // Custom/offline test rooms need not have matchmaking rows. If rows exist, their
        // participant bindings must agree before finalizing the match in this transaction.
        await using var find = new NpgsqlCommand("select id from pvp_matches where match_id=@match for update;", connection, transaction);
        find.Parameters.AddWithValue("match", result.MatchId);
        if (await find.ExecuteScalarAsync(cancellationToken) is not Guid matchId) return;
        foreach (var run in result.Runs)
        {
            await using var check = new NpgsqlCommand("""
                select count(*) from pvp_match_players where match_id=@match and seat=@seat
                and account_id=@account and run_id=@run and deck_id=@deck and runtime_player_id=@runtime;
                """, connection, transaction);
            check.Parameters.AddWithValue("match", matchId);
            check.Parameters.AddWithValue("seat", run.OnlineSeatId.ToString());
            check.Parameters.AddWithValue("runtime", run.SeatId.ToString());
            check.Parameters.AddWithValue("account", Guid.Parse(run.AccountId));
            check.Parameters.AddWithValue("run", Guid.Parse(run.RunId));
            check.Parameters.AddWithValue("deck", Guid.Parse(run.DeckId));
            if ((long)(await check.ExecuteScalarAsync(cancellationToken))! != 1)
                throw new InvalidOperationException("Persisted match participant differs from the server result.");
        }
        await using var command = new NpgsqlCommand("""
            update pvp_match_players
            set player_status = case when @draw then 'draw' when seat=@winner then 'winner' else 'loser' end,
                final_result = case when @draw then 'draw' when seat=@winner then 'win' else 'loss' end,
                reconnect_deadline_at=null, disconnected_at=null, last_seen_at=now()
            where match_id=@match;
            update pvp_match_connections
            set connection_status='closed', disconnected_at=coalesce(disconnected_at,now()), last_seen_at=now()
            where match_id=@match and connection_status='active';
            update pvp_matches
            set match_status='completed',
                winner_account_id=case when @draw then null else
                    (select account_id from pvp_match_players where match_id=@match and seat=@winner) end,
                ended_reason=@reason, completed_at=coalesce(completed_at,now()), updated_at=now()
            where id=@match;
            """, connection, transaction);
        command.Parameters.AddWithValue("match", matchId);
        command.Parameters.AddWithValue("winner", result.WinnerSeat.ToString());
        command.Parameters.AddWithValue("draw", result.IsDraw);
        command.Parameters.AddWithValue("reason", result.EndedReason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
