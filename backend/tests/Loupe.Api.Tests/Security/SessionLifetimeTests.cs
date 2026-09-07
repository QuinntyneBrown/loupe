// Acceptance Test
// Traces to: L2-037
// Description: A valid signed identity establishes a session that expires at the idle boundary.
using System.Net;
using System.Net.Http.Json;
using Loupe.Application.Sessions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Api.Tests.Security;

public sealed class SessionLifetimeTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_037_1_3_5_Valid_login_is_private_and_expires_at_thirty_minutes_idle()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var callback = await OidcFlow.CompleteAsync(factory, client);
        Assert.True(callback.StatusCode == HttpStatusCode.Redirect, factory.Failure.Exception?.ToString());
        Assert.Equal("/my-work", callback.Headers.Location?.OriginalString);
        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-loupe-session=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        var session = await client.GetFromJsonAsync<SessionResult>("/api/session");
        Assert.Equal("owner-a", session?.Subject);
        factory.Clock.Advance(TimeSpan.FromMinutes(30));
        using var expired = await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }
}
