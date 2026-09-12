using Loupe.Application.Search;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Ai;

public sealed class EmbeddingConfiguration(IOptions<EmbeddingOptions> options) : IEmbeddingConfiguration
{
    public bool IsConfigured => options.Value.IsConfigured;
    public string Model => options.Value.Model;
}
