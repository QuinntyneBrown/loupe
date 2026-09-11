using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerSummaries;

public interface IPhotographerSummaryQueue
{
    Task QueueIfConfiguredAsync(Photographer photographer, CancellationToken cancellationToken);
    Task CancelAsync(Guid id, string ownerId, bool deleting, CancellationToken cancellationToken);
}
