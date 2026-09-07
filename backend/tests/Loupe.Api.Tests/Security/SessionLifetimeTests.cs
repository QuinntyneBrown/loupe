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
    public async Task L2_037_3_Activity_renews_idle_time_but_never_the_absolute_lifetime()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var callback = await OidcFlow.CompleteAsync(factory, client);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        for (var interval = 0; interval < 24; interval++)
        {
            factory.Clock.Advance(TimeSpan.FromMinutes(29));
            using var active = await client.GetAsync("/api/session");
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        }
        factory.Clock.Advance(TimeSpan.FromMinutes(24) - TimeSpan.FromMilliseconds(1));
        using var before = await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        factory.Clock.Advance(TimeSpan.FromMilliseconds(1));
        using var atBoundary = await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, atBoundary.StatusCode);
    }

    [Fact]
    public async Task L2_037_5_Login_rotates_the_cookie_and_rejects_its_predecessor_across_instances()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var first = await OidcFlow.CompleteAsync(factory, client);
        var oldCookie = Assert.Single(first.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-loupe-session=", StringComparison.Ordinal)).Split(';')[0];
        using var second = await OidcFlow.CompleteAsync(factory, client);
        var newCookie = Assert.Single(second.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-loupe-session=", StringComparison.Ordinal)).Split(';')[0];
        Assert.NotEqual(oldCookie, newCookie);
        await using var another = new ApiFactory(database.ConnectionString);
        using var replay = another.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        replay.DefaultRequestHeaders.Add("Cookie", oldCookie);
        using var rejected = await replay.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        replay.DefaultRequestHeaders.Remove("Cookie");
        replay.DefaultRequestHeaders.Add("Cookie", newCookie);
        using var accepted = await replay.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

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
