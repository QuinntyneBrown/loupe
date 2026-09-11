using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceTagStore(LibraryDbContext database) : IReferenceTagStore
{
    public async Task<Reference> ReplaceAsync(string ownerId, Guid referenceId, long revision, IReadOnlyList<ReferenceTagInput> tags, CancellationToken cancellationToken)
    {
        var reference = await database.References.Include(item => item.Boards).Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == referenceId && item.OwnerId == ownerId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        var requested = tags.ToDictionary(tag => tag.Name!.ToUpperInvariant());
        foreach (var tag in reference.Tags.ToArray())
        {
            if (!requested.ContainsKey(tag.NormalizedName)) reference.Tags.Remove(tag);
        }
        foreach (var (key, input) in requested)
        {
            var existing = reference.Tags.SingleOrDefault(tag => tag.NormalizedName == key);
            if (existing is null)
                reference.Tags.Add(new ReferenceTag { ReferenceId = referenceId, OwnerId = ownerId, NormalizedName = key,
                    Name = input.Name!, Category = input.Category, Provenance = "manual" });
            else if (existing.Name != input.Name || existing.Category != input.Category)
            {
                existing.Name = input.Name!;
                existing.Category = input.Category;
                existing.Provenance = "manual";
            }
        }
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return reference;
    }
}
