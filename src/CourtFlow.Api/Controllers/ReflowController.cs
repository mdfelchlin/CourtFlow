using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Reflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/reflow")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public sealed class ReflowController : ControllerBase
{
    private readonly IReflowPreviewEngine _engine;
    private readonly ITenantContext _tenant;

    public ReflowController(IReflowPreviewEngine engine, ITenantContext tenant)
    {
        _engine = engine;
        _tenant = tenant;
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(Guid tournamentId, CancellationToken ct)
    {
        var delta = await _engine.PreviewAsync(tournamentId, ct);
        return Ok(new ReflowPreviewResponse(
            delta.Notes.ToList(),
            delta.Moves.Select(m => new ReflowMoveDto(
                m.MatchAssignmentId,
                m.MatchId,
                m.OldScheduledStart,
                m.NewScheduledStart,
                m.ShiftMinutes)).ToList(),
            delta.ApplyToken));
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply(Guid tournamentId, [FromBody] ReflowApplyBody body, CancellationToken ct)
    {
        var result = await _engine.ApplyAsync(tournamentId, body.ApplyToken, _tenant.UserId, ct);
        return Ok(new ReflowApplyResponse(result.RepairJobId, result.AssignmentsUpdated));
    }
}

public sealed record ReflowPreviewResponse(
    IReadOnlyList<string> Notes,
    IReadOnlyList<ReflowMoveDto> Moves,
    string? ApplyToken);

public sealed record ReflowMoveDto(
    Guid MatchAssignmentId,
    Guid MatchId,
    DateTime OldScheduledStart,
    DateTime NewScheduledStart,
    int ShiftMinutes);

public sealed record ReflowApplyBody(string ApplyToken);
public sealed record ReflowApplyResponse(Guid RepairJobId, int AssignmentsUpdated);
