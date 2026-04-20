using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Imports;

public enum RankedSeedStrategy { Snake = 1 }

public sealed record ImportErrorDto(int RowNumber, string Message);
public sealed record ImportResultDto(int CreatedPools, int CreatedTeams, int UpdatedTeams, IReadOnlyList<ImportErrorDto> Errors);

public sealed record PredefinedPoolImportRow(
    int RowNumber,
    string DivisionName,
    string PoolName,
    string TeamName,
    string? Club,
    Wave Wave,
    int? SeedHint);

public sealed record RankedTeamImportRow(
    int RowNumber,
    string DivisionName,
    int Rank,
    string TeamName,
    string? Club,
    Wave Wave);

public sealed record ImportPredefinedPoolsCommand(Guid TournamentId, bool DryRun, IReadOnlyList<PredefinedPoolImportRow> Rows) : IRequest<ImportResultDto>;
public sealed record ImportRankedTeamsCommand(Guid TournamentId, bool DryRun, RankedSeedStrategy Strategy, IReadOnlyList<RankedTeamImportRow> Rows) : IRequest<ImportResultDto>;

public sealed class ImportPredefinedPoolsValidator : AbstractValidator<ImportPredefinedPoolsCommand>
{
    public ImportPredefinedPoolsValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.Rows).NotEmpty();
    }
}

public sealed class ImportRankedTeamsValidator : AbstractValidator<ImportRankedTeamsCommand>
{
    public ImportRankedTeamsValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.Rows).NotEmpty();
    }
}

public sealed class ImportPredefinedPoolsHandler : IRequestHandler<ImportPredefinedPoolsCommand, ImportResultDto>
{
    private readonly IAppDbContext _db;
    public ImportPredefinedPoolsHandler(IAppDbContext db) => _db = db;

    public async Task<ImportResultDto> Handle(ImportPredefinedPoolsCommand request, CancellationToken ct)
    {
        var errors = new List<ImportErrorDto>();
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == request.TournamentId, ct);
        if (tournament is null)
        {
            return new ImportResultDto(0, 0, 0, new[] { new ImportErrorDto(0, "Tournament not found.") });
        }

