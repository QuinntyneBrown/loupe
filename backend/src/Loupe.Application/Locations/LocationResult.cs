using System.Globalization;
using Loupe.Domain.Locations;

namespace Loupe.Application.Locations;

public sealed record LocationResult(Guid Id, string Name, string? AddressLine1, string? AddressLine2, string? Locality, string? Region,
    string? PostalCode, string? Country, Coordinates? Coordinates, LocationSetting? Setting, string? ScoutingBrief, string? Notes,
    IReadOnlyList<LocationTagResult> Tags, IReadOnlyList<LocationImageResult> Images, Guid? CoverImageId, object? Report,
    string ReportStatus, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Revision)
{
    public static LocationResult From(Location location) => new(location.Id, location.Name, location.AddressLine1, location.AddressLine2,
        location.Locality, location.Region, location.PostalCode, location.Country,
        location is { Latitude: { } latitude, Longitude: { } longitude } ? new Coordinates(Format(latitude), Format(longitude)) : null,
        location.Setting, location.ScoutingBrief, location.Notes,
        location.Tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).Select(tag => new LocationTagResult(tag.Name, tag.Category)).ToArray(),
        location.Images.OrderBy(image => image.Position).Select(image => LocationImageResult.From(location.Id, image)).ToArray(),
        location.CoverImageId, null, "None", location.CreatedAt, location.UpdatedAt, location.Revision);

    private static string Format(decimal value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
}
