using Loupe.Application.Photographs;
using MediatR;

namespace Loupe.Application.Comparisons;

public sealed record ListComparisonCandidatesQuery(int PageSize, string? Cursor) : IRequest<PhotographPage>;
