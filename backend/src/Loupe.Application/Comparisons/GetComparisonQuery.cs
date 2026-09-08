using MediatR;

namespace Loupe.Application.Comparisons;

public sealed record GetComparisonQuery(Guid FirstId, Guid SecondId) : IRequest<ComparisonResult>;
