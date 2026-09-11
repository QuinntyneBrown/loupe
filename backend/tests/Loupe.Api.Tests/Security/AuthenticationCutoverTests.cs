// Acceptance Test
// Traces to: L2-037, L2-038
// Description: The authentication migration retains legacy content without granting it to new accounts.
using System.Net;
using Loupe.Domain.Photographs;
using Loupe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
namespace Loupe.Api.Tests.Security;

public sealed class AuthenticationCutoverTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_037_10_Migration_retains_old_content_and_invalidates_old_sessions()
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        var photoId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var previous = db.Database.GetMigrations().Last(m => !m.EndsWith("_LocalUsers", StringComparison.Ordinal));
            await db.GetService<IMigrator>().MigrateAsync(previous);
            db.Photographs.Add(new Photograph { Id = photoId, OwnerId = "legacy-owner", Title = "Retained photograph", CreatedAt = factory.Clock.GetUtcNow(), ImageKey = "legacy-original", PreviewKey = "legacy-preview", Width = 20, Height = 20 });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO sessions ("Id", "Issuer", "Subject", "Name", "CreatedAt", "LastSeenAt")
                VALUES ('legacy-session', 'former-provider', 'owner-a', 'Photographer', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                """);
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Sessions.ToListAsync());
            Assert.Equal("legacy-owner", (await db.Photographs.AsNoTracking().SingleAsync(p => p.Id == photoId)).OwnerId);
        }
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var foreign = await client.GetAsync($"/api/photographs/{photoId}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var callback = await client.GetAsync("/signin-oidc");
        Assert.Equal(HttpStatusCode.NotFound, callback.StatusCode);
        Assert.Null(callback.Headers.Location);
    }
    [Theory]
    [InlineData("Jwt:SigningKey", "")]
    [InlineData("Jwt:SigningKey", "not-base64")]
    [InlineData("Jwt:SigningKey", "c2hvcnQ=")]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    public async Task L2_037_11_Invalid_JWT_configuration_prevents_startup(string key, string value)
    {
        await using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { [key] = value } };
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
