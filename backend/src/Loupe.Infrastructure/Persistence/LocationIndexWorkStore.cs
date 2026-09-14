using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Search;
using Loupe.Application.ShootPlanning;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LocationIndexWorkStore(LibraryDbContext database, IOperationLeaseStore leases, IEmbeddingConfiguration embeddings, TimeProvider clock) : ILocationIndexWorkStore
{
    public Task<BackgroundOperation?> ClaimAsync(CancellationToken cancellationToken) =>
        leases.ClaimAsync([new(OperationType.LocationIndex, ExecutionMode.Live)], cancellationToken);

    public Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken) => leases.RenewAsync(operation, cancellationToken);

    public async Task<LocationIndexSource?> ReadAsync(BackgroundOperation operation, CancellationToken cancellationToken)
    {
        var location = await database.Locations.AsNoTracking().Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == operation.ResourceId && item.OwnerId == operation.OwnerId, cancellationToken);
        return location is null ? null : new LocationIndexSource(location.Revision, LocationSearchInputBuilder.From(location));
    }

    public async Task PublishAsync(BackgroundOperation operation, LocationIndexSource source, float[] vector, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var current = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"Id\" = {operation.Id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (current is null || current.Status != OperationStatus.Running || current.LeaseToken != operation.LeaseToken || current.LeaseExpiresAt <= now) return;
        var location = await database.Locations.FromSqlInterpolated($"SELECT * FROM locations WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        current.LeaseToken = null;
        current.LeaseExpiresAt = null;
        current.UpdatedAt = now;
        current.CompletedAt = now;
        if (location is null)
        {
            current.Status = OperationStatus.Canceled;
            current.Message = "The location was deleted.";
        }
        else if (location.CurrentIndexOperationId != operation.Id || location.Revision != source.Revision)
        {
            // A newer change owns the next run; this vector would be stale the moment it landed.
            current.Status = OperationStatus.Canceled;
            current.Message = "Superseded by a newer change.";
        }
        else
        {
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO search_vectors ("OwnerId", "ItemType", "ItemId", "ModelIdentity", "SourceRevision", "Vector", "IndexedAt")
                VALUES ({operation.OwnerId}, 'location', {location.Id}, {embeddings.Model}, {source.Revision}, CAST({vector} AS vector), {now})
                ON CONFLICT ("OwnerId", "ItemType", "ItemId") DO UPDATE
                SET "ModelIdentity" = EXCLUDED."ModelIdentity", "SourceRevision" = EXCLUDED."SourceRevision", "Vector" = EXCLUDED."Vector", "IndexedAt" = EXCLUDED."IndexedAt"
                """, cancellationToken);
            current.Status = OperationStatus.Succeeded;
            current.Message = "Search is current.";
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid> RequeueAsync(Guid locationId, string ownerId, CancellationToken cancellationToken)
    {
        // The receipt owns the transaction; lock the location so the intent lands against its current pointer.
        var location = await database.Locations.FromSqlInterpolated($"SELECT * FROM locations WHERE \"Id\" = {locationId} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        await LocationIndexIntent.RecordAsync(database, location, embeddings.Model, clock.GetUtcNow(), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return location.CurrentIndexOperationId!.Value;
    }
}
