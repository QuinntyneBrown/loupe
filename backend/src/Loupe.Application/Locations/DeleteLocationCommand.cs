using Loupe.Application.Deletions;
using MediatR;

namespace Loupe.Application.Locations;

public sealed record DeleteLocationCommand(Guid Id, long Revision) : IRequest<DeletionResult>;
