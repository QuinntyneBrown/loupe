// Acceptance Test
// Traces to: L2-037, L2-038
// Description: Operator provisioning and reset are verified through real API sign-in.
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Loupe.Application.Sessions;
using Loupe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Loupe.Api.Tests.Security;

public sealed class AdminAccountTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private async Task<int> AdminAsync(string command, string email, string password)
    {
        var dll = Path.GetFullPath("../../../../../src/Loupe.Admin/bin/Debug/net10.0/Loupe.Admin.dll", AppContext.BaseDirectory);
        var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(dll); start.ArgumentList.Add(command); start.ArgumentList.Add(email);
        if (command == "create-user") start.ArgumentList.Add("Test Photographer");
        start.Environment["ConnectionStrings__Library"] = database.ConnectionString;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteLineAsync(password); process.StandardInput.Close();
        await process.WaitForExitAsync();
        Assert.DoesNotContain(password, await stdout); Assert.DoesNotContain(password, await stderr);
        return process.ExitCode;
    }
    private async Task<ApiFactory> FactoryAsync()
    {
        var factory = new ApiFactory(database.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        return factory;
    }
    [Fact]
    public async Task L2_037_6_7_Provisioned_user_survives_restart_and_reset_revokes_all_sessions()
    {
        await using var factory = await FactoryAsync();
        var email = Guid.NewGuid() + "@example.com";
        Assert.Equal(0, await AdminAsync("create-user", email, LocalSignInFlow.Password));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var login = await LocalSignInFlow.LoginAsync(client, "  " + email.ToUpperInvariant() + "  ", LocalSignInFlow.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var user = await login.Content.ReadFromJsonAsync<SessionResult>();
        Assert.True(Guid.TryParse(user!.Subject, out _));
        var copiedCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-loupe-session=")).Split(';')[0];
        Assert.Equal(0, await AdminAsync("reset-password", email, "a replacement long password"));
        using var expired = await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var oldPassword = await LocalSignInFlow.LoginAsync(client, email, LocalSignInFlow.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        await using var restarted = new ApiFactory(database.ConnectionString);
        using var later = restarted.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        later.DefaultRequestHeaders.Add("Cookie", copiedCookie);
        using var replay = await later.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var fresh = restarted.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var newPassword = await LocalSignInFlow.LoginAsync(fresh, email, "a replacement long password");
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
        Assert.Equal(user.Subject, (await newPassword.Content.ReadFromJsonAsync<SessionResult>())!.Subject);
    }
    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task L2_037_6_Password_length_boundaries_are_enforced(int length, bool accepted)
    {
        await using var factory = await FactoryAsync();
        var email = Guid.NewGuid() + "@example.com";
        Assert.Equal(accepted, await AdminAsync("create-user", email, new string('x', length)) == 0);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var login = await LocalSignInFlow.LoginAsync(client, email, new string('x', Math.Clamp(length, 15, 128)));
        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, login.StatusCode);
    }
    [Fact]
    public async Task L2_037_6_Concurrent_case_insensitive_duplicates_create_exactly_one_account()
    {
        await using var factory = await FactoryAsync();
        var email = Guid.NewGuid() + "@example.com";
        var results = await Task.WhenAll(AdminAsync("create-user", email, LocalSignInFlow.Password), AdminAsync("create-user", email.ToUpperInvariant(), LocalSignInFlow.Password));
        Assert.Single(results, code => code == 0);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var login = await LocalSignInFlow.LoginAsync(client, email, LocalSignInFlow.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
