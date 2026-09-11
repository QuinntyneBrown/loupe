namespace Loupe.Application.Photographers;

public sealed record PhotographerPage(IReadOnlyList<PhotographerResult> Items, string? NextCursor, int TotalCount);
