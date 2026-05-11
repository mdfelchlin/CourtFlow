using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Tournaments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class TournamentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TournamentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TournamentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new ListTournamentsQuery(), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTournamentQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTournamentCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTournamentCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Mismatched route and body ids");
        var result = await _mediator.Send(command, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new PublishTournamentCommand(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new UnpublishTournamentCommand(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ArchiveTournamentCommand(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new RestoreArchivedTournamentCommand(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/clone")]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Clone(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CloneTournamentCommand(id), ct);
        return result is null ? NotFound() : CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _mediator.Send(new DeleteTournamentCommand(id), ct) ? NoContent() : NotFound();
}
