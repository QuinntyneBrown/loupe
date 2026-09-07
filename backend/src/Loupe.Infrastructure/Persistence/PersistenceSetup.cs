using Loupe.Application.Sessions;
using Loupe.Application.Photographs;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.Deletions;
using Loupe.Application.Maintenance;
using Loupe.Application.Critiques;
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
        services.AddScoped<IOperationReceiptStore, OperationReceiptStore>();
        services.AddScoped<IBackgroundOperationStore, BackgroundOperationStore>();
        services.AddSingleton<ICritiqueConfiguration, CritiqueConfiguration>();
        services.AddOptions<AiOptions>().BindConfiguration("Ai")
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
