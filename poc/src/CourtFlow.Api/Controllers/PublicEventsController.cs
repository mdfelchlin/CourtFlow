using CourtFlow.Application.Features.Public;
using CourtFlow.Application.ReadModels.PublicEventReadiness;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/public/events")]
[AllowAnonymous]
[EnableRateLimiting("public-read")]
public class PublicEventsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicEventsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PublicEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new ListPublicEventsQuery(), ct));

    [HttpGet("past")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Past(CancellationToken ct)
        => Ok(await _mediator.Send(new ListPastPublicEventsQuery(), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PublicEventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPublicEventQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/schedule")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicMatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Schedule(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPublicScheduleQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/brackets")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicBracketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Brackets(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPublicBracketsQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/standings")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicPoolStandingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Standings(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPublicStandingsQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/readiness")]
    [ProducesResponseType(typeof(PublicEventReadinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Readiness(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPublicEventReadinessQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/teams")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicDivisionTeamsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Teams(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPublicTeamsQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
