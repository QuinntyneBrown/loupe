using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Domain.Locations;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LocationStore(LibraryDbContext context, TimeProvider clock) : ILocationStore
{
    public async Task SaveAsync(Location location, CancellationToken cancellationToken)
    {
        context.Locations.Add(location);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Location?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        context.Locations.AsNoTracking().Include(location => location.Tags).Include(location => location.Images).Include(location => location.CurrentScoutingOperation)
            .SingleOrDefaultAsync(location => location.Id == id && location.OwnerId == ownerId, cancellationToken);

    public Task<int> CountAsync(string ownerId, CancellationToken cancellationToken) =>
        context.Locations.CountAsync(location => location.OwnerId == ownerId, cancellationToken);

    public async Task<IReadOnlyList<LocationSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var query = context.Locations.AsNoTracking().Where(location => location.OwnerId == ownerId);
        if (cursor is not null) query = query.Where(location => location.CreatedAt < cursor.CreatedAt
            || location.CreatedAt == cursor.CreatedAt && location.Id.CompareTo(cursor.Id) > 0);
        return await query.OrderByDescending(location => location.CreatedAt).ThenBy(location => location.Id).Take(count)
            .Select(location => new LocationSummary(location.Id, location.Name, location.Locality,
                location.CoverImageId == null ? null : LocationImageUrls.Preview(location.Id, location.CoverImageId.Value),
                location.Images.Count,
                location.CurrentScoutingOperation != null && location.CurrentScoutingOperation.Status == OperationStatus.Queued ? "Queued"
                : location.CurrentScoutingOperation != null && location.CurrentScoutingOperation.Status == OperationStatus.Running ? "Running"
                : location.CurrentScoutingOperation != null && location.CurrentScoutingOperation.Status == OperationStatus.Failed ? "Failed"
                : location.ScoutingReportJson == null ? "None" : "Ready", location.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<Location> UpdateAsync(Guid id, string ownerId, long revision, LocationDetails details, CancellationToken cancellationToken) =>
        EditAsync(id, ownerId, revision, location =>
        {
            location.Name = details.Name;
            location.AddressLine1 = details.AddressLine1;
            location.AddressLine2 = details.AddressLine2;
            location.Locality = details.Locality;
            location.Region = details.Region;
            location.PostalCode = details.PostalCode;
            location.Country = details.Country;
            location.Latitude = details.Latitude;
            location.Longitude = details.Longitude;
            location.Setting = details.Setting;
        }, cancellationToken);

    public Task<Location> UpdateTextAsync(Guid id, string ownerId, long revision, LocationTextField field, string? text, CancellationToken cancellationToken) =>
        EditAsync(id, ownerId, revision, location =>
        {
            if (field == LocationTextField.ScoutingBrief) location.ScoutingBrief = text;
            else location.Notes = text;
        }, cancellationToken);

    public Task<Location> ReplaceTagsAsync(Guid id, string ownerId, long revision, IReadOnlyList<LocationTagInput> tags, CancellationToken cancellationToken) =>
        EditAsync(id, ownerId, revision, location =>
        {
            var requested = tags.ToDictionary(tag => tag.Name!.ToUpperInvariant());
            foreach (var tag in location.Tags.Where(tag => !requested.ContainsKey(tag.NormalizedName)).ToArray()) location.Tags.Remove(tag);
            foreach (var (key, input) in requested)
            {
                var existing = location.Tags.SingleOrDefault(tag => tag.NormalizedName == key);
                if (existing is null)
                    location.Tags.Add(new LocationTag { LocationId = id, OwnerId = ownerId, NormalizedName = key, Name = input.Name!, Category = input.Category });
                else
                {
                    existing.Name = input.Name!;
                    existing.Category = input.Category;
                }
            }
        }, cancellationToken);

    private async Task<Location> EditAsync(Guid id, string ownerId, long revision, Action<Location> apply, CancellationToken cancellationToken)
    {
        var location = await context.Locations.Include(item => item.Tags).Include(item => item.Images).Include(item => item.CurrentScoutingOperation)
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken) ?? throw new ResourceNotFoundException();
        if (location.Revision != revision) throw new RevisionConflictException();
        apply(location);
        location.UpdatedAt = clock.GetUtcNow();
        location.Revision++;
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return location;
    }
}
