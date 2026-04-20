using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Imports;
using CourtFlow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/imports")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class ImportsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ImportsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("predefined-pools")]
    public async Task<IActionResult> ImportPredefinedPools([FromBody] ImportPredefinedPoolsRequest request, CancellationToken ct)
    {
        var cmd = new ImportPredefinedPoolsCommand(
            request.TournamentId,
            request.Rows.Select(r => new PredefinedPoolImportRow(
                r.RowNumber,
                r.DivisionName,
                r.PoolName,
                r.TeamName,
                r.Club,
                r.Wave,
                r.SeedHint)).ToList());

        return Ok(await _mediator.Send(cmd, ct));
    }

    [HttpPost("ranked-teams")]
    public async Task<IActionResult> ImportRankedTeams([FromBody] ImportRankedTeamsRequest request, CancellationToken ct)
    {
        var cmd = new ImportRankedTeamsCommand(
            request.TournamentId,
            request.Strategy,
            request.Rows.Select(r => new RankedTeamImportRow(
                r.RowNumber,
                r.DivisionName,
                r.Rank,
                r.TeamName,
                r.Club,
                r.Wave)).ToList());

        return Ok(await _mediator.Send(cmd, ct));
    }
}

public sealed record ImportPredefinedPoolsRequest(Guid TournamentId, IReadOnlyList<ImportPredefinedPoolRowRequest> Rows);
public sealed record ImportPredefinedPoolRowRequest(int RowNumber, string DivisionName, string PoolName, string TeamName, string? Club, Wave Wave, int? SeedHint);

public sealed record ImportRankedTeamsRequest(Guid TournamentId, RankedSeedStrategy Strategy, IReadOnlyList<ImportRankedTeamRowRequest> Rows);
public sealed record ImportRankedTeamRowRequest(int RowNumber, string DivisionName, int Rank, string TeamName, string? Club, Wave Wave);
