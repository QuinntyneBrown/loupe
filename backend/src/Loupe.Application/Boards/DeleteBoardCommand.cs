using MediatR;
namespace Loupe.Application.Boards;
public sealed record DeleteBoardCommand(Guid Id, long Revision) : IRequest;
