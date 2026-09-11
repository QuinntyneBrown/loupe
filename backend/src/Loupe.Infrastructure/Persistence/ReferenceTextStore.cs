using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceTextStore(LibraryDbContext database) : IReferenceTextStore
{
    public async Task<Reference> UpdateAsync(Guid id, string ownerId, long revision, ReferenceTextField field, string? text, CancellationToken cancellationToken)
    {
        var reference = await database.References.Include(item => item.Boards).Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        switch (field)
        {
            case ReferenceTextField.Description: reference.Description = text; reference.DescriptionProvenance = text is null ? null : "manual"; break;
            case ReferenceTextField.Notes: reference.Notes = text; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return reference;
    }
}
