using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed record RequestCritiqueCommand(Guid PhotographId, long Revision, bool Regenerate, string? OperationKey) : IRequest<OperationResult>;
