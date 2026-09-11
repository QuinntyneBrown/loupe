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
    [HttpGet]
    public Task<SearchPage> Search(CancellationToken cancellationToken, [FromQuery] string? query = null, [FromQuery] string type = "all",
        [FromQuery] string[]? tags = null, [FromQuery] Guid[]? boardIds = null, [FromQuery] int pageSize = 24, [FromQuery] string? cursor = null) =>
        sender.Send(new SearchQuery(query, type, tags, boardIds, pageSize, cursor), cancellationToken);
}
