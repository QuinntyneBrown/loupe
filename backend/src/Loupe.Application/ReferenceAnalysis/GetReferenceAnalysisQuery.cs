using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed record GetReferenceAnalysisQuery(Guid ReferenceId) : IRequest<OperationResult?>;
