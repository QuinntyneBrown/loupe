using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class BackgroundOperationStore(LibraryDbContext database, TimeProvider clock) : IBackgroundOperationStore
{
    public Task<BackgroundOperation?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        database.BackgroundOperations.AsNoTracking().SingleOrDefaultAsync(operation => operation.Id == id && operation.OwnerId == ownerId, cancellationToken);

    public async Task<Guid> AdmitCritiqueAsync(Guid photographId, string ownerId, long revision, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var photograph = await database.Photographs.FromSqlInterpolated($"SELECT * FROM photographs WHERE \"Id\" = {photographId} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (photograph.Revision != revision) throw new RevisionConflictException();
        var inputJson = JsonSerializer.Serialize(new CritiqueInput(photograph.ImageKey, photograph.Brief, photograph.Exif));
        var active = database.BackgroundOperations.Where(operation => operation.OwnerId == ownerId
            && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running));
        var existing = await active.SingleOrDefaultAsync(operation => operation.Type == OperationType.Critique
            && operation.ResourceId == photographId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Mode == identity.Mode && existing.Model == identity.Model && existing.PromptVersion == identity.PromptVersion
                && JsonSerializer.Serialize(JsonSerializer.Deserialize<CritiqueInput>(existing.InputJson!)) == inputJson)
                return existing.Id;
            throw new AnalysisActiveException();
        }
        if (await active.CountAsync(cancellationToken) >= 5) throw new AnalysisLimitException();
        var now = clock.GetUtcNow();
        var operation = new BackgroundOperation
        {
            OwnerId = ownerId,
            ResourceId = photographId,
            Type = OperationType.Critique,
            Mode = identity.Mode,
            Model = identity.Model,
            PromptVersion = identity.PromptVersion,
            InputJson = inputJson,
            CreatedAt = now,
            UpdatedAt = now
        };
        database.BackgroundOperations.Add(operation);
        await database.SaveChangesAsync(cancellationToken);
        return operation.Id;
    }
}
