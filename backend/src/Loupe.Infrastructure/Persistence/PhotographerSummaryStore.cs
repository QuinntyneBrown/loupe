using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.PhotographerSummaries;
using Loupe.Domain.Operations;
using Loupe.Domain.Photographers;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographerSummaryStore(LibraryDbContext database, TimeProvider clock) : IPhotographerSummaryStore
{
    public async Task<Guid> AdmitAsync(Guid id, string ownerId, long revision, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var photographer = await database.Photographers.FromSqlInterpolated($"SELECT * FROM photographers WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (photographer.Revision != revision) throw new RevisionConflictException();
        var active = database.BackgroundOperations.Where(operation => operation.OwnerId == ownerId && operation.Type != OperationType.LocationIndex && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running));
        var existing = await active.SingleOrDefaultAsync(operation => operation.Type == OperationType.PhotographerSummary && operation.ResourceId == id, cancellationToken);
        if (existing is not null)
        {
            var input = JsonSerializer.Deserialize<PhotographerSummaryInput>(existing.InputJson!);
            if (input?.SourceRevision != photographer.SourceRevision || existing.Mode != identity.Mode || existing.Model != identity.Model || existing.PromptVersion != identity.PromptVersion) throw new AnalysisActiveException();
            photographer.CurrentSummaryOperationId = existing.Id; await database.SaveChangesAsync(cancellationToken); return existing.Id;
        }
        var now = clock.GetUtcNow();
        if (photographer.CurrentSummaryOperationId is { } previousId)
        {
            var previous = await database.BackgroundOperations.AsNoTracking().SingleOrDefaultAsync(operation => operation.Id == previousId && operation.OwnerId == ownerId, cancellationToken);
            if (previous?.RetryAvailableAt is { } available && available > now) throw new RetryNotReadyException(available - now);
        }
        if (await active.CountAsync(cancellationToken) >= 5) throw new AnalysisLimitException();
        var source = photographer.CapturedSourceRevision == photographer.SourceRevision && photographer.SourceJson is not null
            ? JsonSerializer.Deserialize<CapturedPortfolioPage>(photographer.SourceJson) : null;
        var operation = new BackgroundOperation
        {
            OwnerId = ownerId, ResourceId = id, Type = OperationType.PhotographerSummary, Mode = identity.Mode,
            Model = identity.Model, PromptVersion = identity.PromptVersion, CreatedAt = now, UpdatedAt = now,
            InputJson = JsonSerializer.Serialize(new PhotographerSummaryInput(photographer.PortfolioUrl, photographer.SourceRevision, source))
        };
        database.BackgroundOperations.Add(operation); photographer.CurrentSummaryOperationId = operation.Id;
        await database.SaveChangesAsync(cancellationToken); return operation.Id;
    }
}
