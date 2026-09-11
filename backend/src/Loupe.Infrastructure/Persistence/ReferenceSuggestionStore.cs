using Loupe.Application.Common;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceSuggestionStore(LibraryDbContext database) : IReferenceSuggestionStore
{
    public async Task<Reference> ReviewAsync(string ownerId, ReviewReferenceSuggestionCommand decision, CancellationToken cancellationToken)
    {
        var reference = await database.References.Include(item => item.Boards).Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == decision.Id && item.OwnerId == ownerId, cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.Revision != decision.Revision) throw new RevisionConflictException();
        ReferenceSuggestionReview.Apply(reference, decision);
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return reference;
    }
}
