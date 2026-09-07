using Loupe.Domain.Photographs;

namespace Loupe.Application.Photographs;

public interface IPhotographStore
{
    Task SaveAsync(Photograph photograph, CancellationToken cancellationToken);
    Task<Photograph?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
}