        var poolTeamCounts = request.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.DivisionName) && !string.IsNullOrWhiteSpace(r.PoolName) && !string.IsNullOrWhiteSpace(r.TeamName))
            .GroupBy(r => $"{r.DivisionName.Trim().ToUpperInvariant()}|{r.PoolName.Trim().ToUpperInvariant()}");
        foreach (var poolGroup in poolTeamCounts)
        {
            var uniqueTeams = poolGroup
                .Select(r => r.TeamName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (uniqueTeams >= 3) continue;

            foreach (var row in poolGroup)
            {
                errors.Add(new ImportErrorDto(row.RowNumber, $"Pool '{row.PoolName}' in division '{row.DivisionName}' must have at least 3 teams."));
            }
        }
        if (errors.Count > 0)
        {
            return new ImportResultDto(0, 0, 0, errors);
        }

        var divisionNames = request.Rows.Select(x => x.DivisionName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var divisions = await _db.Divisions
            .Where(d => d.TournamentId == request.TournamentId && divisionNames.Contains(d.Name))
            .ToListAsync(ct);
        var divisionByName = divisions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        var divisionIds = divisions.Select(x => x.Id).ToHashSet();
        var pools = await _db.Pools.Where(p => divisionIds.Contains(p.DivisionId)).ToListAsync(ct);
        var teams = await _db.Teams.Where(t => divisionIds.Contains(t.DivisionId)).ToListAsync(ct);

        var createdPools = 0;
        var createdTeams = 0;
        var updatedTeams = 0;

        foreach (var row in request.Rows)
        {
            if (!divisionByName.TryGetValue(row.DivisionName.Trim(), out var division))
            {
                division = new Division(tournament.OrganizationId, tournament.Id, row.DivisionName.Trim(), DivisionFormat.PoolPlusBracket, 64);
                divisions.Add(division);
                divisionByName[division.Name] = division;
                if (!request.DryRun)
                {
                    _db.Divisions.Add(division);
                }
            }

            var poolName = row.PoolName.Trim();
            if (string.IsNullOrWhiteSpace(poolName))
            {
                errors.Add(new ImportErrorDto(row.RowNumber, "Pool name is required."));
                continue;
            }

            var teamName = row.TeamName.Trim();
            if (string.IsNullOrWhiteSpace(teamName))
            {
                errors.Add(new ImportErrorDto(row.RowNumber, "Team name is required."));
                continue;
            }

            var pool = pools.FirstOrDefault(p =>
                p.DivisionId == division.Id &&
                string.Equals(p.Name, poolName, StringComparison.OrdinalIgnoreCase));

            if (pool is null)
            {
                pool = new Pool(division.OrganizationId, division.Id, poolName);
                pools.Add(pool);
                createdPools++;
                if (!request.DryRun)
                {
                    _db.Pools.Add(pool);
                }
            }

            var team = teams.FirstOrDefault(t =>
                t.DivisionId == division.Id &&
                string.Equals(t.Name, teamName, StringComparison.OrdinalIgnoreCase));

            if (team is null)
            {
                team = new Team(division.OrganizationId, division.Id, teamName, row.Wave, pool.Id, row.Club, row.SeedHint);
                teams.Add(team);
                createdTeams++;
                if (!request.DryRun)
                {
                    _db.Teams.Add(team);
                }
            }
            else
            {
                team.AssignPool(pool.Id);
                team.SetWave(row.Wave);
                team.SetClub(row.Club);
                team.SetSeedHint(row.SeedHint);
                updatedTeams++;
            }
        }

        if (!request.DryRun && errors.Count == 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new ImportResultDto(createdPools, createdTeams, updatedTeams, errors);
    }
}

public sealed class ImportRankedTeamsHandler : IRequestHandler<ImportRankedTeamsCommand, ImportResultDto>
{
    private readonly IAppDbContext _db;
    public ImportRankedTeamsHandler(IAppDbContext db) => _db = db;

    public async Task<ImportResultDto> Handle(ImportRankedTeamsCommand request, CancellationToken ct)
    {
        var errors = new List<ImportErrorDto>();
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == request.TournamentId, ct);
        if (tournament is null)
        {
            return new ImportResultDto(0, 0, 0, new[] { new ImportErrorDto(0, "Tournament not found.") });
        }

        var divisionNames = request.Rows.Select(x => x.DivisionName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var divisions = await _db.Divisions
            .Where(d => d.TournamentId == request.TournamentId && divisionNames.Contains(d.Name))
            .ToListAsync(ct);
        var divisionByName = divisions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

        var duplicateRanks = request.Rows
            .GroupBy(r => $"{r.DivisionName.Trim().ToUpperInvariant()}|{r.Rank}")
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(r => r.RowNumber))
            .ToHashSet();
        foreach (var rowNumber in duplicateRanks)
        {
            errors.Add(new ImportErrorDto(rowNumber, "Duplicate rank found in same division."));
        }

        if (errors.Count > 0)
        {
            return new ImportResultDto(0, 0, 0, errors);
        }

        var divisionIds = divisions.Select(d => d.Id).ToHashSet();
        var pools = await _db.Pools.Where(p => divisionIds.Contains(p.DivisionId)).ToListAsync(ct);
        var teams = await _db.Teams.Where(t => divisionIds.Contains(t.DivisionId)).ToListAsync(ct);

        var createdPools = 0;
        var createdTeams = 0;
        var updatedTeams = 0;

        foreach (var divisionGroup in request.Rows.GroupBy(r => r.DivisionName.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (!divisionByName.TryGetValue(divisionGroup.Key, out var division))
            {
                division = new Division(tournament.OrganizationId, tournament.Id, divisionGroup.Key, DivisionFormat.PoolPlusBracket, 64);
                divisions.Add(division);
                divisionByName[division.Name] = division;
                if (!request.DryRun)
                {
                    _db.Divisions.Add(division);
                }
            }
            var ordered = divisionGroup.OrderBy(r => r.Rank).ToList();
            var poolSizes = BuildPoolSizes(ordered.Count);
            if (poolSizes.Count == 0)
            {
                var rowNumber = ordered.FirstOrDefault()?.RowNumber ?? 0;
                errors.Add(new ImportErrorDto(rowNumber, $"Division '{divisionGroup.Key}' has {ordered.Count} team(s); minimum is 3."));
                continue;
            }

            var poolNames = Enumerable.Range(0, poolSizes.Count)
                .Select(i => $"Pool {(char)('A' + i)}")
                .ToList();
            var divisionPools = new List<Pool>();
            foreach (var name in poolNames)
            {
                var pool = pools.FirstOrDefault(p =>
                    p.DivisionId == division.Id &&
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (pool is null)
                {
                    pool = new Pool(division.OrganizationId, division.Id, name);
                    pools.Add(pool);
                    createdPools++;
                    if (!request.DryRun)
                    {
                        _db.Pools.Add(pool);
                    }
                }
                divisionPools.Add(pool);
            }

            var poolSequence = request.Strategy switch
            {
                RankedSeedStrategy.Snake => BuildSnakePoolSequence(poolSizes),
                _ => BuildSnakePoolSequence(poolSizes)
            };

            for (var i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                var poolIndex = poolSequence[i];
                var pool = divisionPools[poolIndex];

                var team = teams.FirstOrDefault(t =>
                    t.DivisionId == division.Id &&
                    string.Equals(t.Name, row.TeamName.Trim(), StringComparison.OrdinalIgnoreCase));

                if (team is null)
                {
                    team = new Team(division.OrganizationId, division.Id, row.TeamName.Trim(), row.Wave, pool.Id, row.Club, row.Rank);
                    teams.Add(team);
                    createdTeams++;
                    if (!request.DryRun)
                    {
                        _db.Teams.Add(team);
                    }
                }
                else
                {
                    team.AssignPool(pool.Id);
                    team.SetWave(row.Wave);
                    team.SetClub(row.Club);
                    team.SetSeedHint(row.Rank);
                    updatedTeams++;
                }
            }
        }

        if (!request.DryRun && errors.Count == 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new ImportResultDto(createdPools, createdTeams, updatedTeams, errors);
    }

    public static int GetSnakePoolIndex(int position, int poolCount)
    {
        var block = position / poolCount;
        var offset = position % poolCount;
        return block % 2 == 0 ? offset : poolCount - 1 - offset;
    }

    public static IReadOnlyList<int> BuildPoolSizes(int teamCount)
    {
        if (teamCount < 3) return Array.Empty<int>();
        if (teamCount == 5) return new[] { 5 };

        for (var poolsOfFour = teamCount / 4; poolsOfFour >= 0; poolsOfFour--)
        {
            var remainder = teamCount - (poolsOfFour * 4);
            if (remainder % 3 != 0) continue;
            var poolsOfThree = remainder / 3;
            var result = new List<int>(poolsOfFour + poolsOfThree);
            for (var i = 0; i < poolsOfFour; i++) result.Add(4);
            for (var i = 0; i < poolsOfThree; i++) result.Add(3);
            return result;
        }

        return Array.Empty<int>();
    }

    public static IReadOnlyList<int> BuildSnakePoolSequence(IReadOnlyList<int> poolSizes)
    {
        var remaining = poolSizes.ToArray();
        var sequence = new List<int>(remaining.Sum());

        while (remaining.Any(x => x > 0))
        {
            for (var i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] <= 0) continue;
                sequence.Add(i);
                remaining[i]--;
            }
            for (var i = remaining.Length - 1; i >= 0; i--)
            {
                if (remaining[i] <= 0) continue;
                sequence.Add(i);
                remaining[i]--;
            }
        }

        return sequence;
    }
}
