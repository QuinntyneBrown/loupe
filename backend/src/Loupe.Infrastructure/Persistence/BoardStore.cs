using Loupe.Application.Boards;
using Loupe.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Loupe.Application.Common;
using Loupe.Domain.References;

namespace Loupe.Infrastructure.Persistence;

public sealed class BoardStore(LibraryDbContext database) : IBoardStore
{
    public async Task RequireOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        if (!await database.Boards.AnyAsync(board => board.Id == id && board.OwnerId == ownerId, cancellationToken))
            throw new ResourceNotFoundException();
    }

    public async Task<BoardResult> RenameAsync(string ownerId, Guid id, long revision, string name, CancellationToken cancellationToken)
    {
        var board = await database.Boards.Include(item => item.References).SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (board.Revision != revision) throw new RevisionConflictException();
        board.Name = name;
        board.NormalizedName = name.ToUpperInvariant();
        board.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_boards_OwnerId_NormalizedName" })
        { throw new BoardNameConflictException(); }
        return new BoardResult(board.Id, board.Name, board.Revision, board.References.Count);
    }

    public async Task DeleteAsync(string ownerId, Guid id, long revision, CancellationToken cancellationToken)
    {
        var board = await database.Boards.SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (board.Revision != revision) throw new RevisionConflictException();
        database.Boards.Remove(board);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
    }

    public async Task<Reference> SetMembershipsAsync(string ownerId, Guid referenceId, long revision, Guid[] boardIds, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var reference = await database.References.Include(item => item.Boards).Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == referenceId && item.OwnerId == ownerId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        if (await database.Boards.CountAsync(board => board.OwnerId == ownerId && boardIds.Contains(board.Id), cancellationToken) != boardIds.Length)
            throw new ResourceNotFoundException();
        var current = reference.Boards.Select(item => item.BoardId).ToHashSet();
        if (current.SetEquals(boardIds)) return reference;
        foreach (var membership in reference.Boards.Where(item => !boardIds.Contains(item.BoardId)).ToArray())
            reference.Boards.Remove(membership);
        foreach (var boardId in boardIds.Where(id => !current.Contains(id)))
            reference.Boards.Add(new BoardReference { OwnerId = ownerId, ReferenceId = referenceId, BoardId = boardId });
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        { throw new ResourceNotFoundException(); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new RevisionConflictException(); }
        await transaction.CommitAsync(cancellationToken);
        return reference;
    }

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
