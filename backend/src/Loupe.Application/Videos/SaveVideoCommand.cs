using MediatR;

namespace Loupe.Application.Videos;

public sealed record SaveVideoCommand(string? Title, string? Url, string? Topic, string? Channel, string? Summary, string? Notes, VideoTagInput[]? Tags) : IRequest<VideoResult>;
