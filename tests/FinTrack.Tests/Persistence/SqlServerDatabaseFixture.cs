using FinTrack.Tests.Architecture;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using FinTrack.Infrastructure.Persistence;

namespace FinTrack.Tests.Persistence;

// Banco isolado por instância: cria FinTrackTest_<guid> no SQL Server real do compose e remove ao final.
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"FinTrackTest_{Guid.NewGuid():N}";
    private SqlConnectionStringBuilder _server = null!;

    public string ConnectionString =>
        new SqlConnectionStringBuilder(_server.ConnectionString) { InitialCatalog = _databaseName }.ConnectionString;

    public async Task InitializeAsync()
    {
        _server = BuildServerConnection();
        await ExecuteOnMasterAsync($"CREATE DATABASE [{_databaseName}]");
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await ExecuteOnMasterAsync(
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]");
    }

    public FinTrackDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FinTrackDbContext>().UseSqlServer(ConnectionString).Options);

    private async Task ExecuteOnMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(_server.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static SqlConnectionStringBuilder BuildServerConnection()
    {
        var env = ReadDotEnv();
        string? Get(string key) => Environment.GetEnvironmentVariable(key) ?? env.GetValueOrDefault(key);

        var password = Get("MSSQL_SA_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "MSSQL_SA_PASSWORD ausente (variável de ambiente ou .env). Suba o SQL Server com `docker compose up -d --wait sqlserver`.");
        }

        return new SqlConnectionStringBuilder
        {
            DataSource = $"localhost,{Get("MSSQL_PORT") ?? "14330"}",
            UserID = "sa",
            Password = password,
            InitialCatalog = "master",
            TrustServerCertificate = true,
            ConnectTimeout = 30,
        };
    }

    private static Dictionary<string, string> ReadDotEnv()
    {
        var path = Path.Combine(ProjectReferenceRules.FindRepositoryRoot(), ".env");
        var values = new Dictionary<string, string>();
        if (!File.Exists(path))
        {
            return values;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf('=');
            if (trimmed.StartsWith('#') || separator <= 0)
            {
                continue;
            }

            values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
        }

        return values;
    }
}
