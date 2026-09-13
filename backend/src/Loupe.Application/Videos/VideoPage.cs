namespace Loupe.Application.Videos;

public sealed record VideoPage(IReadOnlyList<VideoResult> Items, string? NextCursor, int TotalCount);
