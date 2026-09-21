using Project333.PvpServer.BattleResults;

namespace Project333.PvpServer.BattleSessions;

public sealed class BattleSessionManager
{
    private readonly BattleResultOutbox? _resultOutbox;
    public BattleSessionManager(BattleResultOutbox? resultOutbox = null) => _resultOutbox = resultOutbox;
    private readonly object _gate = new();
    private bool _serverShutdown;
    private readonly Dictionary<string, BattleSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<BattleSession, int> _pendingJoins = new();

    private sealed class AccountJoinGate
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int Users;
    }
    private readonly Dictionary<string, AccountJoinGate> _accountJoins = new(StringComparer.Ordinal);

    // Presence checks and async deck loading must not let two sockets create two battles for one account.
    public async Task<IDisposable> AcquireAccountJoinAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        AccountJoinGate gate;
        lock (_gate)
        {
            if (_serverShutdown) throw new InvalidOperationException("Server is shutting down.");
            if (!_accountJoins.TryGetValue(accountId, out gate!))
                _accountJoins.Add(accountId, gate = new AccountJoinGate());
            gate.Users++;
        }
        try { await gate.Semaphore.WaitAsync(cancellationToken); }
        catch { ReleaseAccountJoin(accountId, gate, acquired: false); throw; }
        return new BattleAccountJoinLease(() => ReleaseAccountJoin(accountId, gate, acquired: true));
    }

    private void ReleaseAccountJoin(string accountId, AccountJoinGate gate, bool acquired)
    {
        lock (_gate)
        {
            if (acquired) gate.Semaphore.Release();
            if (--gate.Users == 0)
            {
                _accountJoins.Remove(accountId);
                gate.Semaphore.Dispose();
            }
        }
    }
    public void CancelForServerShutdown()
    {
        lock (_gate)
        {
            _serverShutdown = true;
            foreach (var session in _sessions.Values) session.CancelForServerShutdown();
        }
    }
    public BattleSessionJoinLease AcquireForJoin(string matchId, bool useServerAiOpponent, Guid? aiChallengeAttemptId = null)
    {
        lock (_gate)
        {
            var session = GetOrCreate(matchId, useServerAiOpponent, aiChallengeAttemptId);
            _pendingJoins.TryGetValue(session, out var count);
            _pendingJoins[session] = count + 1;
            return new BattleSessionJoinLease(session, () => ReleaseJoin(session));
        }
    }

    private void ReleaseJoin(BattleSession session)
    {
        lock (_gate)
        {
            if (_pendingJoins[session] == 1) _pendingJoins.Remove(session);
            else _pendingJoins[session]--;
            RemoveIfEmpty(session);
        }
    }

    public IReadOnlyList<BattleSessionSummary> SnapshotSessions()
    {
        lock (_gate)
        {
            return _sessions.Values
                .Select(session => new BattleSessionSummary(session.MatchId, session.ConnectionCount, session.IsBattleStarted))
                .OrderBy(summary => summary.MatchId, StringComparer.Ordinal)
                .ToList();
        }
    }

    public bool TryGet(string matchId, out BattleSession? session)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            session = null;
            return false;
        }

        lock (_gate)
        {
            return _sessions.TryGetValue(matchId, out session);
        }
    }

    public bool HasReconnectableServerAiBattle(string matchId)
    {
        return TryGet(matchId, out var session) &&
               session != null &&
               session.UseServerAiOpponent &&
               !session.IsBattleEnded;
    }

    public BattleSession? FindReconnectableServerAiBattle(string matchId)
    {
        return HasReconnectableServerAiBattle(matchId) &&
               TryGet(matchId, out var session)
            ? session
            : null;
    }

    public BattleSession GetOrCreate(string matchId, bool useServerAiOpponent = true, Guid? aiChallengeAttemptId = null)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            matchId = "default";
        }

        lock (_gate)
        {
            if (_serverShutdown) throw new InvalidOperationException("Server is shutting down.");
            if (_sessions.TryGetValue(matchId, out var session))
            {
                return session;
            }

            session = new BattleSession(matchId, useServerAiOpponent, resultOutbox: _resultOutbox, aiChallengeAttemptId: aiChallengeAttemptId);
            _sessions.Add(matchId, session);
            return session;
        }
    }

    public BattleSession GetOrCreateOpenPvpSession(string reconnectIdentityKey)
    {
        lock (_gate)
        {
            if (_serverShutdown) throw new InvalidOperationException("Server is shutting down.");
            var reconnectSession = _sessions.Values
                .Where(session => session.CanReconnectIdentityKey(reconnectIdentityKey))
                .OrderBy(session => session.MatchId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (reconnectSession != null)
            {
                return reconnectSession;
            }

            var existingSession = _sessions.Values
                .Where(session =>
                    !session.UseServerAiOpponent &&
                    !session.IsBattleStarted &&
                    session.ConnectionCount < session.RequiredHumanConnections &&
                    !session.HasConnectedIdentityKey(reconnectIdentityKey))
                .OrderBy(session => session.MatchId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (existingSession != null)
            {
                return existingSession;
            }

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var matchId = $"pvp-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{suffix}";
            var session = new BattleSession(matchId, useServerAiOpponent: false, resultOutbox: _resultOutbox);
            _sessions.Add(matchId, session);
            return session;
        }
    }

    public BattleSession? FindConnectedPvpSession(string reconnectIdentityKey) => FindConnectedSession(reconnectIdentityKey, false);

    public BattleSession? FindConnectedSession(string reconnectIdentityKey, bool useServerAiOpponent)
    {
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey))
        {
            return null;
        }

        lock (_gate)
        {
            return _sessions.Values
                .Where(session =>
                    session.UseServerAiOpponent == useServerAiOpponent &&
                    !session.IsBattleEnded &&
                    session.HasConnectedIdentityKey(reconnectIdentityKey))
                .OrderBy(session => session.MatchId, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }

    public PendingReconnectStatusSnapshot? FindReconnectStatus(string reconnectIdentityKey, bool useServerAiOpponent = false)
    {
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey))
        {
            return null;
        }

        lock (_gate)
        {
            return _sessions.Values
                .Where(session => session.UseServerAiOpponent == useServerAiOpponent)
                .Select(session => session.TryGetReconnectStatus(reconnectIdentityKey, out var status)
                    ? status
                    : null)
                .Where(status => status != null)
                .OrderBy(status => status!.ReconnectDeadlineUtc)
                .FirstOrDefault();
        }
    }

    public void RemoveIfEmpty(BattleSession session)
    {
        if (session.ConnectionCount > 0 || session.HasPendingReconnectReservations)
        {
            return;
        }

        lock (_gate)
        {
            if (session.ConnectionCount == 0 && !session.HasPendingReconnectReservations &&
                !_pendingJoins.ContainsKey(session) &&
                _sessions.TryGetValue(session.MatchId, out var current) && ReferenceEquals(current, session))
            {
                _sessions.Remove(session.MatchId);
            }
        }
    }
}

public sealed record BattleSessionSummary(string MatchId, int ConnectionCount, bool IsBattleStarted);

public sealed class BattleSessionJoinLease : IDisposable
{
    private Action? _release;
    internal BattleSessionJoinLease(BattleSession session, Action release) { Session = session; _release = release; }
    public BattleSession Session { get; }
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}

public sealed class BattleAccountJoinLease : IDisposable
{
    private Action? _release;
    internal BattleAccountJoinLease(Action release) => _release = release;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}