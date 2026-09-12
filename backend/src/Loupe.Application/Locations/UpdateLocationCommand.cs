using MediatR;

namespace Loupe.Application.Locations;

public sealed record UpdateLocationCommand(Guid Id, long Revision, LocationDetailsInput Details) : IRequest<LocationResult>;
