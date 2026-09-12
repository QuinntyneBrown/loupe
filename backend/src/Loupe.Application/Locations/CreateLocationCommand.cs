using MediatR;

namespace Loupe.Application.Locations;

public sealed record CreateLocationCommand(string? Name, string? AddressLine1, string? AddressLine2, string? Locality, string? Region,
    string? PostalCode, string? Country, string? ScoutingBrief, string? Notes, LocationTagInput[]? Tags, string? OperationKey) : IRequest<LocationResult>;
