using MediatR;
namespace Loupe.Application.Boards;
public sealed record RenameBoardCommand(Guid Id, long Revision, string? Name) : IRequest<BoardResult>;
