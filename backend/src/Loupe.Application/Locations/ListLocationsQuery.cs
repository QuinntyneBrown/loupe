using MediatR;

namespace Loupe.Application.Locations;

public sealed record ListLocationsQuery(int PageSize, string? Cursor) : IRequest<LocationPage>;
