using Loupe.Application.ReferenceImports;
using Loupe.Application.Boards;
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
        services.AddLoupeAccounts();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<IPhotographStore, PhotographStore>();
        services.AddScoped<IReferenceStore, ReferenceStore>();
        services.AddScoped<Loupe.Application.ReferenceDrafts.IReferenceDraftStore, ReferenceDraftStore>();
        services.AddScoped<IReferenceTagStore, ReferenceTagStore>();
        services.AddScoped<IBoardStore, BoardStore>();
        services.AddScoped<IReferenceImportStore, ReferenceImportStore>();
        services.AddSingleton<IReferenceImportConfiguration, ReferenceImportConfiguration>();
        services.AddOptions<ReferenceImportOptions>().BindConfiguration("Imports")
            .Validate(options => options.Mode is null or "Live", "Imports:Mode must be Live when configured; Demo execution has been retired.").ValidateOnStart();
        services.AddSingleton<IDnsResolver, DnsResolver>();
        services.AddSingleton<ISourceConnector, SocketSourceConnector>();
        services.AddScoped<IRestrictedPageFetcher, RestrictedPageFetcher>();
        services.AddScoped<IRobotsPolicy, RobotsPolicy>();
        services.AddScoped<IReferenceSourceReader, ReferenceSourceReader>();
        services.AddScoped<IReferenceImportWorkStore, ReferenceImportWorkStore>();
        services.AddHttpClient("sourceFetch", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Loupe/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(provider => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false,
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
        services.AddScoped<ICritiqueProvider, AzureOpenAiCritiqueProvider>();
        services.AddHttpClient("azure-openai", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(10) })
            .RemoveAllLoggers();
        services.AddOptions<AiOptions>().BindConfiguration("Ai")
            .Validate(options => options.MaxConcurrentCalls is >= 1 and <= 64, "Ai:MaxConcurrentCalls must be between one and 64.")
            .Validate(options => options.Mode is null or "Live", "Ai:Mode must be Live when configured; Demo execution has been retired.")
            .Validate(options => options.HasValidEndpoint, "Ai:Endpoint must be an HTTPS resource root without credentials, query, fragment, or additional path.")
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
