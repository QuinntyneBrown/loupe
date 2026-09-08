using Loupe.Domain.Operations;
namespace Loupe.Application.ReferenceImports;

public interface IReferenceImportStore
{
    Task<Guid> AdmitAsync(Guid referenceId, string ownerId, long revision, ExecutionMode mode, CancellationToken cancellationToken);
}
