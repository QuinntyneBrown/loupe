using MediatR;

namespace Loupe.Application.Operations;

public sealed record GetOperationQuery(Guid Id) : IRequest<OperationResult>;
