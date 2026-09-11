using Loupe.Domain.Boards;

namespace Loupe.Application.Boards;

public interface IBoardStore
{
    Task CreateAsync(Board board, CancellationToken cancellationToken);
    Task<IReadOnlyList<BoardResult>> ListAsync(string ownerId, CancellationToken cancellationToken);
    Task<BoardResult> RenameAsync(string ownerId, Guid id, long revision, string name, CancellationToken cancellationToken);
    Task DeleteAsync(string ownerId, Guid id, long revision, CancellationToken cancellationToken);
    Task<Loupe.Domain.References.Reference> SetMembershipsAsync(string ownerId, Guid referenceId, long revision, Guid[] boardIds, CancellationToken cancellationToken);
    Task RequireOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken);
}
