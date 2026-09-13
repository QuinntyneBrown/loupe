using Loupe.Domain.Videos;

namespace Loupe.Application.Videos;

public interface IVideoIndexStore
{
    /// <summary>Claims one video whose vector is missing, stale, or from another model, embeds it, and stores the vector. Returns false when none remain.</summary>
    Task<bool> IndexNextAsync(string model, Func<Video, CancellationToken, Task<float[]>> embed, CancellationToken cancellationToken);
}
