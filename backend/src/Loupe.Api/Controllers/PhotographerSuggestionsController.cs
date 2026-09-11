using Loupe.Application.PhotographerSummaries;
using Loupe.Api.Photographers;
using Loupe.Application.Photographers;
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
    [HttpPut]
    public Task<PhotographerResult> Review(Guid id, ReviewPhotographerSuggestionRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ReviewPhotographerSuggestionCommand(id, request.OperationId, request.Revision, request.Target, request.Decision, request.Name, request.Value, request.Category), cancellationToken);
    [HttpPost("undo")]
    public Task<PhotographerResult> Undo(Guid id, UndoPhotographerSuggestionRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UndoPhotographerSuggestionCommand(id, request.Revision), cancellationToken);
    [HttpGet]
    public async Task<ActionResult<SavedPhotographerSuggestions>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPhotographerSuggestionsQuery(id), cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }
}
