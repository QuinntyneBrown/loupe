using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Photographs;
using Loupe.Application.Security;
using Loupe.Domain.Critiques;
using Loupe.Domain.Photographs;
using MediatR;

namespace Loupe.Application.Comparisons;

public sealed class GetComparisonQueryHandler(ICurrentOwner owner, IPhotographStore photographs) : IRequestHandler<GetComparisonQuery, ComparisonResult>
{
    public async Task<ComparisonResult> Handle(GetComparisonQuery request, CancellationToken cancellationToken)
    {
        GetComparisonQueryValidator.Validate(request);
        var first = await photographs.FindOwnedAsync(request.FirstId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var second = await photographs.FindOwnedAsync(request.SecondId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        return new ComparisonResult(Attempt(first, "firstId"), Attempt(second, "secondId"));
    }

    private static ComparisonAttempt Attempt(Photograph photograph, string field)
    {
        var critique = photograph.CritiqueJson is null ? null : JsonSerializer.Deserialize<SavedCritique>(photograph.CritiqueJson);
        if (critique is null) throw new RequestValidationException(field, "Choose a photograph with a saved successful critique.");
        return new ComparisonAttempt(PhotographResult.From(photograph), critique);
    }
}
