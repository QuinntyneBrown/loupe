using Loupe.Application.Common;
using Loupe.Application.Photographers;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed class ReviewPhotographerSuggestionCommandHandler(ICurrentOwner owner, IPhotographerSuggestionStore suggestions) : IRequestHandler<ReviewPhotographerSuggestionCommand, PhotographerResult>
{
    public async Task<PhotographerResult> Handle(ReviewPhotographerSuggestionCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the photographer revision you opened.");
        if (request.Target is not ("summary" or "tag" or "all")) throw new RequestValidationException("target", "Choose a summary, tag or all suggestions to review.");
        if (request.Decision is not ("accept" or "dismiss")) throw new RequestValidationException("decision", "Accept or dismiss the suggestion.");
        if (request.Value?.Contains('\0') == true) throw new RequestValidationException("value", "Remove null characters before saving.");
        return PhotographerResult.From(await suggestions.ReviewAsync(owner.Id, request, cancellationToken));
    }
}
