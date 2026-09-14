using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Domain.Operations;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceAnalysisStore(LibraryDbContext database, TimeProvider clock) : IReferenceAnalysisStore
{
    public async Task<Guid> AdmitAsync(Guid id, string ownerId, long revision, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var reference = await database.References.FromSqlInterpolated($"SELECT * FROM \"references\" WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        if (reference.ImageKey is null || reference.PreviewKey is null) throw new RequestValidationException("image", "Add an image before requesting visual suggestions.");
        var active = database.BackgroundOperations.Where(operation => operation.OwnerId == ownerId && operation.Type != OperationType.LocationIndex && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running));
        var existing = await active.SingleOrDefaultAsync(operation => operation.Type == OperationType.ReferenceAnalysis && operation.ResourceId == id, cancellationToken);
        if (existing is not null)
        {
            var input = JsonSerializer.Deserialize<ReferenceAnalysisInput>(existing.InputJson!);
            if (input?.ImageRevision != reference.ImageRevision || input.ImageKey != reference.ImageKey || existing.Model != identity.Model) throw new AnalysisActiveException();
            reference.CurrentAnalysisOperationId = existing.Id; await database.SaveChangesAsync(cancellationToken); return existing.Id;
        }
        if (await active.CountAsync(cancellationToken) >= 5) throw new AnalysisLimitException();
        var now = clock.GetUtcNow();
        var operation = new BackgroundOperation
        {
            OwnerId = ownerId, ResourceId = id, Type = OperationType.ReferenceAnalysis, Mode = identity.Mode,
            Model = identity.Model, PromptVersion = identity.PromptVersion, CreatedAt = now, UpdatedAt = now,
            InputJson = JsonSerializer.Serialize(new ReferenceAnalysisInput(reference.ImageKey, reference.PreviewKey, reference.ImageRevision))
        };
        database.BackgroundOperations.Add(operation); reference.CurrentAnalysisOperationId = operation.Id;
        await database.SaveChangesAsync(cancellationToken); return operation.Id;
    }
}
