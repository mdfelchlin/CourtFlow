using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Imports;
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

        var ordered = teams
            .OrderBy(t => t.SeedHint ?? int.MaxValue)
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
            var team = ordered[i];
            var poolIndex = poolSequence[i];
            var pool = divisionPools[poolIndex];
            team.AssignPool(pool.Id);
            team.SetSeedHint(i + 1);
        }

        await _db.SaveChangesAsync(ct);
        return new ApplyDivisionSeedingResult(true, null);
    }
}
