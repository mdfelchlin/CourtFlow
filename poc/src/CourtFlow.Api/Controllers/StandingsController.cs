using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Standings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/divisions/{divisionId:guid}/standings")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class StandingsController : ControllerBase
{
    private readonly IStandingsCalculator _standings;
    public StandingsController(IStandingsCalculator standings) => _standings = standings;

    [HttpGet]
    public async Task<IActionResult> Get(Guid divisionId, CancellationToken ct) =>
        Ok(await _standings.ForDivisionAsync(divisionId, ct));
}
