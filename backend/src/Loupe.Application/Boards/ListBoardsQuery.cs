using MediatR;

namespace Loupe.Application.Boards;

public sealed record ListBoardsQuery : IRequest<IReadOnlyList<BoardResult>>;
