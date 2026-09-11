using Loupe.Application.Deletions;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed record DeletePhotographerCommand(Guid Id, long Revision) : IRequest<DeletionResult>;
