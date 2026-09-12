using Loupe.Application.Common;
using Loupe.Application.Search;

namespace Loupe.Application.ShootPlanning;

public interface ILocationSearchStore
{
    Task<IReadOnlyList<LocationSearchItem>> ListAsync(string ownerId, LocationSearchFilter filter, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountAsync(string ownerId, LocationSearchFilter filter, CancellationToken cancellationToken);
    /// <summary>Current vectors of the model ranked by cosine similarity descending then identifier, at or above the threshold, after the same filters.</summary>
    Task<IReadOnlyList<LocationSearchItem>> RankAsync(string ownerId, LocationSearchFilter filter, LocationSearchRanking ranking, int count, SemanticCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountRankedAsync(string ownerId, LocationSearchFilter filter, LocationSearchRanking ranking, CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchTagCount>> ListTagsAsync(string ownerId, CancellationToken cancellationToken);
}
