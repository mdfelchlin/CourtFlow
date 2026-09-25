using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Teams;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/teams")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class TeamsController : ControllerBase
{
    private readonly IMediator _mediator;
    public TeamsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? tournamentId, [FromQuery] Guid? divisionId, [FromQuery] Guid? poolId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListTeamsQuery(tournamentId, divisionId, poolId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(Create), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var r = await _mediator.Send(cmd, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("{id:guid}/division")]
    public async Task<IActionResult> MoveDivision(Guid id, [FromBody] MoveTeamDivisionBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new MoveTeamCommand(id, body.DivisionId), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("{id:guid}/pool")]
    public async Task<IActionResult> AssignPool(Guid id, [FromBody] AssignTeamPoolBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new AssignTeamPoolCommand(id, body.PoolId), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/swap-pool")]
    public async Task<IActionResult> SwapPool(Guid id, [FromBody] SwapTeamPoolBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new SwapTeamPoolsCommand(id, body.OtherTeamId), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _mediator.Send(new DeleteTeamCommand(id), ct) ? NoContent() : NotFound();
}

public sealed record MoveTeamDivisionBody(Guid DivisionId);
public sealed record AssignTeamPoolBody(Guid? PoolId);
public sealed record SwapTeamPoolBody(Guid OtherTeamId);
