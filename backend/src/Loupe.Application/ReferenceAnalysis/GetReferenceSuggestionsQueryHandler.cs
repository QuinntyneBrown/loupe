using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Security;
using Loupe.Domain.References;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed class GetReferenceSuggestionsQueryHandler(ICurrentOwner owner, IReferenceStore references) : IRequestHandler<GetReferenceSuggestionsQuery, SavedReferenceSuggestions?>
{
    public async Task<SavedReferenceSuggestions?> Handle(GetReferenceSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var reference = await references.FindOwnedAsync(request.ReferenceId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.SuggestionsJson is null) return null;
        var suggestions = JsonSerializer.Deserialize<SavedReferenceSuggestions>(reference.SuggestionsJson);
        return suggestions?.ImageRevision == reference.ImageRevision ? suggestions : null;
    }
}
