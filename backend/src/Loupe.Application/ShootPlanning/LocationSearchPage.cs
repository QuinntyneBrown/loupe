namespace Loupe.Application.ShootPlanning;

public sealed record LocationSearchPage(IReadOnlyList<LocationSearchItem> Items, string? NextCursor, int TotalCount);
