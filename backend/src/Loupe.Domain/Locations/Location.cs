namespace Loupe.Domain.Locations;

public sealed class Location
{
    public required Guid Id { get; init; }
    public required string OwnerId { get; init; }
    public required string Name { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Locality { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public LocationSetting? Setting { get; set; }
    public string? ScoutingBrief { get; set; }
    public string? Notes { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public Guid? CoverImageId { get; set; }
    public long ImageSetRevision { get; set; } = 1;
    public long Revision { get; set; } = 1;
    public ICollection<LocationTag> Tags { get; } = new List<LocationTag>();
    public ICollection<LocationImage> Images { get; } = new List<LocationImage>();
}
