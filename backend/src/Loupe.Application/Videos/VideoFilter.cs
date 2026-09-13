namespace Loupe.Application.Videos;

public sealed record VideoFilter(string Query, string? Topic, string[] Tags);
