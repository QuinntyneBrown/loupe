using System.Text.Json.Serialization;

namespace Loupe.Application.ShootPlanning;

public sealed record LocationSearchItem(Guid Id, string Name, string? Locality, string? CoverPreviewUrl, int ImageCount, string ReportStatus,
    IReadOnlyList<string> RecommendedPeriods, LocationSearchGroupSize? GroupSize, IReadOnlyList<LocationSearchSuitability> Suitability, DateTimeOffset CreatedAt,
    [property: JsonIgnore] double? Score = null);
