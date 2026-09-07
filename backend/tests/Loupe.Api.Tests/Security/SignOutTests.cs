// Given an authenticated browser, when sign-out is acknowledged, then even a copied cookie is revoked.
// L2-037.4 and L2-039.4: only trusted-origin requests with valid antiforgery protection may mutate sessions.
using System.Net;
using Loupe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Security;

public sealed class SignOutTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_037_4_Protected_sign_out_revokes_the_session_on_every_instance()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var callback = await OidcFlow.CompleteAsync(factory, client);
        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-loupe-session=", StringComparison.Ordinal)).Split(';')[0];
        using var session = await client.GetAsync("/api/session");
        Assert.True(session.Headers.Contains("X-CSRF-Token"), "The authenticated session response must supply an antiforgery token.");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", session.Headers.GetValues("X-CSRF-Token").Single());
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        using var signOut = await client.PostAsync("/api/session/sign-out", null);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        using var after = await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        await using var second = new ApiFactory(database.ConnectionString);
        using var replay = second.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        using var rejected = await replay.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }
}
