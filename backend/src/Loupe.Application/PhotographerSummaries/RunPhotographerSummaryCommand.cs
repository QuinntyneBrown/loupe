using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed record RunPhotographerSummaryCommand : IRequest<bool>;
