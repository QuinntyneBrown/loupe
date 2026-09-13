using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Videos;

public sealed class ListVideoTagsQueryHandler(ICurrentOwner owner, IVideoStore videos) : IRequestHandler<ListVideoTagsQuery, IReadOnlyList<VideoTagCount>>
{
    public Task<IReadOnlyList<VideoTagCount>> Handle(ListVideoTagsQuery request, CancellationToken cancellationToken) => videos.ListTagsAsync(owner.Id, cancellationToken);
}
