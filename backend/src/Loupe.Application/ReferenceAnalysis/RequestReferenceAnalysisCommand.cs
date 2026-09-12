using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed record RequestReferenceAnalysisCommand(Guid ReferenceId, long Revision, string? OperationKey) : IRequest<OperationResult>;
