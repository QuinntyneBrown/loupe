namespace Loupe.Application.Search;

/// <summary>Embeds one document or query with the configured local model; failures surface as provider failures.</summary>
public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}
