using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.PhotographerSummaries;
using Loupe.Domain.Photographers;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographerSuggestionStore(LibraryDbContext database) : IPhotographerSuggestionStore
{
    public async Task<Photographer> ReviewAsync(string ownerId, ReviewPhotographerSuggestionCommand decision, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var photographer = await FindLockedAsync(ownerId, decision.Id, cancellationToken);
        if (photographer.Revision != decision.Revision) throw new RevisionConflictException();
        var undo = new PhotographerSuggestionUndo(photographer.Revision + 1, photographer.Summary, photographer.SummaryProvenance,
            photographer.SuggestionsJson!, photographer.Tags.Select(tag => tag.NormalizedName).ToArray());
        PhotographerSuggestionReview.Apply(photographer, decision);
        photographer.SuggestionUndoJson = JsonSerializer.Serialize(undo); photographer.Revision++;
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return photographer;
    }
    public async Task<Photographer> UndoAsync(string ownerId, Guid id, long revision, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var photographer = await FindLockedAsync(ownerId, id, cancellationToken);
        var undo = photographer.SuggestionUndoJson is null ? null : JsonSerializer.Deserialize<PhotographerSuggestionUndo>(photographer.SuggestionUndoJson);
        if (photographer.Revision != revision || undo?.Revision != revision) throw new RevisionConflictException();
        photographer.Summary = undo.Summary; photographer.SummaryProvenance = undo.SummaryProvenance; photographer.SuggestionsJson = undo.SuggestionsJson;
        foreach (var tag in photographer.Tags.ToArray()) if (!undo.TagNames.Contains(tag.NormalizedName, StringComparer.Ordinal)) photographer.Tags.Remove(tag);
        photographer.SuggestionUndoJson = null; photographer.Revision++;
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return photographer;
    }
    private async Task<Photographer> FindLockedAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        await database.Photographers.FromSqlInterpolated($"SELECT * FROM photographers WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .Include(item => item.Tags).SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
}
