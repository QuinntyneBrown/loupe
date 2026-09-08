using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.ReferenceImports;
using Loupe.Domain.Operations;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;
namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceImportStore(LibraryDbContext database, TimeProvider clock) : IReferenceImportStore
{
    public async Task<Guid> AdmitAsync(Guid referenceId, string ownerId, long revision, ExecutionMode mode, CancellationToken cancellationToken)
    {
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var reference = await database.References.FromSqlInterpolated($"SELECT * FROM \"references\" WHERE \"Id\" = {referenceId} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        var source = reference.SourceUrl ?? throw new RequestValidationException("sourceUrl", "Add a source URL before requesting an import.");
        var normalizedSource = await database.Database.SqlQuery<string>($"SELECT loupe_normalize_source({source}) AS \"Value\"").SingleAsync(cancellationToken);
        var active = database.BackgroundOperations.Where(operation => operation.OwnerId == ownerId
            && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running));
        var existing = await active.SingleOrDefaultAsync(operation => operation.Type == OperationType.ReferenceImport && operation.ResourceId == referenceId, cancellationToken);
        if (existing is not null)
        {
            var input = JsonSerializer.Deserialize<ReferenceImportInput>(existing.InputJson!);
            if (existing.Mode == mode && input?.NormalizedSource == normalizedSource && input.ImageKey == reference.ImageKey)
            {
                reference.CurrentImportOperationId = existing.Id;
                await database.SaveChangesAsync(cancellationToken);
                return existing.Id;
            }
            throw new AnalysisActiveException();
        }
        if (await active.CountAsync(cancellationToken) >= 5) throw new AnalysisLimitException();
        var now = clock.GetUtcNow();
        var operation = new BackgroundOperation
        {
            OwnerId = ownerId,
            ResourceId = referenceId,
            Type = OperationType.ReferenceImport,
            Mode = mode,
            Model = "loupe-source-import-v1",
            PromptVersion = "source-import-v1",
            InputJson = JsonSerializer.Serialize(new ReferenceImportInput(source, normalizedSource, reference.ImageKey, reference.Revision)),
            CreatedAt = now,
            UpdatedAt = now
        };
        database.BackgroundOperations.Add(operation);
        reference.CurrentImportOperationId = operation.Id;
        await database.SaveChangesAsync(cancellationToken);
        return operation.Id;
    }
}
