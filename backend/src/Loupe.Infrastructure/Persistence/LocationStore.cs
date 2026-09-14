using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Application.Scouting;
using Loupe.Application.Search;
using Loupe.Domain.Locations;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LocationStore(LibraryDbContext context, TimeProvider clock, IEmbeddingConfiguration embeddings) : ILocationStore
{
    public async Task SaveAsync(Location location, CancellationToken cancellationToken)
    {
        context.Locations.Add(location);
        await LocationIndexIntent.RecordAsync(context, location, embeddings.Model, clock.GetUtcNow(), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Location?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        context.Locations.AsNoTracking().Include(location => location.Tags).Include(location => location.Images).Include(location => location.CurrentScoutingOperation)
            .Include(location => location.CurrentIndexOperation).SingleOrDefaultAsync(location => location.Id == id && location.OwnerId == ownerId, cancellationToken);

    public Task<int> CountAsync(string ownerId, CancellationToken cancellationToken) =>
        context.Locations.CountAsync(location => location.OwnerId == ownerId, cancellationToken);

    public async Task<IReadOnlyList<LocationSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var query = context.Locations.AsNoTracking().Where(location => location.OwnerId == ownerId);
        if (cursor is not null) query = query.Where(location => location.CreatedAt < cursor.CreatedAt
            || location.CreatedAt == cursor.CreatedAt && location.Id.CompareTo(cursor.Id) > 0);
        var rows = await query.OrderByDescending(location => location.CreatedAt).ThenBy(location => location.Id).Take(count)
            .Select(location => new
            {
                location.Id,
                location.Name,
                location.Locality,
                location.CoverImageId,
                ImageCount = location.Images.Count,
                Operation = location.CurrentScoutingOperation == null ? (OperationStatus?)null : location.CurrentScoutingOperation.Status,
                location.ScoutingReportJson,
                location.ImageSetRevision,
                location.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new LocationSummary(row.Id, row.Name, row.Locality,
            row.CoverImageId == null ? null : LocationImageUrls.Preview(row.Id, row.CoverImageId.Value), row.ImageCount,
            LocationReportStatus.Derive(row.Operation, ScoutingReportJson.Deserialize(row.ScoutingReportJson), row.ImageSetRevision),
            row.CreatedAt)).ToArray();
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
            .Include(item => item.CurrentIndexOperation)
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken) ?? throw new ResourceNotFoundException();
        if (location.Revision != revision) throw new RevisionConflictException();
        apply(location);
        location.UpdatedAt = clock.GetUtcNow();
        location.Revision++;
        await LocationIndexIntent.RecordAsync(context, location, embeddings.Model, location.UpdatedAt, cancellationToken);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return location;
    }
}
