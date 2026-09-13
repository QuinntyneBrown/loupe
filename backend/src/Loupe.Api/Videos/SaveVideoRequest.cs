using Loupe.Application.Videos;

namespace Loupe.Api.Videos;

public sealed record SaveVideoRequest(string? Title, string? Url, string? Topic, string? Channel, string? Summary, string? Notes, VideoTagInput[]? Tags);
