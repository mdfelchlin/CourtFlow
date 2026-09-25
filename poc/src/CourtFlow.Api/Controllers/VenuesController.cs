using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Venues;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/venues")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class VenuesController : ControllerBase
{
    private readonly IMediator _mediator;
    public VenuesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? tournamentId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListVenuesQuery(tournamentId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetVenueQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVenueCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(Get), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVenueCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var r = await _mediator.Send(cmd, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _mediator.Send(new DeleteVenueCommand(id), ct) ? NoContent() : NotFound();
}
