using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Templates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class TemplatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TemplatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var templates = await _mediator.Send(new ListTemplatesQuery(), ct);
        return Ok(templates);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var t = await _mediator.Send(new GetTemplateQuery(id), ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost("capture")]
    public async Task<IActionResult> Capture([FromBody] CaptureTemplateBody body, CancellationToken ct)
    {
        var template = await _mediator.Send(new CaptureTemplateCommand(body.SourceTournamentId, body.Name), ct);
        return template is null
            ? NotFound()
            : CreatedAtAction(nameof(Get), new { id = template.Id }, template);
    }

    [HttpPost("{id:guid}/create-tournament")]
    public async Task<IActionResult> CreateTournament(Guid id, [FromBody] CreateTournamentFromTemplateBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateTournamentFromTemplateCommand(
            id,
            body.Name,
            body.StartDate,
            body.EndDate,
            body.TimeZoneId,
            body.DefaultMatchDurationMinutes), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _mediator.Send(new DeleteTemplateCommand(id), ct) ? NoContent() : NotFound();
}

public sealed record CaptureTemplateBody(Guid SourceTournamentId, string? Name);
public sealed record CreateTournamentFromTemplateBody(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes);
