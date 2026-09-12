using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed class UndoReferenceSuggestionCommandHandler(ICurrentOwner owner, IReferenceSuggestionStore suggestions)
    : IRequestHandler<UndoReferenceSuggestionCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(UndoReferenceSuggestionCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        return ReferenceResult.From(await suggestions.UndoAsync(owner.Id, request.Id, request.Revision, cancellationToken));
    }
}
