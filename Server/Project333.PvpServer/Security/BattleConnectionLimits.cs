using System.Globalization;

namespace Project333.PvpServer.Security;

public sealed record BattleConnectionLimitSettings(
    int AuthenticationTimeoutSeconds = 30,
    int MaxConnectionsPerAccount = 2,
    int MessageBurst = 30,
    int MessagesPerSecond = 10,
    int JoinBurst = 4,
    int JoinRefillSeconds = 5)
{
    public static BattleConnectionLimitSettings FromConfiguration(IConfiguration configuration) => new(
        ReadValue(configuration, "PROJECT333_BATTLE_AUTH_TIMEOUT_SECONDS", 30, 1, 300),
        ReadValue(configuration, "PROJECT333_BATTLE_ACCOUNT_CONNECTION_LIMIT", 2, 2, 8),
        ReadValue(configuration, "PROJECT333_BATTLE_MESSAGE_BURST", 30, 1, 1000),
        ReadValue(configuration, "PROJECT333_BATTLE_MESSAGES_PER_SECOND", 10, 1, 100),
        ReadValue(configuration, "PROJECT333_BATTLE_JOIN_BURST", 4, 1, 50),
        ReadValue(configuration, "PROJECT333_BATTLE_JOIN_REFILL_SECONDS", 5, 1, 60));

    // Invalid explicit configuration fails startup instead of silently disabling protection.
    private static int ReadValue(IConfiguration configuration, string key, int fallback, int min, int max)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            throw new InvalidOperationException($"{key} must be an integer between {min} and {max}.");
        return value;
    }
}

// One registry per server process. Only server-authenticated account IDs enter this map.
// Empty entries are removed; unauthenticated traffic cannot create account entries.
public sealed class BattleAccountConnections(int limit)
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, int> _counts = new();

    public int TrackedAccountCount { get { lock (_gate) return _counts.Count; } }

    internal bool TryAcquire(Guid accountId)
    {
        lock (_gate)
        {
            _counts.TryGetValue(accountId, out var count);
            if (count >= limit) return false;
            _counts[accountId] = count + 1;
            return true;
        }
    }

    internal void Release(Guid accountId)
    {
        lock (_gate)
        {
            if (!_counts.TryGetValue(accountId, out var count)) return;
            if (count == 1) _counts.Remove(accountId);
            else _counts[accountId] = count - 1;
        }
    }
}

public enum BattleAuthenticationAdmission { Accepted, IdentityMismatch, ConnectionLimit, Expired, Closed }

public sealed class BattleConnectionGuard : IDisposable
{
    private readonly object _gate = new();
    private readonly BattleAccountConnections _accounts;
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly TimeSpan _authenticationTimeout;
    private readonly Bucket _messages;
    private readonly Bucket _joins;
    private Guid? _accountId;
    private bool _expired;
    private bool _disposed;

    public BattleConnectionGuard(BattleConnectionLimitSettings settings, BattleAccountConnections accounts, TimeProvider? clock = null)
    {
        _accounts = accounts;
        _clock = clock ?? TimeProvider.System;
        _started = _clock.GetTimestamp();
        _authenticationTimeout = TimeSpan.FromSeconds(settings.AuthenticationTimeoutSeconds);
        _messages = new Bucket(settings.MessageBurst, settings.MessagesPerSecond, _started);
        _joins = new Bucket(settings.JoinBurst, 1d / settings.JoinRefillSeconds, _started);
    }

    // Called before JSON parsing/logging; invalid JSON, unknown commands and KeepAlive
    // all consume the same bounded budget. There is no unbounded delayed command queue.
    public bool TryAcceptMessage() { lock (_gate) return !_disposed && _messages.Take(_clock); }
    public bool TryAcceptJoin() { lock (_gate) return !_disposed && _joins.Take(_clock); }

    public BattleAuthenticationAdmission Authenticate(Guid serverAccountId)
    {
        if (serverAccountId == Guid.Empty) throw new ArgumentException("An authenticated account is required.", nameof(serverAccountId));
        lock (_gate)
        {
            if (_disposed) return BattleAuthenticationAdmission.Closed;
            if (_accountId.HasValue)
                return _accountId.Value == serverAccountId ? BattleAuthenticationAdmission.Accepted : BattleAuthenticationAdmission.IdentityMismatch;
            if (ExpireAuthenticationCore()) return BattleAuthenticationAdmission.Expired;
            if (!_accounts.TryAcquire(serverAccountId)) return BattleAuthenticationAdmission.ConnectionLimit;
            _accountId = serverAccountId;
            return BattleAuthenticationAdmission.Accepted;
        }
    }

    public bool ExpireAuthenticationIfDue()
    {
        lock (_gate) return !_disposed && ExpireAuthenticationCore();
    }

    private bool ExpireAuthenticationCore()
    {
        if (_accountId.HasValue) return false;
        _expired |= _clock.GetElapsedTime(_started) >= _authenticationTimeout;
        return _expired;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_accountId.HasValue) _accounts.Release(_accountId.Value);
        }
    }

    private sealed class Bucket(int capacity, double refillPerSecond, long started)
    {
        private double _tokens = capacity;
        private long _updated = started;

        public bool Take(TimeProvider clock)
        {
            var now = clock.GetTimestamp();
            _tokens = Math.Min(capacity, _tokens + Math.Max(0, clock.GetElapsedTime(_updated, now).TotalSeconds) * refillPerSecond);
            _updated = now;
            if (_tokens < 1) return false;
            _tokens -= 1;
            return true;
        }
    }
}
