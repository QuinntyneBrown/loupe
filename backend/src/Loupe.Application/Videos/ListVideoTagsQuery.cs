using MediatR;

namespace Loupe.Application.Videos;

public sealed record ListVideoTagsQuery : IRequest<IReadOnlyList<VideoTagCount>>;
