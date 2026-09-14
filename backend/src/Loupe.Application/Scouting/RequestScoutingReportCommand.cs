using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed record RequestScoutingReportCommand(Guid LocationId, long Revision, bool Regenerate, string? OperationKey) : IRequest<OperationResult>;
