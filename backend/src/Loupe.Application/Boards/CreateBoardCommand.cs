using MediatR;

namespace Loupe.Application.Boards;

public sealed record CreateBoardCommand(string? Name) : IRequest<BoardResult>;
