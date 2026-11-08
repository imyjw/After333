using System.Collections.Concurrent;
using System.Threading.Channels;
using Project333.PvpServer.BattleSessions;
using Project333.PvpServer.Matchmaking;

static class AiFallbackTimerChecks
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);
    public static async Task Run(Action<string> pass, CancellationToken ct)
    {
        var manager = new BattleSessionManager();
        var old = manager.GetOrCreate("reused-room", false);
        var oldClient = Add(old);
        var delay = new ManualDelay();
        var started = new TaskCompletionSource<BattleSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new PvpAiFallbackScheduler(Wait, (room, _) =>
        {
            foreach (var client in room.SnapshotConnections()) room.RemoveConnection(client.ConnectionId);
            started.TrySetResult(room);
            return Task.CompletedTask;
        }, ct, delay.Delay);
        scheduler.Schedule(old);
        var first = delay.Take();
        Require(first.Duration == Wait, "First attempt must retain the20s server wait.");
        old.RemoveConnection(oldClient.ConnectionId);
        manager.RemoveIfEmpty(old);
        var current = manager.GetOrCreate(old.MatchId, false);
        Require(!ReferenceEquals(old, current), "Recreate the actual room with the same durable match ID.");
        Add(current);
        // Reproduce the previous guard exactly: string-key ownership suppresses this new room.
        var legacy = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        Require(legacy.TryAdd(old.MatchId, 0) && !legacy.TryAdd(current.MatchId, 0), "Legacy collision reproduction.");
        scheduler.Schedule(current);
        var second = delay.Take(); // Old implementation has no second timer.
        first.Release();
        await Empty(scheduler, 1, ct);
        second.Release();
        Require(ReferenceEquals(await started.Task.WaitAsync(TimeSpan.FromSeconds(2), ct), current), "Only the replacement room gets an AI attempt.");
        await Empty(scheduler, 0, ct);
        pass("same-ID recreated room receives its own timer; old cleanup cannot erase it (legacy collision reproduced)");

        var room = new BattleSession("dedup", false); Add(room);
        delay = new ManualDelay();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        scheduler = new PvpAiFallbackScheduler(Wait, async (session, _) =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                entered.SetResult();
                await finishAttempt.Task;
                throw new InvalidOperationException("synthetic transient database failure");
            }
            foreach (var client in session.SnapshotConnections()) session.RemoveConnection(client.ConnectionId);
            completed.SetResult();
        }, ct, delay.Delay);
        Parallel.For(0, 200, _ => scheduler.Schedule(room));
        first = delay.Take(); Require(!delay.HasPending, "200 joins must reserve one initial timer.");
        first.Release(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Parallel.For(0, 200, _ => scheduler.Schedule(room));
        Require(!delay.HasPending && attempts == 1, "A pending attempt cannot be duplicated.");
        finishAttempt.SetResult();
        second = await delay.Next(ct);
        Require(second.Duration == TimeSpan.FromSeconds(1), "A transient failure must not restart the20s wait.");
        second.Release(); await completed.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);
        await Empty(scheduler, 0, ct);
        Require(attempts == 2, "Retry exactly once without overlapping attempts.");
        pass("200 concurrent schedules and200 in-flight schedules deduplicate; transient failure retries after1s instead of20s");

        room = new BattleSession("join-during-attempt", false);
        var clientBefore = Add(room); delay = new ManualDelay(); attempts = 0;
        completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PvpAiFallbackScheduler? joiningScheduler = null;
        joiningScheduler = new PvpAiFallbackScheduler(Wait, (session, _) =>
        {
            if (++attempts == 1)
            {
                session.RemoveConnection(clientBefore.ConnectionId);
                Add(session);
                joiningScheduler!.Schedule(session); // Join races an already owned timer.
            }
            else
            {
                foreach (var client in session.SnapshotConnections()) session.RemoveConnection(client.ConnectionId);
                completed.SetResult();
            }
            return Task.CompletedTask;
        }, ct, delay.Delay);
        joiningScheduler.Schedule(room); delay.Take().Release();
        second = await delay.Next(ct);
        Require(second.Duration == TimeSpan.FromSeconds(1), "Joined room remains watched after an in-flight attempt.");
        second.Release(); await completed.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);
        await Empty(joiningScheduler, 0, ct);
        pass("remove/rejoin during a pending attempt keeps the replacement connection watched");

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        delay = new ManualDelay(); room = new BattleSession("shutdown", false); Add(room); attempts = 0;
        scheduler = new PvpAiFallbackScheduler(Wait, (_, _) => { attempts++; return Task.CompletedTask; }, stop.Token, delay.Delay);
        scheduler.Schedule(room); first = delay.Take(); stop.Cancel();
        await Empty(scheduler, 0, ct); first.Release();
        scheduler.Schedule(room); Require(!delay.HasPending && attempts == 0, "Shutdown cannot rearm or promote.");
        pass("shutdown cancels the timer and prevents cleanup from rearming it");

        delay = new ManualDelay();
        scheduler = new PvpAiFallbackScheduler(Wait, (_, _) => throw new Exception("must not run"), ct, delay.Delay);
        room = new BattleSession("empty", false); scheduler.Schedule(room);
        var ai = new BattleSession("already-ai", true); Add(ai); scheduler.Schedule(ai);
        Require(!delay.HasPending, "Empty rooms and AI battles must not enter the PvP fallback timer.");
        pass("empty rooms and already-AI battles are excluded");

        // Repeat the identity collision without wall-clock sleeps or PostgreSQL.
        for (var iteration = 0; iteration < 100; iteration++)
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
            delay = new ManualDelay();
            scheduler = new PvpAiFallbackScheduler(Wait, (_, _) => Task.CompletedTask, cancel.Token, delay.Delay);
            var a = new BattleSession("stress-same-id", false); Add(a);
            var b = new BattleSession("stress-same-id", false); Add(b);
            scheduler.Schedule(a); scheduler.Schedule(b);
            delay.Take(); delay.Take();
            cancel.Cancel(); await Empty(scheduler, 0, ct);
        }
        pass("100 deterministic same-ID replacement repetitions retain separate timer ownership");
    }

    private static BattleClientConnection Add(BattleSession session)
    {
        var client = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket())
            { AccountId = Guid.NewGuid().ToString(), PlayerToken = Guid.NewGuid().ToString("N") };
        Require(session.TryAddConnection(client, out var error), error);
        return client;
    }
    private static async Task Empty(PvpAiFallbackScheduler scheduler, int expected, CancellationToken ct)
    {
        var timers = typeof(PvpAiFallbackScheduler).GetField("_timers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(scheduler)!;
        var count = timers.GetType().GetProperty("Count")!;
        var until = DateTime.UtcNow.AddSeconds(2);
        while ((int)count.GetValue(timers)! != expected && DateTime.UtcNow < until) await Task.Delay(1, ct);
        Require((int)count.GetValue(timers)! == expected, "Timer ownership cleanup count mismatch.");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Gate
    {
        public TimeSpan Duration { get; }
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Gate(TimeSpan duration) => Duration = duration;
        public void Release() => Ready.TrySetResult();
    }
    private sealed class ManualDelay
    {
        private readonly Channel<Gate> _gates = Channel.CreateUnbounded<Gate>();
        public bool HasPending => _gates.Reader.TryPeek(out _);
        public Task Delay(TimeSpan duration, CancellationToken ct)
        {
            var gate = new Gate(duration); _gates.Writer.TryWrite(gate);
            return gate.Ready.Task.WaitAsync(ct);
        }
        public Gate Take() => _gates.Reader.TryRead(out var gate) ? gate : throw new InvalidOperationException("Expected a registered room timer; the new waiting room was stranded.");
        public async Task<Gate> Next(CancellationToken ct) => await _gates.Reader.ReadAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(2), ct);
    }
}
