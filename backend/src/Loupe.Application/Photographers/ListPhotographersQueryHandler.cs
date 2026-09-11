using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed class ListPhotographersQueryHandler(ICurrentOwner owner, IPhotographerStore photographers) : IRequestHandler<ListPhotographersQuery, PhotographerPage>
{
    public async Task<PhotographerPage> Handle(ListPhotographersQuery request, CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        var scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { type = "photographers", owner.Id, request.PageSize }))) + ".";
        if (request.Cursor is not null && (request.Cursor.Length > 200 || !request.Cursor.StartsWith(scope, StringComparison.Ordinal)))
            throw new RequestValidationException("cursor", "This cursor belongs to a different view. Refresh the list.");
        var cursor = CreatedCursor.Parse(request.Cursor?[scope.Length..]);
        var found = await photographers.ListAsync(owner.Id, request.PageSize + 1, cursor, cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? scope + new CreatedCursor(items[^1].CreatedAt, items[^1].Id).Encode() : null;
        return new(items, next, await photographers.CountAsync(owner.Id, cancellationToken));
    }
}
