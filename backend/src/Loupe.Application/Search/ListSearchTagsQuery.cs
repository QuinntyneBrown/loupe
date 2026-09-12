using MediatR;

namespace Loupe.Application.Search;

public sealed record ListSearchTagsQuery(string[]? SelectedTags = null) : IRequest<IReadOnlyList<SearchTagFacet>>;
