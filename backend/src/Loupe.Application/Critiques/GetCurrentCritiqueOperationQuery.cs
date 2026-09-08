using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed record GetCurrentCritiqueOperationQuery(Guid PhotographId) : IRequest<OperationResult?>;
