using Loupe.Application.Search;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.ShootPlanning;

public sealed class ListLocationTagsQueryHandler(ICurrentOwner owner, ILocationSearchStore search) : IRequestHandler<ListLocationTagsQuery, IReadOnlyList<SearchTagCount>>
{
    public Task<IReadOnlyList<SearchTagCount>> Handle(ListLocationTagsQuery request, CancellationToken cancellationToken) =>
        search.ListTagsAsync(owner.Id, cancellationToken);
}
