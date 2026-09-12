using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed record DeleteReferenceDraftCommand(Guid Id) : IRequest;
