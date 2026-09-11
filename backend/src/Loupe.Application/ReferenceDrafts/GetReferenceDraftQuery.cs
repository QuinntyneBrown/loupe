using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed record GetReferenceDraftQuery(Guid Id) : IRequest<ReferenceDraftResult>;
