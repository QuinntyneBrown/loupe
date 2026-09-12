using Loupe.Application.Locations;

namespace Loupe.Api.Locations;

public sealed record CreateLocationRequest(string? Name, string? AddressLine1, string? AddressLine2, string? Locality, string? Region,
    string? PostalCode, string? Country, CoordinatesInput? Coordinates, string? Setting, string? ScoutingBrief, string? Notes, LocationTagInput[]? Tags);
