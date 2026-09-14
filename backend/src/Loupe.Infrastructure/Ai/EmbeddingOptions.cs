namespace Loupe.Infrastructure.Ai;

/// <summary>Local embedding inference (Ollama) beside the API; no endpoint means Meaning search and indexing are not configured.</summary>
public sealed class EmbeddingOptions
{
    public string? Endpoint { get; set; }
    public string Model { get; set; } = "bge-m3";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);

    public bool HasValidEndpoint => string.IsNullOrWhiteSpace(Endpoint)
        || (Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0);
}
