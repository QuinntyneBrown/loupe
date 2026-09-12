using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Search;

public sealed class ListSearchTagsQueryHandler(ICurrentOwner owner, ISearchStore search)
    : IRequestHandler<ListSearchTagsQuery, IReadOnlyList<SearchTagFacet>>
{
    public async Task<IReadOnlyList<SearchTagFacet>> Handle(ListSearchTagsQuery request, CancellationToken cancellationToken)
    {
        if (request.SelectedTags?.Length > 10 || request.SelectedTags?.Any(tag => tag?.Contains('\0') == true) == true)
            throw new RequestValidationException("selectedTags", "Choose up to 10 valid tag names without null characters.");
        ILookup<string, string> selected;
        try
        {
            selected = (request.SelectedTags ?? []).ToLookup(tag => TagName.Validate(tag).ToUpperInvariant(), StringComparer.Ordinal);
        }
        catch (RequestValidationException error)
        {
            throw new RequestValidationException("selectedTags", error.Errors["tags"][0]);
        }
        var tags = await search.ListTagsAsync(owner.Id, cancellationToken);
        return tags.Select(tag => new SearchTagFacet(tag.Name, tag.Count, tag.NormalizedName,
            selected[tag.NormalizedName].Distinct(StringComparer.Ordinal).ToArray())).ToArray();
    }
}
