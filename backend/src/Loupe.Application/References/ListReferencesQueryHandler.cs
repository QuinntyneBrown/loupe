using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class ListReferencesQueryHandler(ICurrentOwner owner, IReferenceStore references) : IRequestHandler<ListReferencesQuery, ReferencePage>
{
    public async Task<ReferencePage> Handle(ListReferencesQuery request, CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        var found = await references.ListAsync(owner.Id, request.PageSize + 1, CreatedCursor.Parse(request.Cursor), cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? new CreatedCursor(items[^1].CreatedAt, items[^1].Id).Encode() : null;
        return new ReferencePage(items, next);
    }
}
