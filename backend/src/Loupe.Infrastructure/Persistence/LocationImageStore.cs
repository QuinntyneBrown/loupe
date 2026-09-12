using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Locations;
using Loupe.Domain.Deletions;
using Loupe.Domain.Locations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LocationImageStore(LibraryDbContext database, TimeProvider clock) : ILocationImageStore
{
    public Task<int> CountAsync(Guid locationId, string ownerId, CancellationToken cancellationToken) =>
        database.LocationImages.CountAsync(image => image.LocationId == locationId && image.OwnerId == ownerId, cancellationToken);

    public async Task AddAsync(Guid locationId, string ownerId, ProcessedImage image, string imageKey, string previewKey, CancellationToken cancellationToken)
    {
        // The receipt owns the transaction; lock the location so concurrent uploads cannot exceed the cap or collide on a position.
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Adding a location image requires a transaction.");
        await ScoutingCancellation.CancelAsync(database, locationId, ownerId, "The image set changed.", clock.GetUtcNow(), cancellationToken);
        var location = await Lock(locationId, ownerId).Include(item => item.Images).SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (location.Images.Count >= LocationImageLimit.Maximum) throw new RequestValidationException("images", LocationImageLimit.Message);
        var added = new LocationImage
        {
            Id = Guid.NewGuid(),
            LocationId = locationId,
            OwnerId = ownerId,
            Position = location.Images.Count == 0 ? 1 : location.Images.Max(item => item.Position) + 1,
            ImageKey = imageKey,
            PreviewKey = previewKey,
            Width = image.Width,
            Height = image.Height,
            Exif = image.Exif,
            CreatedAt = clock.GetUtcNow()
        };
        // A client-generated key would be taken for an existing row; add it explicitly.
        database.LocationImages.Add(added);
        location.Images.Add(added);
        location.CoverImageId ??= added.Id;
        Touch(location);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<Location> RemoveAsync(Guid locationId, string ownerId, Guid imageId, long revision, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await ScoutingCancellation.CancelAsync(database, locationId, ownerId, "The image set changed.", clock.GetUtcNow(), cancellationToken);
        var location = await Lock(locationId, ownerId).Include(item => item.Images).Include(item => item.Tags).Include(item => item.CurrentScoutingOperation).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ResourceNotFoundException();
        var image = location.Images.SingleOrDefault(item => item.Id == imageId) ?? throw new ResourceNotFoundException();
        if (location.Revision != revision) throw new RevisionConflictException();
        location.Images.Remove(image);
        var position = 1;
        foreach (var remaining in location.Images.OrderBy(item => item.Position)) remaining.Position = position++;
        if (location.CoverImageId == imageId)
            location.CoverImageId = location.Images.OrderBy(item => item.Position).FirstOrDefault(item => item.Position >= image.Position)?.Id
                ?? location.Images.OrderBy(item => item.Position).FirstOrDefault()?.Id;
        database.Deletions.Add(new DeletionOperation
        {
            OwnerId = ownerId,
            ResourceType = "location-image",
            ResourceId = imageId,
            DeletedAt = clock.GetUtcNow(),
            MediaKeys = [image.ImageKey, image.PreviewKey]
        });
        Touch(location);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        await transaction.CommitAsync(cancellationToken);
        return location;
    }

    public async Task<Location> SetCoverAsync(Guid locationId, string ownerId, Guid imageId, long revision, CancellationToken cancellationToken)
    {
        var location = await database.Locations.Include(item => item.Images).Include(item => item.Tags).Include(item => item.CurrentScoutingOperation)
            .SingleOrDefaultAsync(item => item.Id == locationId && item.OwnerId == ownerId, cancellationToken) ?? throw new ResourceNotFoundException();
        if (location.Images.All(item => item.Id != imageId)) throw new ResourceNotFoundException();
        if (location.Revision != revision) throw new RevisionConflictException();
        location.CoverImageId = imageId;
        location.UpdatedAt = clock.GetUtcNow();
        location.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return location;
    }

    private IQueryable<Location> Lock(Guid locationId, string ownerId) =>
        database.Locations.FromSqlInterpolated($"SELECT * FROM \"locations\" WHERE \"Id\" = {locationId} AND \"OwnerId\" = {ownerId} FOR UPDATE");

    private void Touch(Location location)
    {
        location.UpdatedAt = clock.GetUtcNow();
        location.ImageSetRevision++;
        location.Revision++;
    }
}
