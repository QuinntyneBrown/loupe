namespace Loupe.Application.Locations;

public sealed record LocationDetails(string Name, string? AddressLine1, string? AddressLine2, string? Locality, string? Region,
    string? PostalCode, string? Country, string? ScoutingBrief, string? Notes);
