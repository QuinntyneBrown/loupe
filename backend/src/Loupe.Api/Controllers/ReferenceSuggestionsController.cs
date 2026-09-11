using Loupe.Application.ReferenceAnalysis;
using Loupe.Domain.References;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/references/{id:guid}/suggestions")]
public sealed class ReferenceSuggestionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SavedReferenceSuggestions>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetReferenceSuggestionsQuery(id), cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }
}
