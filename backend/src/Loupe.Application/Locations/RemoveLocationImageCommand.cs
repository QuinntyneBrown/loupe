using MediatR;

namespace Loupe.Application.Locations;

public sealed record RemoveLocationImageCommand(Guid Id, Guid ImageId, long Revision) : IRequest<LocationResult>;
