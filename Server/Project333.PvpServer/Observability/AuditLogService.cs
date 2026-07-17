using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Project333.PvpServer.Observability;

public sealed class AuditLogService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private DateTime _lastRetentionCleanupUtcDate = DateTime.MinValue;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public AuditLogService(IConfiguration configuration)
    {
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        Enabled = ResolveEnabled(configuration);
        LogDirectory = ResolveLogDirectory(configuration);
        RetentionDays = ResolveRetentionDays(configuration);
    }

    public bool Enabled { get; }

    public string LogDirectory { get; }

    public int RetentionDays { get; }

    public async Task LogAsync(
        string eventType,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(eventType))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(LogDirectory);
            var filePath = Path.Combine(LogDirectory, $"audit_{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");
            var entry = new AuditLogEntry(DateTimeOffset.UtcNow, eventType, payload);
            var line = JsonSerializer.Serialize(entry, _jsonOptions);

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                CleanupExpiredLogsIfNeeded();

                await File.AppendAllTextAsync(
                    filePath,
                    line + Environment.NewLine,
                    Encoding.UTF8,
                    cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[audit] write failed: {ex.Message}");
        }
    }

    private static bool ResolveEnabled(IConfiguration configuration)
    {
        var value = configuration["PROJECT333_AUDIT_LOG_ENABLED"] ??
                    Environment.GetEnvironmentVariable("PROJECT333_AUDIT_LOG_ENABLED");
        return !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(value, "no", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveLogDirectory(IConfiguration configuration)
    {
        var configured = configuration["PROJECT333_AUDIT_LOG_DIR"] ??
                         Environment.GetEnvironmentVariable("PROJECT333_AUDIT_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        const string projectLogDirectory = @"C:\Project_333\Logs\Server\Audit";
        if (Directory.Exists(@"C:\Project_333"))
        {
            return projectLogDirectory;
        }

        return Path.Combine(AppContext.BaseDirectory, "Logs", "Audit");
    }

    private static int ResolveRetentionDays(IConfiguration configuration)
    {
        var value = configuration["PROJECT333_AUDIT_LOG_RETENTION_DAYS"] ??
                    Environment.GetEnvironmentVariable("PROJECT333_AUDIT_LOG_RETENTION_DAYS");
        return int.TryParse(value, out var parsed) ? parsed : 30;
    }

    private void CleanupExpiredLogsIfNeeded()
    {
        if (RetentionDays <= 0)
        {
            return;
        }

        var today = DateTime.UtcNow.Date;
        if (_lastRetentionCleanupUtcDate == today)
        {
            return;
        }

        _lastRetentionCleanupUtcDate = today;
        var cutoffUtc = DateTime.UtcNow.AddDays(-RetentionDays);
        foreach (var filePath in Directory.EnumerateFiles(LogDirectory, "audit_*.jsonl", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(filePath) < cutoffUtc)
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[audit] retention cleanup skipped {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }
    }

    private sealed record AuditLogEntry(
        DateTimeOffset TimeUtc,
        string EventType,
        object? Payload);
}
