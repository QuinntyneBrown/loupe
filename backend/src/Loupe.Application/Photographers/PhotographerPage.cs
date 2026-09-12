namespace Loupe.Application.Photographers;

public sealed record PhotographerPage(IReadOnlyList<PhotographerSummary> Items, string? NextCursor, int TotalCount);
