using Project333.PvpServer.BattleSessions;

static class PveReconnectConcurrencyChecks
{
    public static async Task Run(Action<string> pass, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        var manager = new BattleSessionManager();
        var account = Guid.NewGuid().ToString();

        using var first = await Acquire(manager, account, token);
        var secondWait = Acquire(manager, account, token);
        Require(!secondWait.IsCompleted, "A second join for the same account must wait while its first join owns the gate.");
        using (await Acquire(manager, Guid.NewGuid().ToString(), token))
            Require(!secondWait.IsCompleted, "Another account must enter without releasing or bypassing the first account's gate.");

        using (var canceledWait = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            var canceled = Acquire(manager, account, canceledWait.Token);
            Require(!canceled.IsCompleted, "Canceled-waiter fixture must actually wait behind the owner.");
            canceledWait.Cancel();
            await RequireCanceled(canceled, canceledWait.Token);
        }
        Require(!secondWait.IsCompleted, "Canceling a queued join must not release another join's active gate.");
        first.Dispose();
        using var second = await secondWait;
        first.Dispose();
        var thirdWait = Acquire(manager, account, token);
        Require(!thirdWait.IsCompleted, "Disposing an old lease twice must not admit a third join while the second owns the gate.");
        second.Dispose();
        using var third = await thirdWait;
        second.Dispose();
        var fourthWait = Acquire(manager, account, token);
        Require(!fourthWait.IsCompleted, "Repeated disposal must stay harmless across several owner handoffs.");
        third.Dispose();
        using (await fourthWait) { }
        using (await Acquire(manager, account, token)) { }
        pass("account join admission serializes the same account, permits other accounts, safely cancels waiters and tolerates repeated lease disposal");

        var active = 0;
        var completed = 0;
        async Task JoinOnce()
        {
            using var lease = await Acquire(manager, account, token);
            Require(Interlocked.Increment(ref active) == 1, "Concurrent account joins must never overlap their protected participant lookup/registration work.");
            try { await Task.Yield(); }
            finally { Interlocked.Decrement(ref active); }
            Interlocked.Increment(ref completed);
        }
        for (var round = 0; round < 3; round++)
            await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => JoinOnce()));
        Require(completed == 72 && active == 0, "All queued joins must complete after gate removal and reacquisition without leaking a permit.");
        pass("repeated concurrent account joins retain one owner through 72 queued admissions and gate reuse");

        var session = CreateStartedPveSession();
        Require(session.TryBeginServerAiLoop(), "A PvE session must allow its first AI orchestration loop.");
        Require(!session.TryBeginServerAiLoop(), "A reconnect kick must not start another AI loop while the old loop is broadcasting or persisting.");
        session.EndServerAiLoop();
        var entered = 0;
        Parallel.For(0, 24, _ => { if (session.TryBeginServerAiLoop()) Interlocked.Increment(ref entered); });
        Require(entered == 1, "Simultaneous AI kicks must elect exactly one loop owner.");
        session.EndServerAiLoop();
        Require(session.TryBeginServerAiLoop(), "The next AI turn must enter after the previous loop exits.");
        session.CancelForServerShutdown();
        session.EndServerAiLoop();
        Require(!session.TryBeginServerAiLoop() && session.GetFinalResult() == null,
            "Releasing an AI loop after shutdown must neither restart AI nor turn server cancellation into a battle result.");
        pass("PvE AI orchestration rejects duplicate/concurrent reconnect kicks, admits the next loop after release and stays disabled after server shutdown");
    }

    static async Task<IDisposable> Acquire(BattleSessionManager manager, string account, CancellationToken ct) =>
        await manager.AcquireAccountJoinAsync(account, ct);

    static async Task RequireCanceled(Task<IDisposable> pending, CancellationToken expectedToken)
    {
        try { using var unexpectedLease = await pending; }
        catch (OperationCanceledException) when (expectedToken.IsCancellationRequested) { return; }
        throw new InvalidOperationException("A canceled queued join must finish with cancellation instead of acquiring the gate.");
    }

    static BattleSession CreateStartedPveSession()
    {
        var session = new BattleSession("pve-loop-concurrency-" + Guid.NewGuid().ToString("N"), true);
        var owner = new BattleClientConnection(Guid.NewGuid().ToString("N"), new OfflineSocket()) {
            AccountId = Guid.NewGuid().ToString(), PlayerToken = Guid.NewGuid().ToString("N"),
            RunId = Guid.NewGuid().ToString(), DeckId = Guid.NewGuid().ToString() };
        owner.PlayerDeckCardIds.AddRange(Enumerable.Repeat("Goblin", 33));
        Require(session.TryAddConnection(owner, out var error) && session.TryStartBattleIfReady(out error), error);
        return session;
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
