using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Scouting;
using Loupe.Domain.Locations;
using Loupe.Domain.Operations;
using Loupe.Domain.Scouting;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ScoutingStore(LibraryDbContext database, TimeProvider clock) : IScoutingStore
{
    public async Task<Guid> AdmitAsync(Guid locationId, string ownerId, long revision, bool regenerate, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        // The receipt owns the transaction. Serialize admissions per owner, then lock the location.
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var location = await database.Locations.FromSqlInterpolated($"SELECT * FROM locations WHERE \"Id\" = {locationId} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .Include(item => item.Images).SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (location.Revision != revision) throw new RevisionConflictException();
        if (location.Images.Count == 0) throw new RequestValidationException("images", "Add an image before requesting a scouting report.");
        var inputJson = JsonSerializer.Serialize(Input(location));
        var active = database.BackgroundOperations.Where(operation => operation.OwnerId == ownerId
            && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running));
        var existing = await active.SingleOrDefaultAsync(operation => operation.Type == OperationType.LocationScouting && operation.ResourceId == locationId, cancellationToken);
        if (existing is not null)
        {
            // A second request while a job is active returns that job, whatever the flag.
            location.CurrentScoutingOperationId = existing.Id;
            await database.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }
        if (!regenerate)
        {
            // An unchanged image set, brief, mode, model, and prompt version reuses the completed report without a provider call.
            var current = location.ScoutingReportJson is null ? null : JsonSerializer.Deserialize<SavedScoutingReport>(location.ScoutingReportJson);
            var currentId = current?.OperationId ?? Guid.Empty;
            var completed = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"OwnerId\" = {ownerId} AND \"ResourceId\" = {locationId} AND \"Type\" = 'LocationScouting' AND \"Status\" = 'Succeeded' AND \"Mode\" = {identity.Mode.ToString()} AND \"Model\" = {identity.Model} AND \"PromptVersion\" = {identity.PromptVersion} AND \"InputJson\" = CAST({inputJson} AS jsonb) AND \"OutputJson\" IS NOT NULL ORDER BY (\"Id\" = {currentId}) DESC, \"CompletedAt\" DESC, \"Id\" LIMIT 1")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (completed is not null)
            {
                if (current?.OperationId != completed.Id)
                {
                    location.ScoutingReportJson = completed.OutputJson;
                    location.Revision++;
                }
                location.CurrentScoutingOperationId = completed.Id;
                await database.SaveChangesAsync(cancellationToken);
                return completed.Id;
            }
        }
        if (await active.CountAsync(cancellationToken) >= 5) throw new AnalysisLimitException();
        var now = clock.GetUtcNow();
        var operation = new BackgroundOperation
        {
            OwnerId = ownerId,
            ResourceId = locationId,
            Type = OperationType.LocationScouting,
            Mode = identity.Mode,
            Model = identity.Model,
            PromptVersion = identity.PromptVersion,
            InputJson = inputJson,
            CreatedAt = now,
            UpdatedAt = now
        };
        database.BackgroundOperations.Add(operation);
        location.CurrentScoutingOperationId = operation.Id;
        await database.SaveChangesAsync(cancellationToken);
        return operation.Id;
    }

    public static ScoutingInput Input(Location location) => new(location.ImageSetRevision, location.ScoutingBrief,
        location.Images.OrderBy(image => image.Position).Select(image => new ScoutingImageInput(image.Id, image.Position, image.PreviewKey, image.Exif)).ToArray());
}
