using Loupe.Application.References;
using MediatR;
namespace Loupe.Application.Boards;
public sealed record SetReferenceBoardsCommand(Guid ReferenceId, long Revision, Guid[]? BoardIds) : IRequest<ReferenceResult>;
