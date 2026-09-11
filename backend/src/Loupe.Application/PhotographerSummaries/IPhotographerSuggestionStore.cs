using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerSummaries;

public interface IPhotographerSuggestionStore
{
    Task<Photographer> ReviewAsync(string ownerId, ReviewPhotographerSuggestionCommand decision, CancellationToken cancellationToken);
    Task<Photographer> UndoAsync(string ownerId, Guid id, long revision, CancellationToken cancellationToken);
}
