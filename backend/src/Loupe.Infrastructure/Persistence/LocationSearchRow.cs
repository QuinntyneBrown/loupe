namespace Loupe.Infrastructure.Persistence;

/// <summary>One keyword search hit as read from SQL; the report fields are decoded in memory.</summary>
public sealed class LocationSearchRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Locality { get; init; }
    public Guid? CoverImageId { get; init; }
    public int ImageCount { get; init; }
    public string? OperationStatus { get; init; }
    public string? ScoutingReportJson { get; init; }
    public long ImageSetRevision { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
