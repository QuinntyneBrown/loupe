using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed record RequestPhotographerSummaryCommand(Guid PhotographerId, long Revision, string? OperationKey) : IRequest<OperationResult>;
