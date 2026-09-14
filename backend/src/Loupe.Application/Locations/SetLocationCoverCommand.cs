using MediatR;

namespace Loupe.Application.Locations;

public sealed record SetLocationCoverCommand(Guid Id, long Revision, Guid ImageId) : IRequest<LocationResult>;
