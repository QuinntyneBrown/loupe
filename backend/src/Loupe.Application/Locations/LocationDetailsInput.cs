namespace Loupe.Application.Locations;

public sealed record LocationDetailsInput(string? Name, string? AddressLine1, string? AddressLine2, string? Locality, string? Region,
    string? PostalCode, string? Country, CoordinatesInput? Coordinates, string? Setting);
