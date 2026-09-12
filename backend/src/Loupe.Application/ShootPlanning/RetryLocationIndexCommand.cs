using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.ShootPlanning;

public sealed record RetryLocationIndexCommand(Guid OperationId, long Revision, string OperationKey) : IRequest<OperationResult>;
