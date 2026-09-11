using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed record ImportReferenceDraftCommand(string? SourceUrl, string? OperationKey) : IRequest<ReferenceDraftResult>;
