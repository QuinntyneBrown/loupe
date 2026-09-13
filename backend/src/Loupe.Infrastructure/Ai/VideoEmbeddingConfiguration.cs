using Loupe.Application.Videos;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class VideoEmbeddingConfiguration(IOptions<AiOptions> options) : IVideoEmbeddingConfiguration
{
    public string? CurrentModel => options.Value.IsEmbeddingConfigured ? options.Value.EmbeddingModel : null;
}
