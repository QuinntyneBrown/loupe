using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Photographers;
using Loupe.Application.Security;
using Loupe.Domain.Photographers;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed class GetPhotographerSuggestionsQueryHandler(ICurrentOwner owner, IPhotographerStore photographers) : IRequestHandler<GetPhotographerSuggestionsQuery, SavedPhotographerSuggestions?>
{
    public async Task<SavedPhotographerSuggestions?> Handle(GetPhotographerSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var photographer = await photographers.FindOwnedAsync(owner.Id, request.PhotographerId, cancellationToken) ?? throw new ResourceNotFoundException();
        return photographer.SuggestionsJson is null ? null : JsonSerializer.Deserialize<SavedPhotographerSuggestions>(photographer.SuggestionsJson);
    }
}
