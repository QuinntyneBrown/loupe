using Loupe.Application.Common;

namespace Loupe.Application.Locations;

public static class LocationDetailsValidator
{
    public static LocationDetails Normalize(CreateLocationCommand request) => new(
        TextField.Normalize(request.Name, 200, "name") ?? throw new RequestValidationException("name", "Enter a name."),
        TextField.Normalize(request.AddressLine1, 200, "addressLine1"),
        TextField.Normalize(request.AddressLine2, 200, "addressLine2"),
        TextField.Normalize(request.Locality, 100, "locality"),
        TextField.Normalize(request.Region, 100, "region"),
        TextField.Normalize(request.PostalCode, 20, "postalCode"),
        TextField.Normalize(request.Country, 100, "country"),
        TextField.Normalize(request.ScoutingBrief, 2000, "scoutingBrief"),
        TextField.Normalize(request.Notes, 10000, "notes"));
}
