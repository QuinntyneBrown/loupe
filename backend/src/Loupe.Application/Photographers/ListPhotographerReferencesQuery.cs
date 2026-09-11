using MediatR;

namespace Loupe.Application.Photographers;

public sealed record ListPhotographerReferencesQuery(Guid Id, int PageSize = 24, string? Cursor = null) : IRequest<PhotographerReferencePage>;
