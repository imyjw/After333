using Npgsql;

namespace Project333.PvpServer.Persistence.Db;

public sealed class DbConnectionFactory
{
    public const string ConnectionStringKey = "PROJECT333_DB_CONNECTION";

    private readonly IConfiguration _configuration;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    private string? ConnectionString => _configuration[ConnectionStringKey];

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException($"{ConnectionStringKey} is not set.");
        }

        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
