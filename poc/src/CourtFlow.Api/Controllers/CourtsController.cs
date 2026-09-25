using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Courts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/courts")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class CourtsController : ControllerBase
{
    private readonly IMediator _mediator;
    public CourtsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? venueId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListCourtsQuery(venueId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourtCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(Create), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourtCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var r = await _mediator.Send(cmd, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _mediator.Send(new DeleteCourtCommand(id), ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/delay")]
    public async Task<IActionResult> Delay(Guid id, [FromQuery] int slots, CancellationToken ct)
    {
        if (slots <= 0) return BadRequest("slots must be > 0");
        var count = await _mediator.Send(new DelayCourtCommand(id, slots), ct);
        return Ok(new { matchesDelayed = count });
    }
}
