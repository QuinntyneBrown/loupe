using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.PhotographerDrafts;
using Loupe.Application.Photographers;
using Loupe.Domain.Operations;
using Loupe.Domain.Photographers;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographerDraftStore(LibraryDbContext database, IPhotographerStore photographers, TimeProvider clock) : IPhotographerDraftStore
{
    public async Task<Guid> AdmitAsync(string ownerId, string portfolioUrl, string? name, bool configured, CancellationToken cancellationToken)
    {
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var existing = await photographers.FindSourceOwnedAsync(ownerId, portfolioUrl, cancellationToken);
        var now = clock.GetUtcNow();
        var draft = new PhotographerDraft { Id = Guid.NewGuid(), OwnerId = ownerId, PortfolioUrl = portfolioUrl, Name = existing?.Name ?? name,
            ExpiresAt = now.AddHours(24), CommittedPhotographerId = existing?.Id };
        if (existing is null && configured)
        {
            if (await database.BackgroundOperations.CountAsync(item => item.OwnerId == ownerId && (item.Status == OperationStatus.Queued || item.Status == OperationStatus.Running), cancellationToken) >= 5)
                throw new AnalysisLimitException();
            draft.ImportOperation = new BackgroundOperation { OwnerId = ownerId, ResourceId = draft.Id, Type = OperationType.PhotographerDraftImport,
                Mode = ExecutionMode.Live, Model = "loupe-portfolio-import-v1", PromptVersion = "portfolio-import-v1", InputJson = JsonSerializer.Serialize(portfolioUrl), CreatedAt = now, UpdatedAt = now };
            draft.ImportOperationId = draft.ImportOperation.Id;
        }
        else if (existing is null) draft.FailureCode = "integration_not_configured";
        database.PhotographerDrafts.Add(draft); await database.SaveChangesAsync(cancellationToken); return draft.Id;
    }

    public async Task<PhotographerDraft> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        await database.PhotographerDrafts.AsNoTracking().Include(item => item.ImportOperation).SingleOrDefaultAsync(item => item.OwnerId == ownerId && item.Id == id && !item.Canceled && item.ExpiresAt > clock.GetUtcNow(), cancellationToken)
        ?? throw new ResourceNotFoundException();

    public async Task<SavePhotographerResult> CommitAsync(string ownerId, Guid id, long revision, PhotographerMetadata metadata, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Draft commits require a transaction.");
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var import = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"OwnerId\" = {ownerId} AND \"ResourceId\" = {id} AND \"Type\" = 'PhotographerDraftImport' FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var draft = await database.PhotographerDrafts.FromSqlInterpolated($"SELECT * FROM photographer_drafts WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (draft.Canceled || draft.ExpiresAt <= clock.GetUtcNow()) throw new ResourceNotFoundException();
        if (draft.Revision != revision) throw new RevisionConflictException();
        if (import?.Status is OperationStatus.Queued or OperationStatus.Running) throw new AnalysisActiveException();
        if (draft.CommittedPhotographerId is { } savedId)
            return new(PhotographerResult.From(await photographers.FindOwnedAsync(ownerId, savedId, cancellationToken) ?? throw new ResourceNotFoundException()), true);
        var sameSource = await database.Database.SqlQuery<bool>($"SELECT loupe_normalize_source({draft.PortfolioUrl}) = loupe_normalize_source({metadata.PortfolioUrl}) AS \"Value\"").SingleAsync(cancellationToken);
        var candidate = new Photographer { Id = Guid.NewGuid(), OwnerId = ownerId, Name = metadata.Name, PortfolioUrl = metadata.PortfolioUrl, CreatedAt = clock.GetUtcNow(),
            Summary = metadata.Summary, SummaryProvenance = metadata.Summary is null ? null : "manual", Notes = metadata.Notes,
            SourceJson = sameSource ? draft.SourceJson : null, CapturedSourceRevision = sameSource && draft.SourceJson is not null ? 1 : null,
            SourceFailureCode = sameSource ? draft.FailureCode ?? import?.FailureCode : null };
        foreach (var tag in metadata.Tags) candidate.Tags.Add(new PhotographerTag { PhotographerId = candidate.Id, OwnerId = ownerId,
            Name = tag.Name!, NormalizedName = tag.Name!.ToUpperInvariant(), Category = tag.Category, Provenance = "manual" });
        var saved = await photographers.SaveAsync(candidate, cancellationToken);
        draft.CommittedPhotographerId = saved.Id; await database.SaveChangesAsync(cancellationToken);
        return new(PhotographerResult.From(saved), saved.Id != candidate.Id);
    }

    public async Task CancelAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var now = clock.GetUtcNow();
        await database.BackgroundOperations.Where(item => item.OwnerId == ownerId && item.Type == OperationType.PhotographerDraftImport && item.ResourceId == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, OperationStatus.Canceled).SetProperty(item => item.InputJson, (string?)null)
                .SetProperty(item => item.OutputJson, (string?)null).SetProperty(item => item.CompletedAt, now).SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LeaseToken, (Guid?)null).SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null).SetProperty(item => item.RetryAvailableAt, (DateTimeOffset?)null)
                .SetProperty(item => item.Message, "Page reading canceled."), cancellationToken);
        var draft = await database.PhotographerDrafts.FromSqlInterpolated($"SELECT * FROM photographer_drafts WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (!draft.Canceled) {draft.Canceled = true; draft.Name = null; draft.SourceJson = null; draft.Revision++; await database.SaveChangesAsync(cancellationToken);}
        await transaction.CommitAsync(cancellationToken);
    }
}
