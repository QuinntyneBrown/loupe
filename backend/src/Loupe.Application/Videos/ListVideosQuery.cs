using MediatR;

namespace Loupe.Application.Videos;

public sealed record ListVideosQuery(string? Query = null, string? Topic = null, string[]? Tags = null, int PageSize = 24, string? Cursor = null, string Mode = "keyword") : IRequest<VideoPage>;
