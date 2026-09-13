namespace Loupe.Application.Videos;

public interface IVideoEmbeddingProvider
{
    /// <summary>Embeds text with the configured model; throws <see cref="Loupe.Application.Operations.ProviderFailureException"/> on failure.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}
