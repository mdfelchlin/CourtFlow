using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.CourtStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tournaments/{tournamentId:guid}/court-status")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class CourtStatusController : ControllerBase
{
    private readonly ICourtStatusService _svc;
    public CourtStatusController(ICourtStatusService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Get(Guid tournamentId, CancellationToken ct) =>
        Ok(await _svc.GetForTournamentAsync(tournamentId, ct));
}
