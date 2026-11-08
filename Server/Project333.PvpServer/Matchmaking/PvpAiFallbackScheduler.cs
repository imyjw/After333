using System.Collections.Concurrent;
using Project333.PvpServer.BattleSessions;

namespace Project333.PvpServer.Matchmaking;

// Own timers by the actual room instance, not its reusable durable match ID.
// The DB promotion still enforces each live connection's minimum waiting time.
public sealed class PvpAiFallbackScheduler
{
    private readonly ConcurrentDictionary<BattleSession, byte> _timers = new();
    private readonly TimeSpan _minimumWait;
    private readonly Func<BattleSession, CancellationToken, Task> _attempt;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly CancellationToken _stopping;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    public PvpAiFallbackScheduler(TimeSpan minimumWait,
        Func<BattleSession, CancellationToken, Task> attempt, CancellationToken stopping,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        if (minimumWait <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumWait));
        _minimumWait = minimumWait;
        _attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
        _stopping = stopping;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    private bool IsWaiting(BattleSession session) => !_stopping.IsCancellationRequested &&
        !session.IsBattleStarted && !session.UseServerAiOpponent && session.AiChallengeAttemptId == null;

    public void Schedule(BattleSession session) => Schedule(session, _minimumWait);

    private void Schedule(BattleSession session, TimeSpan initialDelay)
    {
        if (!IsWaiting(session) || session.ConnectionCount == 0 || !_timers.TryAdd(session, 0)) return;
        _ = RunAsync(session, initialDelay);
    }

    private async Task RunAsync(BattleSession session, TimeSpan initialDelay)
    {
        try
        {
            await _delay(initialDelay, _stopping);
            var failures = 0;
            while (IsWaiting(session) && session.ConnectionCount > 0)
            {
                try
                {
                    await _attempt(session, _stopping);
                    failures = 0;
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    if (++failures == 1 || failures % 10 == 0)
                        Console.WriteLine($"[match:{session.MatchId}] AI fallback failed: {ex.GetType().Name}; retrying.");
                }
                if (IsWaiting(session) && session.ConnectionCount > 0)
                    await _delay(RetryInterval, _stopping);
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        finally
        {
            _timers.TryRemove(session, out _);
            // A join can call Schedule while this timer is between its final
            // connection-count check and removal. Recheck after releasing ownership.
            // A competing Schedule is harmless: TryAdd admits exactly one owner.
            if (IsWaiting(session) && session.ConnectionCount > 0)
                Schedule(session, RetryInterval);
        }
    }
}
