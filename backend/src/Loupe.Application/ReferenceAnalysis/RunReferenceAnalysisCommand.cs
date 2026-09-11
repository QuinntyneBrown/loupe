using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed record RunReferenceAnalysisCommand : IRequest<bool>;
