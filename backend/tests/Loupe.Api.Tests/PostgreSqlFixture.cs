using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Loupe.Api.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    // LOUPE_TEST_POSTGRES names a reachable pgvector-enabled server (a superuser connection string);
    // each fixture then creates and drops its own database instead of starting a container.
    private static readonly string? Server = Environment.GetEnvironmentVariable("LOUPE_TEST_POSTGRES");
    private readonly string databaseName = "loupe_acceptance_" + Guid.NewGuid().ToString("N");
    private readonly PostgreSqlContainer? database = Server is null
        ? new PostgreSqlBuilder("pgvector/pgvector@sha256:7d400e340efb42f4d8c9c12c6427adb253f726881a9985d2a471bf0eed824dff")
            .WithDatabase("loupe_acceptance").WithUsername("loupe")
            .WithPassword(Guid.NewGuid().ToString("N")).Build()
        : null;
    public string ConnectionString => database?.GetConnectionString()
        ?? new NpgsqlConnectionStringBuilder(Server) { Database = databaseName }.ConnectionString;
    public string MediaRoot { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loupe-acceptance", Guid.NewGuid().ToString("N")));
    public async Task InitializeAsync()
    {
        if (database is not null) { await database.StartAsync(); return; }
        await using var connection = new NpgsqlConnection(Server);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await create.ExecuteNonQueryAsync();
    }
    public async Task DisposeAsync()
    {
        if (database is not null) await database.DisposeAsync();
        else
        {
            await using var connection = new NpgsqlConnection(Server);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
        var relative = Path.GetRelativePath(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loupe-acceptance")), MediaRoot);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new InvalidOperationException("Unsafe fixture cleanup path.");
        if (Directory.Exists(MediaRoot)) Directory.Delete(MediaRoot, recursive: true);
    }
}
