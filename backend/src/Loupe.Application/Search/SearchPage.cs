namespace Loupe.Application.Search;

public sealed record SearchPage(IReadOnlyList<SearchItem> Items, string? NextCursor, int TotalCount);
