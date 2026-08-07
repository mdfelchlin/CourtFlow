using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Brackets;
using CourtFlow.Application.Features.MatchGraph;
using CourtFlow.Application.Features.Divisions;
using CourtFlow.Application.Features.Imports;
using CourtFlow.Application.Features.Teams;
using CourtFlow.Application.Standings;
using CourtFlow.Application.StageMaterialization.Commands;
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
        var r = await _mediator.Send(new ApplyDivisionSeedingCommand(id, body.Strategy), ct);
        return Ok(r);
    }

    [HttpPost("{id:guid}/advancement-brackets")]
    public async Task<IActionResult> AdvancementBrackets(Guid id, [FromBody] AdvancementBracketsBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new GeneratePoolAdvancementBracketsCommand(
            id,
            body.TopNPerPool,
            body.Type,
            body.IfNecessaryFinal,
            body.GoldTeamIds,
            body.SilverTeamIds), ct);
        return Ok(r);
    }

    [HttpPost("{id:guid}/snake-into-pools")]
    public async Task<IActionResult> SnakeIntoPools(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SnakeIntoPoolsCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/generate-brackets")]
    public async Task<IActionResult> GenerateBrackets(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new GenerateBracketsCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/resolve-bracket-slots")]
    public async Task<IActionResult> ResolveBracketSlots(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ResolveBracketSlotsCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/seed-bracket")]
    public async Task<IActionResult> SeedBracketFromRankings(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SeedBracketFromRankingsCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/wizard")]
    public async Task<IActionResult> WizardState(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetDivisionWizardStateQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("{id:guid}/wizard/pools")]
    public async Task<IActionResult> SavePoolStructure(Guid id, [FromBody] SavePoolStructureBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new SaveDivisionPoolStructureCommand(id, body.Pools), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("{id:guid}/wizard/pool-settings")]
    public async Task<IActionResult> SavePoolSettings(Guid id, [FromBody] SavePoolSettingsBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new SaveDivisionPoolSettingsCommand(id, body.Settings), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("{id:guid}/wizard/brackets")]
    public async Task<IActionResult> SaveBracketStructure(Guid id, [FromBody] SaveBracketStructureBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new SaveDivisionBracketStructureCommand(id, body.Brackets), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPut("{id:guid}/wizard/advancement-rules")]
    public async Task<IActionResult> SaveAdvancementRules(Guid id, [FromBody] SaveAdvancementRulesBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new SaveDivisionAdvancementRulesCommand(id, body.Rules), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("{id:guid}/validation")]
    public async Task<IActionResult> Validation(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetDivisionValidationQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("{id:guid}/match-graph")]
    public async Task<IActionResult> MatchGraph(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetDivisionMatchGraphQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("{id:guid}/resolve-match-sources")]
    public async Task<IActionResult> ResolveMatchSources(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ResolveDivisionMatchSourcesCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/work-rotation")]
    public async Task<IActionResult> WorkRotation(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetDivisionWorkRotationQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("bracket-seed-slots/{slotId:guid}/override")]
    public async Task<IActionResult> SetBracketSlotOverride(Guid slotId, [FromBody] BracketSlotOverrideBody body, CancellationToken ct)
    {
        var r = await _mediator.Send(new SetBracketSlotOverrideCommand(slotId, body.ManualTournamentTeamId, body.Reason), ct);
        return r is null ? NotFound() : Ok(r);
    }
}

public sealed record BracketSlotOverrideBody(Guid? ManualTournamentTeamId, string? Reason);

public sealed record SavePoolStructureBody(IReadOnlyList<DivisionPoolStructureDto> Pools);
public sealed record SavePoolSettingsBody(PoolSettingsDto Settings);
public sealed record SaveBracketStructureBody(IReadOnlyList<DivisionBracketStructureDto> Brackets);
public sealed record SaveAdvancementRulesBody(IReadOnlyList<DivisionAdvancementRuleDto> Rules);

public sealed record ReorderDivisionTeamsBody(IReadOnlyList<Guid> OrderedTeamIds);
public sealed record ApplySeedingBody(RankedSeedStrategy Strategy);
public sealed record AdvancementBracketsBody(
    int TopNPerPool,
    BracketType Type,
    bool IfNecessaryFinal,
    IReadOnlyList<Guid>? GoldTeamIds,
    IReadOnlyList<Guid>? SilverTeamIds);
