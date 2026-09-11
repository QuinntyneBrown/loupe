using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class UpdateReferenceTextCommandHandler(ICurrentOwner owner, IReferenceTextStore texts) : IRequestHandler<UpdateReferenceTextCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(UpdateReferenceTextCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        var maximum = request.Field switch { ReferenceTextField.Description => 4000, ReferenceTextField.Notes => 10000, _ => throw new ArgumentOutOfRangeException(nameof(request)) };
        var text = TextField.Normalize(request.Text, maximum, request.Field.ToString().ToLowerInvariant());
        return ReferenceResult.From(await texts.UpdateAsync(request.Id, owner.Id, request.Revision, request.Field, text, cancellationToken));
    }
}
