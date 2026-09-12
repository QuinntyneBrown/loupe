using System.Text.Json;
using Loupe.Application.Operations;
using Loupe.Application.Scouting;
using Loupe.Domain.Operations;
using Loupe.Domain.Scouting;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ScoutingWorkStore(LibraryDbContext database, IOperationLeaseStore leases, TimeProvider clock) : IScoutingWorkStore
{
    public Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken) => leases.ClaimAsync([new(OperationType.LocationScouting, mode)], cancellationToken);
    public Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken) => leases.RenewAsync(operation, cancellationToken);

    public async Task PublishAsync(BackgroundOperation operation, ScoutingReport report, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var current = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"Id\" = {operation.Id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (current is null || current.Status != OperationStatus.Running || current.LeaseToken != operation.LeaseToken || current.LeaseExpiresAt <= now) return;
        var location = await database.Locations.FromSqlInterpolated($"SELECT * FROM locations WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var input = JsonSerializer.Deserialize<ScoutingInput>(operation.InputJson!)!;
        // A deleted location or a changed image set never receives a stale report.
        if (location is null || location.CurrentScoutingOperationId != operation.Id || location.ImageSetRevision != input.ImageSetRevision) return;
        var saved = new SavedScoutingReport(operation.Id, now, operation.Mode, operation.Model, operation.PromptVersion,
            input.ScoutingBrief, input.ImageSetRevision, input.Images.Count, report);
        var json = ScoutingReportJson.Serialize(saved);
        location.ScoutingReportJson = json;
        location.Revision++;
        current.OutputJson = json;
        current.Status = OperationStatus.Succeeded;
        current.CompletedAt = now;
        current.UpdatedAt = now;
        current.LeaseToken = null;
        current.LeaseExpiresAt = null;
        current.Message = "Scouting report ready.";
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
