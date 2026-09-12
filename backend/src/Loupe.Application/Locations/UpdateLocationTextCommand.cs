using MediatR;

namespace Loupe.Application.Locations;

public sealed record UpdateLocationTextCommand(Guid Id, long Revision, LocationTextField Field, string? Text) : IRequest<LocationResult>;
