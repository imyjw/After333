using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Project333.PvpServer.Persistence.Db;

namespace Project333.PvpServer.BattleResults;

// Local durable handoff bridges a DB outage. The DB receipt makes a replay after
// commit-but-before-file-delete safe. Keep this directory across server releases.
public sealed class BattleResultOutbox : BackgroundService
{
    private readonly IBattleResultStore _store;
    private readonly ILogger<BattleResultOutbox> _logger;
    private readonly ConcurrentDictionary<Guid, BattleResult> _pending = new();
    private readonly SemaphoreSlim _deliveryGate = new(1, 1);
    private readonly object _journalGate = new();
    private readonly Dictionary<Guid, (int Attempts, DateTimeOffset Next)> _failures = new();
    public string DirectoryPath { get; }

    public BattleResultOutbox(IBattleResultStore store, IConfiguration configuration, ILogger<BattleResultOutbox> logger)
    {
        _store = store;
        _logger = logger;
        var configuredPath = configuration["PROJECT333_BATTLE_RESULT_OUTBOX_DIR"];
        var connection = new NpgsqlConnectionStringBuilder(configuration[DbConnectionFactory.ConnectionStringKey] ?? "");
        var databaseKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{connection.Host}:{connection.Port}/{connection.Database}/{connection.Username}")))[..16];
        DirectoryPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "After333", "Server", "BattleResults", databaseKey)
            : configuredPath);
        if (_store.IsConfigured) Directory.CreateDirectory(DirectoryPath);
    }

    public void Capture(BattleResult result)
    {
        if (!_store.IsConfigured) return;
        result = result.ValidatedCopy();
        var held = _pending.GetOrAdd(result.ResultId, result);
        if (held.ToJson() != result.ToJson())
            throw new InvalidOperationException("Cannot replace an already queued battle result.");
        // Retain the memory copy even if the disk write fails; the worker retries.
        // Throwing prevents a successful battle-end response without a durable handoff.
        WriteJournal(held);
    }

    private string JournalPath(Guid id) => Path.Combine(DirectoryPath, id.ToString("N") + ".json");

    private void WriteJournal(BattleResult result)
    {
        lock (_journalGate)
        {
            var path = JournalPath(result.ResultId);
            var json = result.ToJson();
            if (File.Exists(path))
            {
                var saved = JsonSerializer.Deserialize<BattleResult>(File.ReadAllText(path))?.ToJson();
                if (saved != json) throw new InvalidOperationException("Existing result journal differs from this result.");
                return;
            }
            var temporary = Path.Combine(DirectoryPath, result.ResultId.ToString("N") + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    4096, FileOptions.WriteThrough))
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }
                try { File.Move(temporary, path); }
                catch (IOException) when (File.Exists(path))
                {
                    if (JsonSerializer.Deserialize<BattleResult>(File.ReadAllText(path))?.ToJson() != json) throw;
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public async Task<bool> DeliverAsync(Guid resultId, CancellationToken cancellationToken)
    {
        if (!_store.IsConfigured) return false;
        await _deliveryGate.WaitAsync(cancellationToken);
        try
        {
            if (!_pending.TryGetValue(resultId, out var result)) return true;
            try
            {
                WriteJournal(result);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var applied = await _store.ApplyAsync(result, timeout.Token);
                lock (_journalGate) { File.Delete(JournalPath(resultId)); }
                _pending.TryRemove(resultId, out _);
                _failures.Remove(resultId);
                _logger.LogInformation("Battle result {ResultId} persisted (new application: {Applied}).", resultId, applied);
                return true;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                var attempts = _failures.TryGetValue(resultId, out var previous) ? previous.Attempts + 1 : 1;
                _failures[resultId] = (attempts, DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 5 * Math.Pow(2, Math.Min(attempts - 1, 4)))));
                _logger.LogError(ex, "Battle result {ResultId} remains pending; retry attempt {Attempt}.", resultId, attempts);
                return false;
            }
        }
        finally { _deliveryGate.Release(); }
    }

    public async Task RetryPendingAsync(CancellationToken cancellationToken)
    {
        if (!_store.IsConfigured) return;
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.json"))
        {
            try
            {
                var result = JsonSerializer.Deserialize<BattleResult>(File.ReadAllText(path))?.ValidatedCopy()
                    ?? throw new InvalidOperationException("Empty result journal.");
                if (Path.GetFileName(path) != result.ResultId.ToString("N") + ".json")
                    throw new InvalidOperationException("Result journal filename does not match its ID.");
                var held = _pending.GetOrAdd(result.ResultId, result);
                if (held.ToJson() != result.ToJson()) throw new InvalidOperationException("Conflicting pending result.");
            }
            catch (FileNotFoundException) { } // Another delivery may have just committed it.
            catch (Exception ex) { _logger.LogError(ex, "Result journal {File} could not be loaded; preserved for repair.", Path.GetFileName(path)); }
        }
        foreach (var id in _pending.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _deliveryGate.WaitAsync(cancellationToken);
            bool due;
            try { due = !_failures.TryGetValue(id, out var failure) || failure.Next <= DateTimeOffset.UtcNow; }
            finally { _deliveryGate.Release(); }
            if (due) await DeliverAsync(id, cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_store.IsConfigured) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try { await RetryPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Battle result replay failed; journals retained for the next retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
