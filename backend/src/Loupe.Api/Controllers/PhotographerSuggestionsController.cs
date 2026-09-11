using Loupe.Application.PhotographerSummaries;
using Loupe.Domain.Photographers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/photographers/{id:guid}/suggestions")]
public sealed class PhotographerSuggestionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SavedPhotographerSuggestions>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPhotographerSuggestionsQuery(id), cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }
}
