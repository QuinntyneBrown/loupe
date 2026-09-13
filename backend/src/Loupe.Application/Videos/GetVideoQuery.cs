using MediatR;

namespace Loupe.Application.Videos;

public sealed record GetVideoQuery(Guid Id) : IRequest<VideoResult>;
