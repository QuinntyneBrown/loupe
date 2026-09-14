using MediatR;

namespace Loupe.Application.ShootPlanning;

public sealed record FindLocationsQuery(string? Query, string Mode = "keyword", string[]? ShootTypes = null, int? People = null, string[]? TimesOfDay = null,
    string? Setting = null, string[]? Tags = null, int PageSize = 24, string? Cursor = null) : IRequest<LocationSearchPage>;
