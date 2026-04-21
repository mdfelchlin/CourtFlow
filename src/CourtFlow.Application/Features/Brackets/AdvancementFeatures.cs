using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Application.Standings;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Brackets;

public sealed record GeneratePoolAdvancementBracketsCommand(
    Guid DivisionId,
    int TopNPerPool,
    BracketType Type,
    bool IfNecessaryFinal,
    IReadOnlyList<Guid>? GoldTeamIds,
    IReadOnlyList<Guid>? SilverTeamIds) : IRequest<GeneratePoolAdvancementBracketsResult>;

public sealed record GeneratePoolAdvancementBracketsResult(IReadOnlyList<BracketDto> Brackets, string? Error);

public sealed class GeneratePoolAdvancementBracketsValidator : AbstractValidator<GeneratePoolAdvancementBracketsCommand>
{
    public GeneratePoolAdvancementBracketsValidator()
    {
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.TopNPerPool).GreaterThanOrEqualTo(1);
    }
}

public sealed class GeneratePoolAdvancementBracketsHandler : IRequestHandler<GeneratePoolAdvancementBracketsCommand, GeneratePoolAdvancementBracketsResult>
{
    private readonly IAppDbContext _db;
    private readonly IBracketBuilder _bracketBuilder;
    private readonly IStandingsCalculator _standings;

    public GeneratePoolAdvancementBracketsHandler(IAppDbContext db, IBracketBuilder bracketBuilder, IStandingsCalculator standings)
    {
        _db = db;
        _bracketBuilder = bracketBuilder;
        _standings = standings;
    }

    public async Task<GeneratePoolAdvancementBracketsResult> Handle(GeneratePoolAdvancementBracketsCommand request, CancellationToken ct)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null)
            return new GeneratePoolAdvancementBracketsResult(Array.Empty<BracketDto>(), "Division not found.");

        var divisionTeamIds = await _db.Teams.Where(t => t.DivisionId == division.Id).Select(t => t.Id).ToHashSetAsync(ct);

        var standingRows = await _standings.ForDivisionAsync(division.Id, ct);
        var (computedGold, computedSilver) = ComputeFromStandings(standingRows, request.TopNPerPool);

        var gold = request.GoldTeamIds is null ? computedGold : request.GoldTeamIds.ToList();
        var silver = request.SilverTeamIds is null ? computedSilver : request.SilverTeamIds.ToList();

        if (gold.Any(id => !divisionTeamIds.Contains(id)) || silver.Any(id => !divisionTeamIds.Contains(id)))
            return new GeneratePoolAdvancementBracketsResult(Array.Empty<BracketDto>(), "Advancement lists must contain only teams in this division.");

        if (gold.Count < 2 && silver.Count < 2)
            return new GeneratePoolAdvancementBracketsResult(Array.Empty<BracketDto>(), "Need at least 2 teams in Gold or Silver to generate a bracket.");

        var old = await _db.Brackets
            .Where(b => b.DivisionId == division.Id && (b.DisplayName == "Gold" || b.DisplayName == "Silver"))
            .ToListAsync(ct);
        foreach (var b in old)
            _db.Brackets.Remove(b);

        await _db.SaveChangesAsync(ct);

        var results = new List<BracketDto>();

        if (gold.Count >= 2)
        {
            var bracket = await _bracketBuilder.BuildAsync(
                division.TournamentId,
                division.Id,
                new BracketBuildOptions(request.Type, gold.Count, request.IfNecessaryFinal, "Gold"),
                ct);
            await SeedWinnersRoundOneAsync(bracket.Id, gold, ct);
            var refreshed = await _db.Brackets.FirstAsync(x => x.Id == bracket.Id, ct);
            results.Add(refreshed.ToDto());
        }

        if (silver.Count >= 2)
        {
            var bracket = await _bracketBuilder.BuildAsync(
                division.TournamentId,
                division.Id,
                new BracketBuildOptions(request.Type, silver.Count, request.IfNecessaryFinal, "Silver"),
                ct);
            await SeedWinnersRoundOneAsync(bracket.Id, silver, ct);
            var refreshed = await _db.Brackets.FirstAsync(x => x.Id == bracket.Id, ct);
            results.Add(refreshed.ToDto());
        }

        await _db.SaveChangesAsync(ct);
        return new GeneratePoolAdvancementBracketsResult(results, null);
    }

    private static (List<Guid> gold, List<Guid> silver) ComputeFromStandings(IReadOnlyList<PoolStandings> standings, int topN)
    {
        var gold = new List<Guid>();
        var silver = new List<Guid>();
        foreach (var pool in standings.OrderBy(p => p.PoolName, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var row in pool.Rows.Where(r => r.Rank <= topN).OrderBy(r => r.Rank))
                gold.Add(row.TeamId);
            foreach (var row in pool.Rows.Where(r => r.Rank > topN).OrderBy(r => r.Rank))
                silver.Add(row.TeamId);
        }
        return (gold, silver);
    }

    private async Task SeedWinnersRoundOneAsync(Guid bracketId, IReadOnlyList<Guid> teamIds, CancellationToken ct)
    {
        var nodes = await _db.BracketNodes
            .Where(n => n.BracketId == bracketId && n.Branch == BracketBranch.Winners && n.Round == 1)
            .OrderBy(n => n.Slot)
            .ToListAsync(ct);

        var idx = 0;
        var seed = 1;
        foreach (var node in nodes)
        {
            if (idx < teamIds.Count)
            {
                node.AssignTeam(1, teamIds[idx], seed++);
                idx++;
            }
            else
            {
                node.AssignTeam(1, null, null);
            }

            if (idx < teamIds.Count)
            {
                node.AssignTeam(2, teamIds[idx], seed++);
                idx++;
            }
            else
            {
                node.AssignTeam(2, null, null);
            }
        }
    }
}
