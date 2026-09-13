using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Videos;

public sealed class IndexVideosCommandHandler(IVideoEmbeddingConfiguration configuration, IVideoEmbeddingProvider provider, IVideoIndexStore index)
    : IRequestHandler<IndexVideosCommand, IndexVideosResult>
{
    public async Task<IndexVideosResult> Handle(IndexVideosCommand request, CancellationToken cancellationToken)
    {
        if (configuration.CurrentModel is not { } model) return new(0, null);
        var indexed = 0;
        while (indexed < request.Limit)
        {
            try
            {
                if (!await index.IndexNextAsync(model, (video, token) => provider.EmbedAsync(VideoEmbedding.Text(video), token), cancellationToken)) break;
            }
            catch (ProviderFailureException failure) { return new(indexed, failure.Kind); }
            indexed++;
        }
        return new(indexed, null);
    }
}
