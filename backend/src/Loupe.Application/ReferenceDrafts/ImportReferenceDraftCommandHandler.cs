using System.Security.Cryptography;
using System.Text;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.ReferenceImports;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed class ImportReferenceDraftCommandHandler(ICurrentOwner owner, IReferenceDraftStore drafts, IOperationReceiptStore receipts,
    IReferenceImportConfiguration configuration) : IRequestHandler<ImportReferenceDraftCommand, ReferenceDraftResult>
{
    public async Task<ReferenceDraftResult> Handle(ImportReferenceDraftCommand request, CancellationToken cancellationToken)
    {
        var key = DraftOperationKey.Validate(request.OperationKey);
        var source = ReferenceMetadataValidator.Source(request.SourceUrl) ?? throw new RequestValidationException("sourceUrl", "Enter a source URL.");
        var configured = true;
        try { _ = configuration.GetMode(); }
        catch (IntegrationNotConfiguredException) { configured = false; }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        var id = await receipts.ExecuteAsync(owner.Id, "reference-draft-import", key, hash,
            token => drafts.AdmitSourceAsync(owner.Id, source, configured, token), cancellationToken);
        return ReferenceDraftResult.From(await drafts.FindOwnedAsync(owner.Id, id, cancellationToken));
    }
}
