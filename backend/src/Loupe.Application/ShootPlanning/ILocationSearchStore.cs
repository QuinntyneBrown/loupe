using Loupe.Application.Common;
using Loupe.Application.Search;

namespace Loupe.Application.ShootPlanning;

public interface ILocationSearchStore
{
    Task<IReadOnlyList<LocationSearchItem>> ListAsync(string ownerId, LocationSearchFilter filter, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountAsync(string ownerId, LocationSearchFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchTagCount>> ListTagsAsync(string ownerId, CancellationToken cancellationToken);
}
