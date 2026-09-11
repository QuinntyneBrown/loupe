using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed class ReviewReferenceSuggestionCommandHandler(ICurrentOwner owner, IReferenceSuggestionStore suggestions)
    : IRequestHandler<ReviewReferenceSuggestionCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(ReviewReferenceSuggestionCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        if (request.Target is not ("description" or "tag")) throw new RequestValidationException("target", "Choose a description or tag to review.");
        if (request.Decision is not ("accept" or "dismiss")) throw new RequestValidationException("decision", "Accept or dismiss the suggestion.");
        return ReferenceResult.From(await suggestions.ReviewAsync(owner.Id, request, cancellationToken));
    }
}
