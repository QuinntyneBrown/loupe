namespace Loupe.Application.Locations;

public sealed record LocationSummary(Guid Id, string Name, string? Locality, string? CoverPreviewUrl, int ImageCount, string ReportStatus, DateTimeOffset CreatedAt);
