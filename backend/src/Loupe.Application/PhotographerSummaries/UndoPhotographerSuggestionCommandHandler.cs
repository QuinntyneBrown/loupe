using Loupe.Application.Common;
using Loupe.Application.Photographers;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed class UndoPhotographerSuggestionCommandHandler(ICurrentOwner owner, IPhotographerSuggestionStore suggestions) : IRequestHandler<UndoPhotographerSuggestionCommand, PhotographerResult>
{
    public async Task<PhotographerResult> Handle(UndoPhotographerSuggestionCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the photographer revision you opened.");
        return PhotographerResult.From(await suggestions.UndoAsync(owner.Id, request.Id, request.Revision, cancellationToken));
    }
}
