// Acceptance Test
// Traces to: L2-037, L2-050
// Description: A configured Seed section ensures one local account at API startup without touching existing accounts.
using System.Net;
using System.Net.Http.Json;
using Loupe.Application.Sessions;
using Loupe.Application.Users;
using Loupe.Domain.Users;
using Loupe.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;
namespace Loupe.Api.Tests.Security;

public sealed class SeedAccountTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Name = "Seeded Photographer";
    private const string Password = "seeded password";
    private static Dictionary<string, string?> Seed(string email, string name = Name, string password = Password) =>
        new() { ["Seed:Email"] = email, ["Seed:Name"] = name, ["Seed:Password"] = password };
    private static HttpClient Client(ApiFactory factory, bool handleCookies = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = handleCookies });
    private async Task MigrateAsync()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
    }
    private static async Task<List<User>> UsersAsync(ApiFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Users.AsNoTracking()
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant()).ToListAsync();
    }
    private static string SessionCookie(HttpResponseMessage login) =>
        Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-loupe-session=")).Split(';')[0];

    [Fact]
    public async Task L2_037_13_Configured_seed_creates_an_account_that_signs_in_before_the_first_request()
    {
        await MigrateAsync();
        var email = Guid.NewGuid() + "@example.com";
        await using var factory = new ApiFactory(database.ConnectionString) { Settings = Seed(email) };
        using var client = Client(factory);
        using var login = await LocalSignInFlow.LoginAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<SessionResult>();
        Assert.True(Guid.TryParse(session!.Subject, out _));
        Assert.Equal(Name, session.Name);
        var user = Assert.Single(await UsersAsync(factory, email));
        Assert.Equal(session.Subject, user.Id);
        Assert.DoesNotContain(Password, user.PasswordHash);
    }

    [Fact]
    public async Task L2_037_14_Restart_with_the_same_seed_leaves_an_existing_account_untouched()
    {
        await MigrateAsync();
        var email = Guid.NewGuid() + "@example.com";
        const string replacement = "a replacement password";
        string subject, cookie;
        await using (var first = new ApiFactory(database.ConnectionString) { Settings = Seed(email) })
        {
            using var client = Client(first);
            using var login = await LocalSignInFlow.LoginAsync(client, email, Password);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            subject = (await login.Content.ReadFromJsonAsync<SessionResult>())!.Subject;
            await using (var scope = first.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ResetPasswordCommand(email, replacement));
            using var replaced = await LocalSignInFlow.LoginAsync(client, email, replacement);
            Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
            cookie = SessionCookie(replaced);
        }
        await using var restarted = new ApiFactory(database.ConnectionString) { Settings = Seed(email, name: "Renamed Photographer", password: "another seed password") };
        using var later = Client(restarted);
        using var seeded = await LocalSignInFlow.LoginAsync(later, email, Password);
        Assert.Equal(HttpStatusCode.Unauthorized, seeded.StatusCode);
        using var renamed = await LocalSignInFlow.LoginAsync(later, email, "another seed password");
        Assert.Equal(HttpStatusCode.Unauthorized, renamed.StatusCode);
        using var kept = await LocalSignInFlow.LoginAsync(later, email, replacement);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        var session = await kept.Content.ReadFromJsonAsync<SessionResult>();
        Assert.Equal(subject, session!.Subject);
        Assert.Equal(Name, session.Name);
        using var replay = Client(restarted, handleCookies: false);
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        using var survived = await replay.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.OK, survived.StatusCode);
        var user = Assert.Single(await UsersAsync(restarted, email));
        Assert.Equal(subject, user.Id);
    }

    [Fact]
    public async Task L2_037_15_Concurrent_seeds_create_exactly_one_account()
    {
        await MigrateAsync();
        var email = Guid.NewGuid() + "@example.com";
        await using var first = new ApiFactory(database.ConnectionString) { Settings = Seed(email) };
        await using var second = new ApiFactory(database.ConnectionString) { Settings = Seed(email) };
        var clients = await Task.WhenAll(Task.Run(() => Client(first)), Task.Run(() => Client(second)));
        Assert.Single(await UsersAsync(first, email));
        var racing = Guid.NewGuid() + "@example.com";
        var command = new EnsureUserCommand(racing, Name, Password);
        await using var scopeA = first.Services.CreateAsyncScope();
        await using var scopeB = second.Services.CreateAsyncScope();
        await Task.WhenAll(scopeA.ServiceProvider.GetRequiredService<ISender>().Send(command), scopeB.ServiceProvider.GetRequiredService<ISender>().Send(command));
        Assert.Single(await UsersAsync(second, racing));
        foreach (var client in clients)
        {
            using var login = await LocalSignInFlow.LoginAsync(client, email, Password);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            client.Dispose();
        }
    }

    [Theory]
    [InlineData("Seed:Email", "", 1)]
    [InlineData("Seed:Name", "", 1)]
    [InlineData("Seed:Password", "", 1)]
    [InlineData("Seed:Email", "not-an-email", 1)]
    [InlineData("Seed:Name", "n", 201)]
    [InlineData("Seed:Password", "p", 7)]
    [InlineData("Seed:Password", "p", 129)]
    public async Task L2_037_16_Partial_or_invalid_seed_configuration_prevents_startup(string key, string value, int repeat)
    {
        await MigrateAsync();
        var settings = Seed(Guid.NewGuid() + "@example.com");
        settings[key] = string.Concat(Enumerable.Repeat(value, repeat));
        await using var factory = new ApiFactory(database.ConnectionString) { Settings = settings };
        var failure = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains(key, failure.Message);
        if (settings["Seed:Password"]!.Length > 0) Assert.DoesNotContain(settings["Seed:Password"]!, failure.Message);
        await using var plain = new ApiFactory(database.ConnectionString);
        if (settings["Seed:Email"]!.Length > 0) Assert.Empty(await UsersAsync(plain, settings["Seed:Email"]!));
    }

    [Fact]
    public async Task L2_037_17_Unmigrated_database_prevents_startup_with_migration_guidance()
    {
        var name = "loupe_unmigrated_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection))
            await create.ExecuteNonQueryAsync();
        var unmigrated = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = name }.ConnectionString;
        await using var factory = new ApiFactory(unmigrated) { Settings = Seed(Guid.NewGuid() + "@example.com") };
        var failure = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Seed", failure.Message);
        Assert.Contains("migrations", failure.Message);
        Assert.DoesNotContain(Password, failure.ToString());
        Assert.IsAssignableFrom<PostgresException>(failure.InnerException);
        await using var untouched = new NpgsqlConnection(unmigrated);
        await untouched.OpenAsync();
        await using var tables = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'", untouched);
        Assert.Equal(0L, await tables.ExecuteScalarAsync());
    }
}
