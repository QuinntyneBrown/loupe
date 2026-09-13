namespace Loupe.Application.Videos;

public sealed record VideoSearchResult(IReadOnlyList<VideoMatch> Matches, int TotalCount);
