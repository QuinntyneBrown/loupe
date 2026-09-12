using Loupe.Application.ReferenceAnalysis;
using Loupe.Domain.References;
using Loupe.Api.References;
using Loupe.Application.References;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/references/{id:guid}/suggestions")]
public sealed class ReferenceSuggestionsController(ISender sender) : ControllerBase
{
    [HttpPost("undo")]
    public Task<ReferenceResult> Undo(Guid id, UndoReferenceSuggestionRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UndoReferenceSuggestionCommand(id, request.Revision), cancellationToken);

    [HttpPut]
    public Task<ReferenceResult> Review(Guid id, ReviewReferenceSuggestionRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ReviewReferenceSuggestionCommand(id, request.OperationId, request.Revision, request.Target, request.Decision, request.Name, request.Value, request.Category), cancellationToken);

    [HttpGet]
    public async Task<ActionResult<SavedReferenceSuggestions>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetReferenceSuggestionsQuery(id), cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }
}
