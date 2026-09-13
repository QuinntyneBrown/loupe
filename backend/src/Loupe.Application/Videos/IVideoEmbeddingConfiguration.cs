namespace Loupe.Application.Videos;

public interface IVideoEmbeddingConfiguration
{
    /// <summary>The configured embedding model identity, or null when embeddings are not configured.</summary>
    string? CurrentModel { get; }
}
