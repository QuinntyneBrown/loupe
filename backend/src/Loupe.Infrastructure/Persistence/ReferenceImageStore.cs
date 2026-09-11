using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.References;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceImageStore(LibraryDbContext database, IImageStore images, TimeProvider clock) : IReferenceImageStore
{
    public async Task ReplaceAsync(Guid id, string ownerId, long revision, ProcessedImage image, CancellationToken cancellationToken)
    {
        // The receipt owns the transaction and media lock. Lock operations before their resource, as the worker does.
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Image replacement requires a transaction.");
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        await database.BackgroundOperations.Where(operation => operation.ResourceId == id && operation.OwnerId == ownerId
            && (operation.Type == OperationType.ReferenceImport || operation.Type == OperationType.ReferenceAnalysis)
            && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running))
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, OperationStatus.Canceled)
                .SetProperty(operation => operation.LeaseToken, (Guid?)null)
                .SetProperty(operation => operation.InputJson, (string?)null).SetProperty(operation => operation.OutputJson, (string?)null)
                .SetProperty(operation => operation.NextAttemptAt, (DateTimeOffset?)null).SetProperty(operation => operation.RetryAvailableAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.Message, "The image was replaced.")
                .SetProperty(operation => operation.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.CompletedAt, clock.GetUtcNow())
                .SetProperty(operation => operation.UpdatedAt, clock.GetUtcNow()), cancellationToken);
        var reference = await database.References.FromSqlInterpolated($"SELECT * FROM \"references\" WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        reference.ImageKey = await images.WriteAsync(image.Image, cancellationToken);
        reference.PreviewKey = await images.WriteAsync(image.Preview, cancellationToken);
        reference.Width = image.Width;
        reference.Height = image.Height;
        reference.CurrentImportOperationId = null;
        reference.CurrentAnalysisOperationId = null;
        reference.Revision++;
        reference.ImageRevision++;
        await database.SaveChangesAsync(cancellationToken);
        // Old and uncommitted media are removed by reference-aware orphan cleanup.
    }
}
