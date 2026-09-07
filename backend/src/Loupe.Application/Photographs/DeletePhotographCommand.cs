using Loupe.Application.Deletions;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed record DeletePhotographCommand(Guid Id, long Revision) : IRequest<DeletionResult>;
