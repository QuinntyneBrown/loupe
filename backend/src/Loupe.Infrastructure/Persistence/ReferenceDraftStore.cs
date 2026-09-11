using Loupe.Application.Common;
using Loupe.Application.ReferenceDrafts;
using Loupe.Application.References;
using Loupe.Domain.Boards;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;
namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceDraftStore(LibraryDbContext database, IReferenceStore references, TimeProvider clock) : IReferenceDraftStore
{
    public async Task AddAsync(ReferenceDraft draft, CancellationToken cancellationToken)
    {
        database.ReferenceDrafts.Add(draft);
        await database.SaveChangesAsync(cancellationToken);
    }
    public async Task<ReferenceDraft> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        await database.ReferenceDrafts.AsNoTracking().SingleOrDefaultAsync(draft => draft.Id == id && draft.OwnerId == ownerId && draft.ExpiresAt > clock.GetUtcNow(), cancellationToken)
        ?? throw new ResourceNotFoundException();

    public async Task DeleteAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        if (await database.ReferenceDrafts.Where(draft => draft.Id == id && draft.OwnerId == ownerId).ExecuteDeleteAsync(cancellationToken) == 0)
            throw new ResourceNotFoundException();
    }
    public async Task<SaveReferenceUrlResult> CommitAsync(string ownerId, Guid id, long revision, ReferenceMetadata metadata, Guid[] boardIds, CancellationToken cancellationToken)
    {
        var draft = await database.ReferenceDrafts.FromSqlInterpolated($"SELECT * FROM reference_drafts WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (draft.ExpiresAt <= clock.GetUtcNow()) throw new ResourceNotFoundException();
        if (draft.Revision != revision) throw new RevisionConflictException();
        if (draft.CommittedReferenceId is { } savedId)
            return new SaveReferenceUrlResult(ReferenceResult.From(await references.FindOwnedAsync(savedId, ownerId, cancellationToken) ?? throw new ResourceNotFoundException()), true);
        if (await database.Boards.CountAsync(board => board.OwnerId == ownerId && boardIds.Contains(board.Id), cancellationToken) != boardIds.Length)
            throw new ResourceNotFoundException();
        var reference = new Reference
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CreatedAt = clock.GetUtcNow(),
            Title = metadata.Title,
            SourceUrl = metadata.SourceUrl,
            Attribution = metadata.Attribution,
            Notes = metadata.Notes,
            ImageKey = draft.ImageKey,
            PreviewKey = draft.PreviewKey,
            Width = draft.Width,
            Height = draft.Height
        };
        foreach (var boardId in boardIds) reference.Boards.Add(new BoardReference { OwnerId = ownerId, BoardId = boardId, ReferenceId = reference.Id });
        Reference saved;
        if (reference.SourceUrl is not null) saved = await references.SaveSourceAsync(reference, cancellationToken);
        else { await references.SaveAsync(reference, cancellationToken); saved = reference; }
        draft.CommittedReferenceId = saved.Id;
        if (saved.Id == reference.Id) { draft.ImageKey = null; draft.PreviewKey = null; }
        await database.SaveChangesAsync(cancellationToken);
        return new SaveReferenceUrlResult(ReferenceResult.From(saved), saved.Id != reference.Id);
    }
}
