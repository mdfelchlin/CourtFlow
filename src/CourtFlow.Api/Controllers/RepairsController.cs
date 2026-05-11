using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/repairs")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class RepairsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public RepairsController(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid tournamentId, CancellationToken ct)
    {
        var jobs = await _db.RepairJobs
            .Where(j => j.TournamentId == tournamentId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Select(j => new RepairJobDto(j.Id, j.TriggerType, j.Status, j.DivisionId, j.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(jobs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid tournamentId, Guid id, CancellationToken ct)
    {
        var job = await _db.RepairJobs
            .Include(j => j.Actions)
            .FirstOrDefaultAsync(j => j.Id == id && j.TournamentId == tournamentId, ct);
        if (job is null) return NotFound();
        return Ok(new RepairJobDetailDto(
            job.Id, job.TriggerType, job.Status, job.DivisionId, job.CreatedAtUtc,
            job.PreviewDiagnosticsJson,
            job.Actions.Select(a => new RepairActionDto(a.Id, a.ActionType, a.TargetId, a.BeforeJson, a.AfterJson)).ToList()));
    }

    [HttpPost("team-withdrawal")]
    public async Task<IActionResult> TeamWithdrawal(Guid tournamentId, [FromBody] TeamWithdrawalBody body, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException();
        var tt = await _db.TournamentTeams.FirstOrDefaultAsync(
            t => t.Id == body.TournamentTeamId && t.TournamentId == tournamentId, ct);
        if (tt is null) return NotFound();

        tt.Withdraw(body.Reason, body.EffectiveAfterMatchId);

        var job = new RepairJob(orgId, tournamentId, RepairTriggerType.TeamWithdrawal, divisionId: tt.DivisionId);
        job.AddAction(RepairActionType.TeamWithdrawal, body.TournamentTeamId.ToString(), null, null);
        job.MarkApplied();
        _db.RepairJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return Ok(new RepairJobDto(job.Id, job.TriggerType, job.Status, job.DivisionId, job.CreatedAtUtc));
    }

    [HttpPost("court-outage")]
    public async Task<IActionResult> CourtOutage(Guid tournamentId, [FromBody] CourtOutageBody body, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException();
        var outage = new CourtOutage(orgId, body.CourtId, body.StartsAt, body.EndsAt, body.Reason);
        _db.CourtOutages.Add(outage);

        var job = new RepairJob(orgId, tournamentId, RepairTriggerType.CourtOutage);
        job.AddAction(RepairActionType.CourtOutageMarked, body.CourtId.ToString(), null, null);
        job.MarkApplied();
        _db.RepairJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return Ok(new RepairJobDto(job.Id, job.TriggerType, job.Status, job.DivisionId, job.CreatedAtUtc));
    }
}

public sealed record RepairJobDto(Guid Id, RepairTriggerType TriggerType, RepairJobStatus Status, Guid? DivisionId, DateTime CreatedAt);
public sealed record RepairJobDetailDto(
    Guid Id, RepairTriggerType TriggerType, RepairJobStatus Status, Guid? DivisionId, DateTime CreatedAt,
    string? PreviewDiagnosticsJson,
    IReadOnlyList<RepairActionDto> Actions);
public sealed record RepairActionDto(Guid Id, RepairActionType ActionType, string? TargetId, string? Before, string? After);
public sealed record TeamWithdrawalBody(Guid TournamentTeamId, string? Reason, Guid? EffectiveAfterMatchId);
public sealed record CourtOutageBody(Guid CourtId, DateTime StartsAt, DateTime EndsAt, string? Reason);
