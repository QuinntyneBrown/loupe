using MediatR;

namespace Loupe.Application.Videos;

public sealed record IndexVideosCommand(int Limit = 20) : IRequest<IndexVideosResult>;
