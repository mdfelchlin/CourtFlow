using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/schedule")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class ScheduleController : ControllerBase
{
    private readonly IScheduleGenerator _generator;
    private readonly IConstraintChecker _checker;

    public ScheduleController(IScheduleGenerator generator, IConstraintChecker checker)
    {
        _generator = generator;
        _checker = checker;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid tournamentId, CancellationToken ct)
    {
        var matches = await _generator.GeneratePoolPlayAsync(tournamentId, ct);
        return Ok(new { matchesCreated = matches.Count });
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check(Guid tournamentId, CancellationToken ct)
    {
        var report = await _checker.CheckTournamentAsync(tournamentId, ct);
        return Ok(report);
    }
}
