using MediatR;

namespace Loupe.Application.Locations;

public sealed record CreateLocationCommand(LocationDetailsInput Details, string? ScoutingBrief, string? Notes, LocationTagInput[]? Tags, string? OperationKey)
    : IRequest<LocationResult>;
