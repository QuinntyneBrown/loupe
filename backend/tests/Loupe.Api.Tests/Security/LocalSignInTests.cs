// Acceptance Test
// Traces to: L2-037
// Description: Local sign-in accepts credentials only with browser request protection.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Api.Tests.Security;

public sealed class LocalSignInTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_037_Local_login_rejects_missing_browser_protection()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/session/sign-in", new { email = "unknown@example.com", password = "a long test password" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task L2_037_1_5_Provisioned_credentials_issue_a_local_JWT()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await LocalSignInFlow.CompleteAsync(factory, client, "local-user");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-loupe-session="));
        Assert.Equal(3, cookie.Split(';')[0].Split('=', 2)[1].Split('.').Length);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        using var session = await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task L2_037_2_Unknown_credentials_fail_generically()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var csrf = await client.GetAsync("/api/session/csrf");
        Assert.Equal(HttpStatusCode.NoContent, csrf.StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf.Headers.GetValues("X-CSRF-Token").Single());
        using var response = await client.PostAsJsonAsync("/api/session/sign-in", new { email = "unknown@example.com", password = LocalSignInFlow.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_credentials", await response.Content.ReadAsStringAsync());
    }
}
