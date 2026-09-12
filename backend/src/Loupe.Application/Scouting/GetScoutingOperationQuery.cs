using Loupe.Application.Operations;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed record GetScoutingOperationQuery(Guid LocationId) : IRequest<OperationResult?>;
