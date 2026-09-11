using System.Net.Http.Json;
using Loupe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Api.Tests.Security;

public static class LocalSignInFlow
{
    public const string Password = "local acceptance password";
    public static async Task<HttpResponseMessage> CompleteAsync(ApiFactory factory, HttpClient client, string subject = "owner-a")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.Database.MigrateAsync();
        var email = subject + "@example.com";
        var hash = new PasswordHasher<object>().HashPassword(new object(), Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users ("Id", "Email", "NormalizedEmail", "Name", "PasswordHash")
            VALUES ({subject}, {email}, {email.ToUpperInvariant()}, {"Photographer"}, {hash})
            ON CONFLICT ("NormalizedEmail") DO NOTHING
            """);
        return await LoginAsync(client, email, Password);
    }
    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        using var csrf = await client.GetAsync("/api/session/csrf");
        csrf.EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/sign-in");
        request.Headers.Add("Origin", "https://localhost");
        request.Headers.Add("X-CSRF-Token", csrf.Headers.GetValues("X-CSRF-Token").Single());
        request.Content = JsonContent.Create(new { email, password });
        return await client.SendAsync(request);
    }
}
