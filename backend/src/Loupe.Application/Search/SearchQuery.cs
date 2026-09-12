using MediatR;

namespace Loupe.Application.Search;

public sealed record SearchQuery(string? Query, string Type = "all", string[]? Tags = null, Guid[]? BoardIds = null, int PageSize = 24, string? Cursor = null, string Mode = "keyword") : IRequest<SearchPage>;
