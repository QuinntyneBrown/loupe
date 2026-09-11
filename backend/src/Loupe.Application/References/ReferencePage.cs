namespace Loupe.Application.References;

public sealed record ReferencePage(IReadOnlyList<ReferenceSummary> Items, string? NextCursor, int TotalCount, int LibraryCount);
