using Loupe.Api.Boards;
using Loupe.Application.Boards;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/boards")]
public sealed class BoardsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<BoardResult>> List(CancellationToken cancellationToken) => sender.Send(new ListBoardsQuery(), cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<BoardResult> Rename(Guid id, RenameBoardRequest request, CancellationToken cancellationToken) =>
        sender.Send(new RenameBoardCommand(id, request.Revision, request.Name), cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] long revision, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteBoardCommand(id, revision), cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<BoardResult>> Create(CreateBoardRequest request, CancellationToken cancellationToken)
    {
        var board = await sender.Send(new CreateBoardCommand(request.Name), cancellationToken);
        return Created($"/api/boards/{board.Id}", board);
    }
}
