using CourtFlow.Application.ReadModels.TiebreakerChain;
using CourtFlow.Application.Standings;
using CourtFlow.Domain.Enums;
using DomainTiebreaker = CourtFlow.Domain.Enums.DivisionTiebreakerPreset;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Standings;

public class StandingsCalculator : IStandingsCalculator
{
    private readonly AppDbContext _db;
    private readonly ITiebreakerChain _tiebreaker;

    public StandingsCalculator(AppDbContext db, ITiebreakerChain tiebreaker)
    {
        _db = db;
        _tiebreaker = tiebreaker;
    }

    public async Task<DivisionStandingsResult> ForDivisionAsync(Guid divisionId, CancellationToken ct = default)
    {
        var division = await _db.Divisions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == divisionId, ct)
                       ?? throw new InvalidOperationException("Division not found.");
        var preset = MapPreset(division.TiebreakerPreset);

        var pools = await _db.Pools.Where(p => p.DivisionId == divisionId).ToListAsync(ct);
        var teams = await _db.Teams.Where(t => t.DivisionId == divisionId).ToListAsync(ct);
        var matches = await _db.Matches
            .Where(m => m.DivisionId == divisionId && m.PoolId != null && m.State == MatchState.Final)
            .ToListAsync(ct);
        var matchIds = matches.Select(m => m.Id).ToHashSet();
        var sets = await _db.MatchSets.Where(s => matchIds.Contains(s.MatchId)).ToListAsync(ct);

        var result = new List<PoolStandings>();

        foreach (var pool in pools)
        {
            var poolTeams = teams.Where(t => t.PoolId == pool.Id).ToList();
            var poolMatches = matches.Where(m => m.PoolId == pool.Id).ToList();

            var rankedChain = _tiebreaker.OrderTeams(
                poolTeams.Select(t => (t.Id, t.Name)).ToList(),
                poolMatches,
                sets,
                preset);

            var ranked = rankedChain.Select(r =>
            {
                var played = poolMatches.Count(m => m.HomeTeamId == r.TeamId || m.AwayTeamId == r.TeamId);
                var wins = poolMatches.Count(m => m.WinnerTeamId == r.TeamId);
                var losses = played - wins;
                var (setsWon, setsLost, pointsFor, pointsAgainst) = AggregateSets(
                    poolMatches.Where(m => m.HomeTeamId == r.TeamId || m.AwayTeamId == r.TeamId).ToList(),
                    r.TeamId,
                    sets);
                var setPercentage = setsWon + setsLost == 0 ? 0 : (double)setsWon / (setsWon + setsLost);
                var pointPercentage = pointsFor + pointsAgainst == 0 ? 0 : (double)pointsFor / (pointsFor + pointsAgainst);
                return new PoolStandingRow(
                    r.TeamId, r.TeamName, r.Rank, played, wins, losses,
                    setsWon, setsLost, setPercentage, pointsFor, pointsAgainst, pointPercentage);
            }).ToList();

            result.Add(new PoolStandings(pool.Id, pool.Name, ranked));
        }

        return new DivisionStandingsResult(division.TiebreakerPreset.ToString(), result);
    }

    private static TiebreakerPreset MapPreset(DomainTiebreaker preset) => preset switch
    {
        DomainTiebreaker.WinsSetsPoints => TiebreakerPreset.WinsSetsPoints,
        DomainTiebreaker.SetsPoints => TiebreakerPreset.SetsPoints,
        DomainTiebreaker.H2HSetsPoints => TiebreakerPreset.H2HSetsPoints,
        _ => TiebreakerPreset.WinsH2HSetsPoints
    };

    private static (int setsWon, int setsLost, int pointsFor, int pointsAgainst) AggregateSets(
        List<Domain.Entities.Match> matches, Guid teamId, List<Domain.Entities.MatchSet> sets)
    {
        var setsWon = 0; var setsLost = 0; var pointsFor = 0; var pointsAgainst = 0;
        foreach (var m in matches)
        {
            var isHome = m.HomeTeamId == teamId;
            var matchSets = sets.Where(s => s.MatchId == m.Id).ToList();
            foreach (var s in matchSets)
            {
                var my = isHome ? s.HomeScore : s.AwayScore;
                var their = isHome ? s.AwayScore : s.HomeScore;
                pointsFor += my;
                pointsAgainst += their;
                if (my > their) setsWon++;
                else if (their > my) setsLost++;
            }
        }
        return (setsWon, setsLost, pointsFor, pointsAgainst);
    }
}
