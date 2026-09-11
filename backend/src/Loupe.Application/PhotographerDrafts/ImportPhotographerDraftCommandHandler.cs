using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Photographers;
using Loupe.Application.ReferenceDrafts;
using Loupe.Application.ReferenceImports;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.PhotographerDrafts;

public sealed class ImportPhotographerDraftCommandHandler(ICurrentOwner owner, IPhotographerDraftStore drafts, IOperationReceiptStore receipts,
    IPortfolioUrlPolicy policy, IReferenceImportConfiguration configuration) : IRequestHandler<ImportPhotographerDraftCommand, PhotographerDraftResult>
{
    public async Task<PhotographerDraftResult> Handle(ImportPhotographerDraftCommand request, CancellationToken cancellationToken)
    {
        var key = DraftOperationKey.Validate(request.OperationKey);
        var source = PhotographerMetadataValidator.Source(request.PortfolioUrl, policy);
        var name = TextField.Normalize(request.Name, 200, "name");
        var configured = true;
        try { _ = configuration.GetMode(); } catch (IntegrationNotConfiguredException) { configured = false; }
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { source, name })));
        var id = await receipts.ExecuteAsync(owner.Id, "photographer-draft-import", key, fingerprint,
            token => drafts.AdmitAsync(owner.Id, source, name, configured, token), cancellationToken);
        return PhotographerDraftResult.From(await drafts.FindOwnedAsync(owner.Id, id, cancellationToken));
    }
}
