using Loupe.Domain.References;
using Loupe.Application.References;

namespace Loupe.Application.ReferenceDrafts;

public interface IReferenceDraftStore
{
    Task AddAsync(ReferenceDraft draft, CancellationToken cancellationToken);
    Task<Guid> AdmitSourceAsync(string ownerId, string source, bool configured, CancellationToken cancellationToken);
    Task<ReferenceDraft> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(string ownerId, Guid id, CancellationToken cancellationToken);
    Task<SaveReferenceUrlResult> CommitAsync(string ownerId, Guid id, long revision, ReferenceMetadata metadata, Guid[] boardIds, CancellationToken cancellationToken);
}
