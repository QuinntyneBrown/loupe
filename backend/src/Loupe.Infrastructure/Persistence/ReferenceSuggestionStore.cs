using Loupe.Application.Common;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceSuggestionStore(LibraryDbContext database) : IReferenceSuggestionStore
{
    public async Task<Reference> ReviewAsync(string ownerId, ReviewReferenceSuggestionCommand decision, CancellationToken cancellationToken)
    {
        var reference = await database.References.Include(item => item.Boards).Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == decision.Id && item.OwnerId == ownerId, cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.Revision != decision.Revision) throw new RevisionConflictException();
        var undo = new ReferenceSuggestionUndo(reference.Revision + 1, reference.Description, reference.DescriptionProvenance,
            reference.SuggestionsJson!, reference.Tags.Select(tag => new ReferenceTagSnapshot(tag.Name, tag.Category, tag.Provenance)).ToArray());
        ReferenceSuggestionReview.Apply(reference, decision);
        reference.SuggestionUndoJson = JsonSerializer.Serialize(undo);
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return reference;
    }

    public async Task<Reference> UndoAsync(string ownerId, Guid id, long revision, CancellationToken cancellationToken)
    {
        var reference = await database.References.Include(item => item.Boards).Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken) ?? throw new ResourceNotFoundException();
        var undo = reference.SuggestionUndoJson is null ? null : JsonSerializer.Deserialize<ReferenceSuggestionUndo>(reference.SuggestionUndoJson);
        if (reference.Revision != revision || undo?.Revision != revision) throw new RevisionConflictException();
        reference.Description = undo.Description;
        reference.DescriptionProvenance = undo.DescriptionProvenance;
        reference.SuggestionsJson = undo.SuggestionsJson;
        // Review only adds active tags; all pre-review tags remain until an intervening revision invalidates Undo.
        var original = undo.Tags.ToDictionary(tag => tag.Name.ToUpperInvariant());
        foreach (var tag in reference.Tags.ToArray())
            if (!original.ContainsKey(tag.NormalizedName)) reference.Tags.Remove(tag);
        reference.SuggestionUndoJson = null;
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return reference;
    }
}
