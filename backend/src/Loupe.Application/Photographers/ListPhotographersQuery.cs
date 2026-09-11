using MediatR;

namespace Loupe.Application.Photographers;

public sealed record ListPhotographersQuery(int PageSize = 24, string? Cursor = null) : IRequest<PhotographerPage>;
