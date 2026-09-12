using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed class ListReferenceCandidatesQueryHandler(ICurrentOwner owner, IPhotographerStore photographers, IReferencePhotographerStore references) : IRequestHandler<ListReferenceCandidatesQuery, ReferenceCandidatePage>
{
    public async Task<ReferenceCandidatePage> Handle(ListReferenceCandidatesQuery request, CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        if (request.Query?.Contains('\0') == true) throw new RequestValidationException("query", "Remove the null character from the query.");
        var query = TextField.Normalize(request.Query?.Normalize(NormalizationForm.FormC), 200, "query") ?? "";
        _ = await photographers.FindOwnedAsync(owner.Id, request.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { type = "reference-candidates", ownerId = owner.Id, request.Id, request.PageSize, query }))) + ".";
        if (request.Cursor is not null && (request.Cursor.Length > 200 || !request.Cursor.StartsWith(scope, StringComparison.Ordinal)))
            throw new RequestValidationException("cursor", "This cursor belongs to a different view. Refresh the list.");
        var cursor = CreatedCursor.Parse(request.Cursor?[scope.Length..]);
        var found = await references.CandidatesAsync(owner.Id, query, request.PageSize + 1, cursor, cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? scope + new CreatedCursor(items[^1].CreatedAt, items[^1].Id).Encode() : null;
        return new(items, next, await references.CountCandidatesAsync(owner.Id, query, cancellationToken));
    }
}
