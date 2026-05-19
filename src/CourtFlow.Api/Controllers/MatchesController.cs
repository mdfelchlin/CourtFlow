using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Matches;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/matches")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class MatchesController : ControllerBase
{
    private readonly IMediator _mediator;
    public MatchesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? tournamentId, [FromQuery] Guid? divisionId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListMatchesQuery(tournamentId, divisionId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetMatchQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveMatchCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var r = await _mediator.Send(cmd, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/score")]
    public async Task<IActionResult> Score(Guid id, [FromBody] ScoreMatchCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var r = await _mediator.Send(cmd, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/forfeit")]
    public async Task<IActionResult> Forfeit(Guid id, [FromBody] ForfeitMatchRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ForfeitMatchCommand(id, body.WinningTeamId), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new StartMatchCommand(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/referee")]
    public async Task<IActionResult> ChangeReferee(Guid id, [FromBody] ChangeRefereeRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ChangeMatchRefereeCommand(id, body.RefTeamId), ct);
        return r is null ? NotFound() : Ok(r);
    }
}

public sealed record ForfeitMatchRequest(Guid WinningTeamId);
public sealed record ChangeRefereeRequest(Guid? RefTeamId);
