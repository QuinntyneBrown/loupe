using System.Text.Json.Serialization;

namespace Loupe.Infrastructure.Ai;

public sealed class OllamaEmbedResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }
    [JsonPropertyName("embeddings")]
    public float[][]? Embeddings { get; init; }
}
