using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Videos;

public sealed class GetVideoQueryHandler(ICurrentOwner owner, IVideoStore videos, IVideoEmbeddingConfiguration embeddings) : IRequestHandler<GetVideoQuery, VideoResult>
{
    public async Task<VideoResult> Handle(GetVideoQuery request, CancellationToken cancellationToken) =>
        VideoResult.From(await videos.FindOwnedAsync(owner.Id, request.Id, cancellationToken) ?? throw new ResourceNotFoundException(), embeddings.CurrentModel);
}
