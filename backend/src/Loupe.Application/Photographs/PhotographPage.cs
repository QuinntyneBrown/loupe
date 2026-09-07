namespace Loupe.Application.Photographs;

public sealed record PhotographPage(IReadOnlyList<PhotographSummary> Items, string? NextCursor);
