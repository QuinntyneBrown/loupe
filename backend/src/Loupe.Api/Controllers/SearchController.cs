using Loupe.Api.Search;
using Loupe.Application.Search;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
public sealed class SearchController(ISender sender) : ControllerBase
{
    [HttpGet("tags")]
    public Task<IReadOnlyList<SearchTagFacet>> Tags(CancellationToken cancellationToken, [FromQuery] string[]? selectedTags = null) =>
        sender.Send(new ListSearchTagsQuery(selectedTags), cancellationToken);

    [HttpGet]
    public Task<SearchPage> Search([FromQuery] SearchRequest request, CancellationToken cancellationToken) =>
        sender.Send(new SearchQuery(request.Query, request.Type, request.Tags, request.BoardIds, request.PageSize, request.Cursor, request.Mode), cancellationToken);
}
