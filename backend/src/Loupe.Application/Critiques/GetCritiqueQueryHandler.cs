using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Photographs;
using Loupe.Application.Security;
using Loupe.Domain.Critiques;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed class GetCritiqueQueryHandler(ICurrentOwner owner, IPhotographStore photographs) : IRequestHandler<GetCritiqueQuery, SavedCritique?>
{
    public async Task<SavedCritique?> Handle(GetCritiqueQuery request, CancellationToken cancellationToken)
    {
        var photograph = await photographs.FindOwnedAsync(request.PhotographId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        return photograph.CritiqueJson is null ? null : JsonSerializer.Deserialize<SavedCritique>(photograph.CritiqueJson);
    }
}
