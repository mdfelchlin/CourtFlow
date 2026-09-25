using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Imports;
using CourtFlow.Application.Features.Teams;
using CourtFlow.Application.Seeding;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Divisions;

public sealed record ApplyDivisionSeedingCommand(Guid DivisionId, RankedSeedStrategy Strategy) : IRequest<ApplyDivisionSeedingResult>;

public sealed record ApplyDivisionSeedingResult(bool Ok, string? Error);

public sealed class ApplyDivisionSeedingValidator : AbstractValidator<ApplyDivisionSeedingCommand>
{
    public ApplyDivisionSeedingValidator()
    {
        RuleFor(x => x.DivisionId).NotEmpty();
    }
}

public sealed class ApplyDivisionSeedingHandler : IRequestHandler<ApplyDivisionSeedingCommand, ApplyDivisionSeedingResult>
{
    private readonly IAppDbContext _db;

    public ApplyDivisionSeedingHandler(IAppDbContext db) => _db = db;

    public async Task<ApplyDivisionSeedingResult> Handle(ApplyDivisionSeedingCommand request, CancellationToken ct)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null)
            return new ApplyDivisionSeedingResult(false, "Division not found.");

        var teams = await _db.Teams.Where(t => t.DivisionId == division.Id).ToListAsync(ct);
        if (teams.Count < 3)
            return new ApplyDivisionSeedingResult(false, "At least 3 teams are required to build pools.");

        await BackfillDivisionRankingsFromLegacySeedsAsync(division, teams, ct);

        var rankByTeam = new Dictionary<Guid, int>(
            await DivisionEntryRankingService.GetRankByTeamIdAsync(_db, division.Id, ct));
        var ordered = teams
            .OrderBy(t => rankByTeam.TryGetValue(t.Id, out var rk) ? rk : int.MaxValue)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var poolSizes = PoolSeeding.BuildPoolSizes(ordered.Count);
        if (poolSizes.Count == 0)
            return new ApplyDivisionSeedingResult(false, $"Cannot derive pool layout for {ordered.Count} teams.");

        var poolNames = Enumerable.Range(0, poolSizes.Count)
            .Select(i => $"Pool {(char)('A' + i)}")
            .ToList();

        var pools = await _db.Pools.Where(p => p.DivisionId == division.Id).ToListAsync(ct);
        var divisionPools = new List<Pool>();
        foreach (var name in poolNames)
        {
            var pool = pools.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (pool is null)
            {
                pool = new Pool(division.OrganizationId, division.Id, name);
                pools.Add(pool);
                _db.Pools.Add(pool);
            }
            divisionPools.Add(pool);
        }

        var poolSequence = request.Strategy switch
        {
            RankedSeedStrategy.Snake => PoolSeeding.BuildSnakePoolSequence(poolSizes),
            _ => PoolSeeding.BuildSnakePoolSequence(poolSizes)
        };

        for (var i = 0; i < ordered.Count; i++)
        {
            var poolIndex = poolSequence[i];
            ordered[i].AssignPool(divisionPools[poolIndex].Id);
        }

        await _db.SaveChangesAsync(ct);
        return new ApplyDivisionSeedingResult(true, null);
    }

    private async Task BackfillDivisionRankingsFromLegacySeedsAsync(
        Division division,
        List<Team> teams,
        CancellationToken ct)
    {
        var rankByTeam = new Dictionary<Guid, int>(
            await DivisionEntryRankingService.GetRankByTeamIdAsync(_db, division.Id, ct));

        var missingRanking = teams
            .Where(t => !rankByTeam.ContainsKey(t.Id))
            .OrderBy(t => t.SeedHint ?? int.MaxValue)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingRanking.Count == 0)
            return;

        var appendMode = rankByTeam.Count > 0;
        var maxExisting = rankByTeam.Values.DefaultIfEmpty(0).Max();
        var next = Math.Max(maxExisting, 0) + 1;

        foreach (var team in missingRanking)
        {
            var tt = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, team, ct);
            await DivisionEntryRankingService.UpsertRankingAsync(
                _db,
                division,
                tt,
                next,
                source: appendMode ? "append_legacy" : "seed_legacy",
                isManualOverride: false,
                ct);
            appendMode = true;
            next++;
        }
    }
}
