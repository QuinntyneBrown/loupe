using Loupe.Application.Common;
using Loupe.Application.ReferenceDrafts;
using Loupe.Application.References;
using Loupe.Domain.Boards;
using Loupe.Domain.References;
using Loupe.Domain.Operations;
using Loupe.Application.Operations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceDraftStore(LibraryDbContext database, IReferenceStore references, TimeProvider clock) : IReferenceDraftStore
{
    public async Task<Guid> AdmitSourceAsync(string ownerId, string source, bool configured, CancellationToken cancellationToken)
    {
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var existing = await references.FindSourceOwnedAsync(ownerId, source, cancellationToken);
        var now = clock.GetUtcNow();
        var draft = new ReferenceDraft
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            SourceUrl = source,
            Title = existing?.Title ?? new Uri(source).Host,
            Attribution = existing?.Attribution,
            CommittedReferenceId = existing?.Id,
            ExpiresAt = now.AddHours(24)
        };
        if (existing is null && configured)
        {
            if (await database.BackgroundOperations.CountAsync(operation => operation.OwnerId == ownerId
                && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running), cancellationToken) >= 5)
                throw new AnalysisLimitException();
            var normalizedSource = await database.Database.SqlQuery<string>($"SELECT loupe_normalize_source({source}) AS \"Value\"").SingleAsync(cancellationToken);
            draft.ImportOperation = new BackgroundOperation
            {
                OwnerId = ownerId,
                ResourceId = draft.Id,
                Type = OperationType.ReferenceDraftImport,
                Mode = ExecutionMode.Live,
                Model = "loupe-source-import-v1",
                PromptVersion = "source-import-v1",
                InputJson = JsonSerializer.Serialize(new ReferenceImportInput(source, normalizedSource, null, draft.Revision)),
                CreatedAt = now,
                UpdatedAt = now
            };
            draft.ImportOperationId = draft.ImportOperation.Id;
        }
        else if (existing is null) draft.FailureCode = "integration_not_configured";
        await AddAsync(draft, cancellationToken);
        return draft.Id;
    }
    public async Task AddAsync(ReferenceDraft draft, CancellationToken cancellationToken)
    {
        database.ReferenceDrafts.Add(draft);
        await database.SaveChangesAsync(cancellationToken);
    }
    public async Task<ReferenceDraft> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        await database.ReferenceDrafts.AsNoTracking().Include(draft => draft.ImportOperation).SingleOrDefaultAsync(draft => draft.Id == id && draft.OwnerId == ownerId && draft.ExpiresAt > clock.GetUtcNow(), cancellationToken)
        ?? throw new ResourceNotFoundException();

    public async Task DeleteAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        await database.BackgroundOperations.Where(operation => operation.Type == OperationType.ReferenceDraftImport && operation.ResourceId == id && operation.OwnerId == ownerId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, OperationStatus.Canceled)
                .SetProperty(operation => operation.InputJson, (string?)null).SetProperty(operation => operation.OutputJson, (string?)null)
                .SetProperty(operation => operation.CompletedAt, now).SetProperty(operation => operation.UpdatedAt, now)
                .SetProperty(operation => operation.LeaseToken, (Guid?)null).SetProperty(operation => operation.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.NextAttemptAt, (DateTimeOffset?)null).SetProperty(operation => operation.RetryAvailableAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.Message, "Import canceled."), cancellationToken);
        if (await database.ReferenceDrafts.Where(draft => draft.Id == id && draft.OwnerId == ownerId).ExecuteDeleteAsync(cancellationToken) == 0)
            throw new ResourceNotFoundException();
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task<SaveReferenceUrlResult> CommitAsync(string ownerId, Guid id, long revision, ReferenceMetadata metadata, Guid[] boardIds, CancellationToken cancellationToken)
    {
        var draft = await database.ReferenceDrafts.FromSqlInterpolated($"SELECT * FROM reference_drafts WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .Include(draft => draft.ImportOperation)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (draft.ExpiresAt <= clock.GetUtcNow()) throw new ResourceNotFoundException();
        if (draft.Revision != revision) throw new RevisionConflictException();
        if (draft.ImportOperation?.Status is OperationStatus.Queued or OperationStatus.Running) throw new AnalysisActiveException();
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
