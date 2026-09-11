using Loupe.Application.Deletions;
using MediatR;

namespace Loupe.Application.References;

public sealed record DeleteReferenceCommand(Guid Id, long Revision) : IRequest<DeletionResult>;
