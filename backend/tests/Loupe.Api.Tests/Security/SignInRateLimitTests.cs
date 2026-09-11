// Acceptance Test
// Traces to: L2-037
// Description: Failed credential attempts are limited per client IP with controlled-clock recovery.
using System.Net;
using Loupe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Loupe.Api.Tests.Security;

public sealed class SignInRateLimitTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_037_9_Ten_attempts_per_minute_then_retry_after_window()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var failed = await LocalSignInFlow.LoginAsync(client, "absent@example.com", LocalSignInFlow.Password);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }
        using var limited = await LocalSignInFlow.LoginAsync(client, "absent@example.com", LocalSignInFlow.Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(1), limited.Headers.RetryAfter?.Delta);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        using var retry = await LocalSignInFlow.LoginAsync(client, "absent@example.com", LocalSignInFlow.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
    }
}
