using Loupe.Application.Boards;
using Loupe.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Loupe.Infrastructure.Persistence;

public sealed class BoardStore(LibraryDbContext database) : IBoardStore
{
    public async Task CreateAsync(Board board, CancellationToken cancellationToken)
    {
        database.Boards.Add(board);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_boards_OwnerId_NormalizedName" })
        { throw new BoardNameConflictException(); }
    }

    public async Task<IReadOnlyList<BoardResult>> ListAsync(string ownerId, CancellationToken cancellationToken)
    {
        var boards = await database.Boards.AsNoTracking().Where(board => board.OwnerId == ownerId)
            .Select(board => new BoardResult(board.Id, board.Name, board.Revision, board.References.Count))
            .ToListAsync(cancellationToken);
        return boards.OrderBy(board => board.Name, StringComparer.OrdinalIgnoreCase).ThenBy(board => board.Id).ToArray();
    }
}
