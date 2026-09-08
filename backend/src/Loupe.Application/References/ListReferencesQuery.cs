using MediatR;

namespace Loupe.Application.References;

public sealed record ListReferencesQuery(int PageSize, string? Cursor) : IRequest<ReferencePage>;
