using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Loupe.Api.Tests.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Loupe.Application.Critiques;
using Loupe.Api.Tests.Critiques;

namespace Loupe.Api.Tests;

public sealed class ApiFactory(string? connectionString = null, string? mediaRoot = null) : WebApplicationFactory<Program>
{
    public TestClock Clock { get; } = new();
    public TimeProvider? ClockOverride { get; init; }
    public CapturedApiFailure Failure { get; } = new();
    public DbTransactionInterceptor? TransactionInterceptor { get; init; }
    public ICritiqueProvider? CritiqueProvider { get; init; }
    public HttpMessageHandler? AiTransport { get; init; }
    public HttpMessageHandler? EmbeddingTransport { get; init; }
    public HttpMessageHandler? SourceTransport { get; init; }
    public Loupe.Application.ReferenceImports.IDnsResolver? SourceDnsResolver { get; init; }
    public Loupe.Application.ReferenceImports.ISourceConnector? SourceConnector { get; init; }
    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string subject = "owner-a")
    {
        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        try
        {
            await ReauthenticateAsync(client, subject);
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    public async Task ReauthenticateAsync(HttpClient client, string subject)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Remove("Origin");
        using var login = await LocalSignInFlow.CompleteAsync(this, client, subject);
        login.EnsureSuccessStatusCode();
        using var session = await client.GetAsync("/api/session");
        session.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("X-CSRF-Token", session.Headers.GetValues("X-CSRF-Token").Single());
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (SourceTransport is not null)
            builder.ConfigureTestServices(services => services.AddHttpClient("sourceFetch").ConfigurePrimaryHttpMessageHandler(() => SourceTransport));
        if (AiTransport is not null)
            builder.ConfigureTestServices(services => services.AddHttpClient("azure-openai").ConfigurePrimaryHttpMessageHandler(() => AiTransport));
        if (EmbeddingTransport is not null)
            builder.ConfigureTestServices(services => services.AddHttpClient("ollama").ConfigurePrimaryHttpMessageHandler(() => EmbeddingTransport));
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:Endpoint"] = "https://loupe-fixture.openai.azure.com",
            ["Ai:Deployment"] = "critique-fixture",
            ["Jwt:Issuer"] = "Loupe",
            ["Jwt:Audience"] = "Loupe",
            ["Jwt:SigningKey"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("fixture-only-local-signing-key-32-bytes".PadRight(64, 'x'))),
            ["Browser:AllowedOrigins:0"] = "https://localhost",
            ["Media:Root"] = mediaRoot ?? Path.Combine(Path.GetTempPath(), "loupe-unused-media"),
            ["ConnectionStrings:Library"] = connectionString ?? "Host=localhost;Database=unused;Username=unused"
        }));
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings));
        builder.ConfigureTestServices(services =>
        {
            if (CritiqueProvider is not null || AiTransport is null)
            {
                services.RemoveAll<ICritiqueProvider>();
                services.AddSingleton<ICritiqueProvider>(CritiqueProvider ??
                    new ControlledCritiqueProvider((_, _, _) => Task.FromResult(CritiqueResultFixture.Valid())));
            }
            if (SourceDnsResolver is not null)
            {
                services.RemoveAll<Loupe.Application.ReferenceImports.IDnsResolver>();
                services.AddSingleton(SourceDnsResolver);
            }
            if (SourceConnector is not null)
            {
                services.RemoveAll<Loupe.Application.ReferenceImports.ISourceConnector>();
                services.AddSingleton(SourceConnector);
            }
            var interceptor = TransactionInterceptor;
            if (interceptor is not null) services.AddDbContext<LibraryDbContext>(options => options.AddInterceptors(interceptor));
            services.Insert(0, ServiceDescriptor.Singleton<IExceptionHandler>(Failure));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(ClockOverride ?? Clock);
        });
    }
}
