using MediatR;

namespace Loupe.Application.Photographers;

public sealed record ListReferenceCandidatesQuery(Guid Id, string? Query = null, int PageSize = 24, string? Cursor = null) : IRequest<ReferenceCandidatePage>;
