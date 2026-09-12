namespace Loupe.Application.Locations;

public sealed record LocationPage(IReadOnlyList<LocationSummary> Items, string? NextCursor, int TotalCount);
