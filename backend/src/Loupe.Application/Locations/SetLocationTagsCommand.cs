using MediatR;

namespace Loupe.Application.Locations;

public sealed record SetLocationTagsCommand(Guid Id, long Revision, LocationTagInput[]? Tags) : IRequest<LocationResult>;
