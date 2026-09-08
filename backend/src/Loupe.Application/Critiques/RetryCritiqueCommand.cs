using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed record RetryCritiqueCommand(Guid OperationId, long Revision, string? OperationKey) : IRequest<OperationResult>;
