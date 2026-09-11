using Loupe.Domain.Boards;

namespace Loupe.Application.Boards;

public interface IBoardStore
{
    Task CreateAsync(Board board, CancellationToken cancellationToken);
    Task<IReadOnlyList<BoardResult>> ListAsync(string ownerId, CancellationToken cancellationToken);
}
