using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Matches;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.Scheduling;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/schedule-versions")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public sealed class ScheduleVersionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ScheduleVersionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(Guid tournamentId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListScheduleVersionsQuery(tournamentId), ct));

    [HttpPost("preview-move")]
    public async Task<IActionResult> PreviewMove(Guid tournamentId, [FromBody] PreviewScheduleMoveBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new PreviewScheduleMoveCommand(tournamentId, body.MatchId, body.CourtId, body.ScheduledStartUtc, body.DurationMinutes),
            ct);
        if (r is null) return NotFound();
        var dto = new PreviewScheduleMoveResponseDto(
            r.ScheduleVersionId,
            r.MutationSequence,
            ToConflictDto(r.Conflicts),
            r.ApplyToken,
            r.CanApply);
        return Ok(dto);
    }

    [HttpPost("apply-move")]
    [ProducesResponseType(typeof(MatchDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyMove(Guid tournamentId, [FromBody] ApplyScheduleMoveBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ApplyScheduleMoveCommand(tournamentId, body.ApplyToken), ct);
        return r is null ? NotFound() : Ok(r);
    }

    private static ConflictReportDto ToConflictDto(ConflictReport r) =>
        new(r.Conflicts.Select(c => new ScheduleConflictDto(
                c.Kind,
                c.MatchId,
                c.OtherMatchId,
                c.TeamId,
                c.CourtId,
                c.StartUtc,
                c.EndUtc,
                c.Message))
            .ToList());
}

/// <remarks>Thin API models so OpenAPI aligns with ProblemDetails payloads.</remarks>
public sealed record PreviewScheduleMoveBody(Guid MatchId, Guid? CourtId, DateTime ScheduledStartUtc, int? DurationMinutes);
public sealed record ApplyScheduleMoveBody(string ApplyToken);
public sealed record PreviewScheduleMoveResponseDto(
    Guid ScheduleVersionId,
    long MutationSequence,
    ConflictReportDto Conflicts,
    string ApplyToken,
    bool CanApply);
public sealed record ConflictReportDto(IReadOnlyList<ScheduleConflictDto> Conflicts);
public sealed record ScheduleConflictDto(
    ConflictKind Kind,
    Guid MatchId,
    Guid? OtherMatchId,
    Guid? TeamId,
    Guid? CourtId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Message);
