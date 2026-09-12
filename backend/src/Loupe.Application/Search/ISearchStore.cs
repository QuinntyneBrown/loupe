using Loupe.Application.Common;

namespace Loupe.Application.Search;

public interface ISearchStore
{
    Task<IReadOnlyList<SearchTagCount>> ListTagsAsync(string ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchItem>> ListAsync(string ownerId, SearchFilter filter, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountAsync(string ownerId, SearchFilter filter, CancellationToken cancellationToken);
}
