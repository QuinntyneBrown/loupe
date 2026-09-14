namespace Loupe.Application.Search;

/// <summary>The local embedding model behind Meaning search and location indexing; unconfigured means no vectors are produced.</summary>
public interface IEmbeddingConfiguration
{
    bool IsConfigured { get; }
    string Model { get; }
}
