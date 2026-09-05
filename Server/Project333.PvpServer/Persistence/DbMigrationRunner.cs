using Npgsql;

namespace Project333.PvpServer.Persistence;

public static class DbMigrationRunner
{
    private const string ConnectionStringKey = "PROJECT333_DB_CONNECTION";

    private const string EnsureMigrationTableSql = """
        create table if not exists schema_migrations (
            version text primary key,
            applied_at timestamptz not null default now()
        );
        """;

    public static async Task RunFromConfigurationAsync(
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine($"[db] {ConnectionStringKey} is not set. Skipping PostgreSQL migrations.");
            return;
        }

        var migrationsPath = ResolveMigrationsPath(environment.ContentRootPath);
        await RunAsync(connectionString, migrationsPath, cancellationToken);
    }

    public static async Task RunAsync(
        string connectionString,
        string migrationsPath,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(migrationsPath))
        {
            throw new DirectoryNotFoundException($"Migration folder was not found: {migrationsPath}");
        }

        var migrationFiles = Directory
            .EnumerateFiles(migrationsPath, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (migrationFiles.Length == 0)
        {
            Console.WriteLine($"[db] No PostgreSQL migration files found in {migrationsPath}.");
            return;
        }

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var ensureCommand = new NpgsqlCommand(EnsureMigrationTableSql, connection))
        {
            await ensureCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var migrationFile in migrationFiles)
        {
            var version = Path.GetFileNameWithoutExtension(migrationFile);
            if (await IsMigrationAppliedAsync(connection, version, cancellationToken))
            {
                Console.WriteLine($"[db] Migration {version} already applied.");
                continue;
            }

            var sql = await File.ReadAllTextAsync(migrationFile, cancellationToken);
            Console.WriteLine($"[db] Applying migration {version}...");
            await using var migrationCommand = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = 120
            };
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);

            if (!await IsMigrationAppliedAsync(connection, version, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Migration {version} completed without recording its version in schema_migrations.");
            }

            Console.WriteLine($"[db] Applied migration {version}.");
        }
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        NpgsqlConnection connection,
        string version,
        CancellationToken cancellationToken)
    {
        const string sql = "select exists (select 1 from schema_migrations where version = @version);";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("version", version);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static string ResolveMigrationsPath(string contentRootPath)
    {
        var contentRootMigrationsPath = Path.Combine(contentRootPath, "Persistence", "Migrations");
        if (Directory.Exists(contentRootMigrationsPath))
        {
            return contentRootMigrationsPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "Persistence", "Migrations");
    }
}
