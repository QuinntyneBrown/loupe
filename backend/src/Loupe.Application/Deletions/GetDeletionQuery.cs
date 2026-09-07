using MediatR;

namespace Loupe.Application.Deletions;

public sealed record GetDeletionQuery(Guid Id) : IRequest<DeletionResult>;
