using Loupe.Application.ReferenceImports;
using Loupe.Infrastructure.ReferenceImports;
using Loupe.Application.Sessions;
using Loupe.Application.Photographs;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.Deletions;
using Loupe.Application.Maintenance;
using Loupe.Application.Critiques;
using Loupe.Application.References;
using Loupe.Infrastructure.Ai;
using Loupe.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Persistence;

public static class PersistenceSetup
{
    public static IServiceCollection AddLoupePersistence(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>().BindConfiguration("ConnectionStrings")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Library), "ConnectionStrings:Library is required.").ValidateOnStart();
        services.AddDbContext<LibraryDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Library));
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<IPhotographStore, PhotographStore>();
        services.AddScoped<IReferenceStore, ReferenceStore>();
        services.AddScoped<IReferenceImportStore, ReferenceImportStore>();
        services.AddSingleton<IReferenceImportConfiguration, ReferenceImportConfiguration>();
        services.AddOptions<ReferenceImportOptions>().BindConfiguration("Imports")
            .Validate(options => options.Mode is null or "Demo" or "Live", "Imports:Mode must be Demo or Live when configured.").ValidateOnStart();
        services.AddSingleton<IDnsResolver, DnsResolver>();
        services.AddSingleton<ISourceConnector, SocketSourceConnector>();
        services.AddScoped<IRestrictedPageFetcher, RestrictedPageFetcher>();
        services.AddHttpClient("sourceFetch", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(provider => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var resolver = provider.GetRequiredService<IDnsResolver>();
                    var connector = provider.GetRequiredService<ISourceConnector>();
                    var addresses = await resolver.ResolveAsync(context.DnsEndPoint.Host, cancellationToken);
                    var address = addresses.FirstOrDefault(PublicAddressPolicy.IsPublic)
                        ?? throw new SourceFetchException(SourceFetchFailureKind.ForbiddenDestination);
                    return await connector.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken);
                }
            })
            .RemoveAllLoggers();
        services.AddScoped<IOperationReceiptStore, OperationReceiptStore>();
        services.AddScoped<IBackgroundOperationStore, BackgroundOperationStore>();
        services.AddSingleton<ICritiqueConfiguration, CritiqueConfiguration>();
        services.AddScoped<ICritiqueWorkStore, CritiqueWorkStore>();
        services.AddScoped<IOperationLeaseStore, OperationLeaseStore>();
        services.AddSingleton<DemoCritiqueProvider>();
        services.AddScoped<OpenAiCritiqueProvider>();
        services.AddScoped<ICritiqueProvider>(provider => provider.GetRequiredService<IOptions<AiOptions>>().Value.Mode == "Demo"
            ? provider.GetRequiredService<DemoCritiqueProvider>() : provider.GetRequiredService<OpenAiCritiqueProvider>());
        services.AddHttpClient("openai", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(10) })
            .RemoveAllLoggers();
        services.AddOptions<AiOptions>().BindConfiguration("Ai")
            .Validate(options => options.MaxConcurrentCalls is >= 1 and <= 64, "Ai:MaxConcurrentCalls must be between one and 64.")
            .Validate(options => options.Mode is null or "Demo" or "Live", "Ai:Mode must be Demo or Live when configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Ai:Model must name a model.").ValidateOnStart();
        services.AddScoped<IDeletionStore, DeletionStore>();
        services.AddScoped<IDeletedContentCleaner, DeletedContentCleaner>();
        services.AddScoped<IAbandonedMediaCleaner, AbandonedMediaCleaner>();
        services.AddScoped<IDeletionRetention, DeletionRetention>();
        services.AddOptions<MediaOptions>().BindConfiguration("Media")
            .Validate(options => Path.IsPathFullyQualified(options.Root), "Media:Root must be an absolute private storage path.").ValidateOnStart();
        services.AddSingleton<IImageStore, FileImageStore>();
        services.AddSingleton<IImageIngestor, ImageIngestor>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
