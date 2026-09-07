using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed class ListPhotographsQueryHandler(ICurrentOwner owner, IPhotographStore photographs) : IRequestHandler<ListPhotographsQuery, PhotographPage>
{
    public async Task<PhotographPage> Handle(ListPhotographsQuery request, CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        var cursor = CreatedCursor.Parse(request.Cursor);
        var found = await photographs.ListAsync(owner.Id, request.PageSize + 1, cursor, cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? new CreatedCursor(items[^1].CreatedAt, items[^1].Id).Encode() : null;
        return new PhotographPage(items, next);
    }
}
