using Loupe.Application.Common;
using Loupe.Domain.Videos;

namespace Loupe.Application.Videos;

public interface IVideoStore
{
    Task<Video> SaveAsync(Video video, CancellationToken cancellationToken);
    Task<Video?> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken);
    Task<Video> UpdateAsync(string ownerId, Guid id, long revision, VideoMetadata metadata, CancellationToken cancellationToken);
    Task DeleteAsync(string ownerId, Guid id, long revision, CancellationToken cancellationToken);
    Task<IReadOnlyList<Video>> ListAsync(string ownerId, VideoFilter filter, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountAsync(string ownerId, VideoFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<VideoTagCount>> ListTagsAsync(string ownerId, CancellationToken cancellationToken);
    /// <summary>Ranks the owner's currently indexed videos by cosine similarity to the query vector, applying topic and tag filters before the threshold and limit.</summary>
    Task<VideoSearchResult> SearchAsync(string ownerId, string model, float[] query, VideoFilter filter, double threshold, int count, CancellationToken cancellationToken);
}
