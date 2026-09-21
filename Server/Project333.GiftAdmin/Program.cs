using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Project333.PvpServer.Gifts;
using Project333.PvpServer.Persistence.Db;

// Deliberately has no listener, admin HTTP route, or client-shipped credential.
Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
try
{
    if (args.Length == 0 || args[0] is "help" or "--help")
    {
        Console.WriteLine("Use Tools/ManageGifts.ps1: Accounts, Prepare, Preview or Send. Preparing a request never sends it.");
        return 0;
    }
    var rawConnection = Environment.GetEnvironmentVariable(DbConnectionFactory.ConnectionStringKey);
    if (string.IsNullOrWhiteSpace(rawConnection))
        throw new GiftInboxException("db_not_configured", "Run ManageGifts.ps1 with the saved server settings.");
    var db = new NpgsqlConnectionStringBuilder(rawConnection);
    if (!string.Equals(db.Host, "localhost", StringComparison.OrdinalIgnoreCase) &&
        (!IPAddress.TryParse(db.Host, out var ip) || !IPAddress.IsLoopback(ip)))
        throw new GiftInboxException("local_database_required", "This administrator tool only connects to a local PostgreSQL database.");
    var canonicalHost = IPAddress.TryParse(db.Host, out var databaseAddress) ? databaseAddress.ToString() : db.Host!.ToLowerInvariant();
    var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{canonicalHost}:{db.Port}/{db.Database}/{db.Username}")));
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        [DbConnectionFactory.ConnectionStringKey] = rawConnection
    }).Build();
    var service = new GiftInboxService(new DbConnectionFactory(configuration));
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    object response;
    switch (args[0])
    {
        case "accounts":
        {
            var request = JsonSerializer.Deserialize<AccountSearch>(ReadInput(), json)
                ?? throw new GiftInboxException("invalid_request", "Search input is missing.");
            response = new { Accounts = await service.FindAccountsAsync(request.Search, 100, timeout.Token) };
            break;
        }
        case "prepare":
        {
            if (args.Length != 2) throw new GiftInboxException("request_file_required", "Specify the new request file path.");
            var request = JsonSerializer.Deserialize<GiftSendRequest>(ReadInput(), json)
                ?? throw new GiftInboxException("invalid_request", "Gift input is missing.");
            var preview = await service.PreviewAsync(request, timeout.Token);
            var path = Path.GetFullPath(args[1]);
            await WritePreparedFileAsync(path, new PreparedGift(1, identity, request), json, timeout.Token);
            response = new { RequestFile = path, Request = request, Preview = preview, Sent = false };
            break;
        }
        case "preview":
        case "send":
        {
            if (args.Length != 2) throw new GiftInboxException("request_file_required", "Specify a prepared request file.");
            var path = Path.GetFullPath(args[1]);
            if (new FileInfo(path).Length > 65536) throw new GiftInboxException("request_too_large", "Request file is too large.");
            var prepared = JsonSerializer.Deserialize<PreparedGift>(await File.ReadAllTextAsync(path, timeout.Token), json);
            if (prepared == null || prepared.Version != 1 || prepared.Request == null)
                throw new GiftInboxException("invalid_request_file", "The prepared gift file is invalid.");
            if (!string.Equals(prepared.DatabaseIdentity, identity, StringComparison.Ordinal))
                throw new GiftInboxException("database_mismatch", "This request was prepared for a different database.");
            if (args[0] == "preview")
                response = new { RequestFile = path, Request = prepared.Request, Preview = await service.PreviewAsync(prepared.Request, timeout.Token) };
            else
                response = new { RequestFile = path, Result = await service.SendAsync(prepared.Request, timeout.Token) };
            break;
        }
        default:
            throw new GiftInboxException("invalid_command", "Use accounts, prepare, preview or send.");
    }
    Console.WriteLine(JsonSerializer.Serialize(response, json));
    return 0;
}
catch (GiftInboxException ex)
{
    Console.WriteLine(JsonSerializer.Serialize(new { Error = new { ex.Code, ex.Message } }, json));
    return 2;
}
catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
{
    Console.WriteLine(JsonSerializer.Serialize(new { Error = new { Code = "gift_schema_not_ready", Message = "Deploy the gift inbox server migration first." } }, json));
    return 3;
}
catch (NpgsqlException)
{
    Console.WriteLine(JsonSerializer.Serialize(new { Error = new { Code = "database_unavailable", Message = "Database access failed. Keep the same request file when retrying; completed sends are deduplicated." } }, json));
    return 3;
}
catch (OperationCanceledException)
{
    Console.WriteLine(JsonSerializer.Serialize(new { Error = new { Code = "operation_timeout", Message = "The operation timed out. Retry with the same request file." } }, json));
    return 3;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
{
    Console.WriteLine(JsonSerializer.Serialize(new { Error = new { Code = ex switch { JsonException => "invalid_json_input", UnauthorizedAccessException => "local_access_denied", IOException => "local_file_error", _ => "invalid_local_arguments" }, Message = "Check the input, file path and permissions. Existing request files are never overwritten." } }, json));
    return 2;
}

static async Task WritePreparedFileAsync(string path, PreparedGift prepared, JsonSerializerOptions json,
    CancellationToken cancellationToken)
{
    var directory = Path.GetDirectoryName(path)!;
    Directory.CreateDirectory(directory);
    // Write fully before publishing the retry key. A crash or cancellation cannot
    // leave a half-written request at the path the administrator later sends.
    var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
    try
    {
        await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(output, prepared, json, cancellationToken);
            await output.FlushAsync(cancellationToken);
            output.Flush(flushToDisk: true);
        }
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(temporaryPath, path, overwrite: false);
    }
    finally
    {
        // This is exclusively our unique temporary file, never the requested file.
        try { File.Delete(temporaryPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

static string ReadInput()
{
    var input = Console.In.ReadToEnd();
    if (input.Length > 65536) throw new GiftInboxException("request_too_large", "Request input is too large.");
    // Windows PowerShell 5.1 can prepend a UTF-8 BOM to a native stdin pipe,
    // even when $OutputEncoding was created without a preamble. Console.In
    // preserves it, while System.Text.Json expects the string to start at JSON.
    if (input.Length > 0 && input[0] == '\uFEFF') input = input[1..];
    return input;
}

internal sealed record AccountSearch(string? Search);
internal sealed record PreparedGift(int Version, string DatabaseIdentity, GiftSendRequest Request);