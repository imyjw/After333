namespace Project333.PvpServer.BattleSessions;

public sealed class BattleSessionManager
{
    private readonly object _gate = new();
    private readonly Dictionary<string, BattleSession> _sessions = new(StringComparer.Ordinal);

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

    public BattleSession GetOrCreate(string matchId, bool useServerAiOpponent = true)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            matchId = "default";
        }

        lock (_gate)
        {
            if (_sessions.TryGetValue(matchId, out var session))
            {
                return session;
            }

            session = new BattleSession(matchId, useServerAiOpponent);
            _sessions.Add(matchId, session);
            return session;
        }
    }

    public BattleSession GetOrCreateOpenPvpSession(string reconnectIdentityKey)
    {
        lock (_gate)
        {
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
            var session = new BattleSession(matchId, useServerAiOpponent: false);
            _sessions.Add(matchId, session);
            return session;
        }
    }

    public BattleSession? FindConnectedPvpSession(string reconnectIdentityKey)
    {
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey))
        {
            return null;
        }

        lock (_gate)
        {
            return _sessions.Values
                .Where(session =>
                    !session.UseServerAiOpponent &&
                    !session.IsBattleEnded &&
                    session.HasConnectedIdentityKey(reconnectIdentityKey))
                .OrderBy(session => session.MatchId, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }

    public PendingReconnectStatusSnapshot? FindReconnectStatus(string reconnectIdentityKey)
    {
        if (string.IsNullOrWhiteSpace(reconnectIdentityKey))
        {
            return null;
        }

        lock (_gate)
        {
            return _sessions.Values
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
            if (session.ConnectionCount == 0 && !session.HasPendingReconnectReservations)
            {
                _sessions.Remove(session.MatchId);
            }
        }
    }
}

public sealed record BattleSessionSummary(string MatchId, int ConnectionCount, bool IsBattleStarted);
