using System.Collections.Concurrent;
using Project333.PvpServer.Auth;

namespace Project333.PvpServer.BattleSessions;

// Login families survive access-token refresh; transports do not outlive their family.
public sealed class BattleLoginSessionRegistry(GuestAuthService authentication)
{
    private readonly ConcurrentDictionary<string, Registration> _connections = new(StringComparer.Ordinal);

    public void Track(BattleClientConnection connection, Func<Task> disconnect)
    {
        if (!Guid.TryParse(connection.AccountId, out var accountId) || !connection.LoginFamilyId.HasValue)
            throw new InvalidOperationException("A battle connection needs an authenticated login family.");
        _connections.TryAdd(connection.ConnectionId,
            new Registration(connection, accountId, connection.LoginFamilyId.Value, disconnect));
    }

    public void Untrack(BattleClientConnection connection) => _connections.TryRemove(connection.ConnectionId, out _);

    public void NotifyLoginChanged(Guid accountId) => _ = ObserveLoginChangeAsync(accountId);

    private async Task ObserveLoginChangeAsync(Guid accountId)
    {
        try { await DisconnectStaleAsync(accountId, CancellationToken.None); }
        catch (Exception ex) { Console.WriteLine($"[auth] battle login notification failed: {ex.GetType().Name}"); }
    }

    public async Task DisconnectStaleAsync(Guid accountId, CancellationToken ct)
    {
        foreach (var entry in _connections.Values.Where(item => item.AccountId == accountId))
        {
            // Look up durable truth rather than trusting event ordering from simultaneous logins.
            if (!await authentication.IsLoginFamilyCurrentAsync(entry.AccountId, entry.FamilyId, ct))
                await entry.DisconnectAsync();
        }
    }

    public async Task<bool> EnsureCurrentAsync(BattleClientConnection connection, CancellationToken ct)
    {
        if (!_connections.TryGetValue(connection.ConnectionId, out var entry))
            throw new InvalidOperationException("Battle login was not registered.");
        if (connection.IsLoginReplaced || !await authentication.IsLoginFamilyCurrentAsync(entry.AccountId, entry.FamilyId, ct))
        {
            await entry.DisconnectAsync();
            return false;
        }
        return true;
    }

    public Task DisconnectAsync(BattleClientConnection connection) =>
        _connections.TryGetValue(connection.ConnectionId, out var entry) ? entry.DisconnectAsync() : Task.CompletedTask;

    private sealed class Registration(BattleClientConnection connection, Guid accountId, Guid familyId, Func<Task> disconnect)
    {
        private readonly object _gate = new();
        private Task? _disconnection;
        public Guid AccountId { get; } = accountId;
        public Guid FamilyId { get; } = familyId;
        public Task DisconnectAsync()
        {
            lock (_gate)
            {
                if (_disconnection != null) return _disconnection;
                connection.MarkLoginReplaced();
                return _disconnection = disconnect();
            }
        }
    }
}
