using System.Globalization;
using Loupe.Application.Common;
using Loupe.Domain.Locations;

namespace Loupe.Application.Locations;

public static class LocationDetailsValidator
{
    public static LocationDetails Normalize(CreateLocationCommand request)
    {
        var (latitude, longitude) = Coordinates(request.Coordinates);
        return new(
            TextField.Normalize(request.Name, 200, "name") ?? throw new RequestValidationException("name", "Enter a name."),
            TextField.Normalize(request.AddressLine1, 200, "addressLine1"),
            TextField.Normalize(request.AddressLine2, 200, "addressLine2"),
            TextField.Normalize(request.Locality, 100, "locality"),
            TextField.Normalize(request.Region, 100, "region"),
            TextField.Normalize(request.PostalCode, 20, "postalCode"),
            TextField.Normalize(request.Country, 100, "country"),
            latitude, longitude, Setting(request.Setting),
            TextField.Normalize(request.ScoutingBrief, 2000, "scoutingBrief"),
            TextField.Normalize(request.Notes, 10000, "notes"));
    }

    private static (decimal? Latitude, decimal? Longitude) Coordinates(CoordinatesInput? input)
    {
        var latitude = input?.Latitude?.Trim();
        var longitude = input?.Longitude?.Trim();
        if (string.IsNullOrEmpty(latitude) && string.IsNullOrEmpty(longitude)) return (null, null);
        if (string.IsNullOrEmpty(latitude)) throw new RequestValidationException("latitude", "Enter a latitude to go with the longitude.");
        if (string.IsNullOrEmpty(longitude)) throw new RequestValidationException("longitude", "Enter a longitude to go with the latitude.");
        return (Degrees(latitude, 90, "latitude"), Degrees(longitude, 180, "longitude"));
    }

    private static decimal Degrees(string value, decimal limit, string field)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var degrees)
            || degrees.Scale > 6)
            throw new RequestValidationException(field, "Enter decimal degrees with up to six decimal places.");
        return Math.Abs(degrees) <= limit ? degrees : throw new RequestValidationException(field, $"Enter a value between -{limit} and {limit}.");
    }

    private static LocationSetting? Setting(string? value) => value?.Trim() switch
    {
        null or "" => null,
        var name when Enum.TryParse<LocationSetting>(name, ignoreCase: true, out var setting) => setting,
        _ => throw new RequestValidationException("setting", "Choose Indoor, Outdoor, or Mixed.")
    };
}
