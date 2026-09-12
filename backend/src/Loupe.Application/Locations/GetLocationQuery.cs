using MediatR;

namespace Loupe.Application.Locations;

public sealed record GetLocationQuery(Guid Id) : IRequest<LocationResult>;
