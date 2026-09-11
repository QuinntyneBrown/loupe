using Loupe.Domain.References;

namespace Loupe.Application.ReferenceAnalysis;

public interface IReferenceSuggestionStore
{
    Task<Reference> ReviewAsync(string ownerId, ReviewReferenceSuggestionCommand decision, CancellationToken cancellationToken);
    Task<Reference> UndoAsync(string ownerId, Guid id, long revision, CancellationToken cancellationToken);
}
