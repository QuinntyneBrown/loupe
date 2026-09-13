namespace Loupe.Application.Videos;

public sealed record VideoMetadata(string Title, string VideoId, string Topic, string? Channel, string? Summary, string? Notes, VideoTagInput[] Tags);
