using Loupe.Domain.Photographers;

namespace Loupe.Application.Photographers;

public interface IPhotographerStore
{
    Task<Photographer> SaveAsync(Photographer photographer, CancellationToken cancellationToken);
    Task<Photographer?> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken);
}
