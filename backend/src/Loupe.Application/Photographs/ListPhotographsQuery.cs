using MediatR;

namespace Loupe.Application.Photographs;

public sealed record ListPhotographsQuery(int PageSize = 24, string? Cursor = null) : IRequest<PhotographPage>;
