using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed record GetPhotographerSummaryQuery(Guid PhotographerId) : IRequest<OperationResult?>;
