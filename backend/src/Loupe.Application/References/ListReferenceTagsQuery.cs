using MediatR;

namespace Loupe.Application.References;

public sealed record ListReferenceTagsQuery(Guid? BoardId) : IRequest<IReadOnlyList<ReferenceTagFacet>>;
