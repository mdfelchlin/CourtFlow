using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Divisions;
using CourtFlow.Application.Standings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/divisions")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class DivisionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStandingsCalculator _standings;
    public DivisionsController(IMediator mediator, IStandingsCalculator standings)
    {
        _mediator = mediator;
        _standings = standings;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? tournamentId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListDivisionsQuery(tournamentId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDivisionCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(Create), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDivisionCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var r = await _mediator.Send(cmd, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _mediator.Send(new DeleteDivisionCommand(id), ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/standings")]
    public async Task<IActionResult> Standings(Guid id, CancellationToken ct) =>
        Ok(await _standings.ForDivisionAsync(id, ct));
}
