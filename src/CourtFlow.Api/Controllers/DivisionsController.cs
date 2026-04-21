using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Brackets;
using CourtFlow.Application.Features.Divisions;
using CourtFlow.Application.Features.Imports;
using CourtFlow.Application.Features.Teams;
using CourtFlow.Application.Standings;
using CourtFlow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/divisions")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class DivisionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStandingsCalculator _standings;
    public DivisionsController(IMediator mediator, IStandingsCalculator standings)
    {
        _mediator = mediator;
        _standings = standings;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? tournamentId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListDivisionsQuery(tournamentId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDivisionCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(Create), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDivisionCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var r = await _mediator.Send(cmd, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _mediator.Send(new DeleteDivisionCommand(id), ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/standings")]
    public async Task<IActionResult> Standings(Guid id, CancellationToken ct) =>
        Ok(await _standings.ForDivisionAsync(id, ct));

    [HttpPut("{id:guid}/team-order")]
    public async Task<IActionResult> ReorderTeams(Guid id, [FromBody] ReorderDivisionTeamsBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ReorderDivisionTeamsCommand(id, body.OrderedTeamIds), ct);
        return r is null ? BadRequest("Invalid team order.") : Ok(r);
    }

    [HttpPost("{id:guid}/randomize-seeds")]
    public async Task<IActionResult> RandomizeSeeds(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new RandomizeDivisionSeedsCommand(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/apply-seeding")]
    public async Task<IActionResult> ApplySeeding(Guid id, [FromBody] ApplySeedingBody body, CancellationToken ct)
    {
        var strategy = (RankedSeedStrategy)body.Strategy;
        var r = await _mediator.Send(new ApplyDivisionSeedingCommand(id, strategy), ct);
        return Ok(r);
    }

    [HttpPost("{id:guid}/advancement-brackets")]
    public async Task<IActionResult> AdvancementBrackets(Guid id, [FromBody] AdvancementBracketsBody body, CancellationToken ct)
    {
        var type = (BracketType)body.Type;
        var r = await _mediator.Send(new GeneratePoolAdvancementBracketsCommand(
            id,
            body.TopNPerPool,
            type,
            body.IfNecessaryFinal,
            body.GoldTeamIds,
            body.SilverTeamIds), ct);
        return Ok(r);
    }
}

public sealed record ReorderDivisionTeamsBody(IReadOnlyList<Guid> OrderedTeamIds);
public sealed record ApplySeedingBody(int Strategy);
public sealed record AdvancementBracketsBody(
    int TopNPerPool,
    int Type,
    bool IfNecessaryFinal,
    IReadOnlyList<Guid>? GoldTeamIds,
    IReadOnlyList<Guid>? SilverTeamIds);
