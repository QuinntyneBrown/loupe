using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;
using Loupe.Application.Boards;

namespace Loupe.Application.References;

public sealed class ListReferencesQueryHandler(ICurrentOwner owner, IReferenceStore references, IBoardStore boards) : IRequestHandler<ListReferencesQuery, ReferencePage>
{
    public async Task<ReferencePage> Handle(ListReferencesQuery request, CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        if (request.BoardId is { } boardId) await boards.RequireOwnedAsync(owner.Id, boardId, cancellationToken);
        var scope = ReferenceListCursor.Scope(owner.Id, request.BoardId, request.PageSize);
        var found = await references.ListAsync(owner.Id, request.PageSize + 1, ReferenceListCursor.Parse(request.Cursor, scope), request.BoardId, cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? ReferenceListCursor.Encode(new CreatedCursor(items[^1].CreatedAt, items[^1].Id), scope) : null;
        var totalCount = await references.CountAsync(owner.Id, request.BoardId, cancellationToken);
        var libraryCount = request.BoardId is null ? totalCount : await references.CountAsync(owner.Id, null, cancellationToken);
        return new ReferencePage(items, next, totalCount, libraryCount);
    }
}
