using Npgsql;
using NpgsqlTypes;
using Project333.PvpServer.Persistence.Db;
using Project333.PvpServer.Challenges;
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

        if (result.AiChallengeAttemptId.HasValue)
        {
            if (!await CompleteAiChallengeAsync(connection, transaction, result, cancellationToken))
                return false;
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        await using (var challenge = new NpgsqlCommand(
            "select exists(select 1 from ai_challenge_attempts where match_id=@match);", connection, transaction))
        {
            challenge.Parameters.AddWithValue("match", result.MatchId);
            if (await challenge.ExecuteScalarAsync(cancellationToken) is true)
                throw new InvalidOperationException("A challenge result must include its authoritative attempt binding.");
        }

        // A canceled match cannot later be resurrected by a stale result delivery.
        // The receipt insert above is rolled back when this transaction is disposed.
        {
            await using var status = new NpgsqlCommand(
                "select match_status from pvp_matches where match_id=@match for update;", connection, transaction);
            status.Parameters.AddWithValue("match", result.MatchId);
            if (await status.ExecuteScalarAsync(cancellationToken) is string matchStatus && matchStatus == "canceled")
                return false;
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

        await CompletePvpMatchAsync(connection, transaction, result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> CompleteAiChallengeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        BattleResult result, CancellationToken cancellationToken)
    {
        var run = result.Runs.Single();
        var accountId = Guid.Parse(run.AccountId);
        var attemptId = result.AiChallengeAttemptId!.Value;
        // Use the same wallet-first ordering as all other account economy transactions.
        await using (var wallet = new NpgsqlCommand(
            "select account_id from user_wallets where account_id=@account for update;", connection, transaction))
        {
            wallet.Parameters.AddWithValue("account", accountId);
            if (await wallet.ExecuteScalarAsync(cancellationToken) is not Guid)
                throw new InvalidOperationException("The AI challenge wallet is missing.");
        }
        string status;
        await using (var attempt = new NpgsqlCommand("""
            select a.status from ai_challenge_attempts a
            join draft_runs r on r.id=a.run_id and r.account_id=a.account_id and r.completed_deck_id=a.deck_id
            join decks d on d.id=a.deck_id and d.account_id=a.account_id and d.source_run_id=a.run_id
            where a.id=@id and a.account_id=@account and a.match_id=@match and a.run_id=@run and a.deck_id=@deck
            for update of a;
            """, connection, transaction))
        {
            attempt.Parameters.AddWithValue("id", attemptId);
            attempt.Parameters.AddWithValue("account", accountId);
            attempt.Parameters.AddWithValue("match", result.MatchId);
            attempt.Parameters.AddWithValue("run", Guid.Parse(run.RunId));
            attempt.Parameters.AddWithValue("deck", Guid.Parse(run.DeckId));
            status = await attempt.ExecuteScalarAsync(cancellationToken) as string
                ?? throw new InvalidOperationException("The challenge attempt/account/match/run/deck binding is invalid.");
        }
        // A refunded, canceled attempt can never be revived by delayed result delivery.
        if (status == "canceled") return false;
        if (status != "active")
            throw new InvalidOperationException("Only a joined, unfinished AI challenge can record a result.");
        var reward = run.Won ? AiChallengeService.WinResourceGold : 0;
        await using (var settle = new NpgsqlCommand("""
            update ai_challenge_attempts set status='completed', result_id=@result, won=@won,
                resource_gold_awarded=@reward, ended_reason=@reason, completed_at=clock_timestamp()
            where id=@id;
            """, connection, transaction))
        {
            settle.Parameters.AddWithValue("id", attemptId);
            settle.Parameters.AddWithValue("result", result.ResultId);
            settle.Parameters.AddWithValue("won", run.Won);
            settle.Parameters.AddWithValue("reward", reward);
            settle.Parameters.AddWithValue("reason", result.EndedReason);
            await settle.ExecuteNonQueryAsync(cancellationToken);
        }
        if (reward > 0)
        {
            await using var credit = new NpgsqlCommand("""
                update user_wallets set resource_gold=resource_gold+@reward, updated_at=clock_timestamp()
                where account_id=@account;
                insert into account_transactions(account_id,transaction_type,resource_gold_delta,ticket_delta,source_table,source_id)
                values (@account,'reward',@reward,0,'ai_challenge_attempts',@id);
                """, connection, transaction);
            credit.Parameters.AddWithValue("account", accountId);
            credit.Parameters.AddWithValue("reward", reward);
            credit.Parameters.AddWithValue("id", attemptId);
            await credit.ExecuteNonQueryAsync(cancellationToken);
        }
        // Deliberately no draft_runs, battle_run_results, reward_grants, collection, or ticket updates.
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
            where match_id=@match and account_id=any(@participants);
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
        command.Parameters.AddWithValue("participants", NpgsqlDbType.Array | NpgsqlDbType.Uuid,
            result.Runs.Select(r => Guid.Parse(r.AccountId)).ToArray());
        command.Parameters.AddWithValue("draw", result.IsDraw);
        command.Parameters.AddWithValue("reason", result.EndedReason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
