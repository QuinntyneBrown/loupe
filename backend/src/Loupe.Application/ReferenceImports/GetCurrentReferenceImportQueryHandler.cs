using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.ReferenceImports;

public sealed class GetCurrentReferenceImportQueryHandler(ICurrentOwner owner, IReferenceStore references,
    IBackgroundOperationStore operations) : IRequestHandler<GetCurrentReferenceImportQuery, OperationResult?>
{
    public async Task<OperationResult?> Handle(GetCurrentReferenceImportQuery request, CancellationToken cancellationToken)
    {
        var reference = await references.FindOwnedAsync(request.ReferenceId, owner.Id, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (reference.CurrentImportOperationId is not { } operationId) return null;
        var operation = await operations.FindOwnedAsync(operationId, owner.Id, cancellationToken)
            ?? throw new ResourceNotFoundException();
        return OperationResult.From(operation);
    }
}
