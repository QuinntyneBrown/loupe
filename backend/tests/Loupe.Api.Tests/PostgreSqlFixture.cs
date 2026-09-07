using Testcontainers.PostgreSql;
using Xunit;

namespace Loupe.Api.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("pgvector/pgvector@sha256:7d400e340efb42f4d8c9c12c6427adb253f726881a9985d2a471bf0eed824dff")
        .WithDatabase("loupe_acceptance").WithUsername("loupe")
        .WithPassword(Guid.NewGuid().ToString("N")).Build();
    public string ConnectionString => database.GetConnectionString();
    public string MediaRoot { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loupe-acceptance", Guid.NewGuid().ToString("N")));
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync()
    {
        await database.DisposeAsync();
        var relative = Path.GetRelativePath(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loupe-acceptance")), MediaRoot);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new InvalidOperationException("Unsafe fixture cleanup path.");
        if (Directory.Exists(MediaRoot)) Directory.Delete(MediaRoot, recursive: true);
    }
}
