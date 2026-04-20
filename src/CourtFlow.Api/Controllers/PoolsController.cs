using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Pools;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/pools")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class PoolsController : ControllerBase
{
    private readonly IMediator _mediator;
    public PoolsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? divisionId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListPoolsQuery(divisionId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePoolCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(Create), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePoolCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var r = await _mediator.Send(cmd, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _mediator.Send(new DeletePoolCommand(id), ct) ? NoContent() : NotFound();
}
