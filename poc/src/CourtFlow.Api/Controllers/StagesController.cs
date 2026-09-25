using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/stages")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class StagesController : ControllerBase
{
    private readonly IMediator _mediator;

    public StagesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? divisionId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListStagesQuery(divisionId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var s = await _mediator.Send(new GetStageQuery(id), ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> Transition(Guid id, [FromBody] TransitionStageBody body, CancellationToken ct)
    {
        var s = await _mediator.Send(new TransitionStageStatusCommand(id, body.TargetStatus), ct);
        return s is null ? NotFound() : Ok(s);
    }
}

public sealed record TransitionStageBody(StageStatus TargetStatus);
