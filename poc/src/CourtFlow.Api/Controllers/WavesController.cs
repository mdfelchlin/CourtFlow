using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Waves;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/waves")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class WavesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WavesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(Guid tournamentId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListWavesQuery(tournamentId), ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid tournamentId, [FromBody] CreateWaveBody body, CancellationToken ct)
    {
        try
        {
            var wave = await _mediator.Send(
                new CreateWaveCommand(tournamentId, body.Code, body.DefaultStartTime, body.DefaultEndTime, body.Notes),
                ct);
            return Ok(wave);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid tournamentId, Guid id, [FromBody] UpdateWaveBody body, CancellationToken ct)
    {
        _ = tournamentId;
        try
        {
            var wave = await _mediator.Send(
                new UpdateWaveCommand(id, body.DefaultStartTime, body.DefaultEndTime, body.Notes),
                ct);
            return wave is null ? NotFound() : Ok(wave);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid tournamentId, Guid id, CancellationToken ct)
    {
        _ = tournamentId;
        var ok = await _mediator.Send(new DeleteWaveCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }
}

public sealed record CreateWaveBody(string Code, string? DefaultStartTime, string? DefaultEndTime, string? Notes);
public sealed record UpdateWaveBody(string? DefaultStartTime, string? DefaultEndTime, string? Notes);
