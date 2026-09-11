using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class SetReferenceTagsCommandHandler(ICurrentOwner owner, IReferenceTagStore store) : IRequestHandler<SetReferenceTagsCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(SetReferenceTagsCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        if (request.Tags is null || request.Tags.Length > 50) throw new RequestValidationException("tags", "Supply up to 50 active tags.");
        var tags = request.Tags.Select(tag => new ReferenceTagInput(TagName.Validate(tag.Name), TagName.Category(tag.Category)))
            .DistinctBy(tag => tag.Name!.ToUpperInvariant()).ToArray();
        return ReferenceResult.From(await store.ReplaceAsync(owner.Id, request.ReferenceId, request.Revision, tags, cancellationToken));
    }
}
