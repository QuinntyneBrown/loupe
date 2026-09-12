using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed record RetryScoutingReportCommand(Guid OperationId, long Revision, string OperationKey) : IRequest<OperationResult>;
