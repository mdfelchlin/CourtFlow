using CourtFlow.Application.Standings;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Standings;

public class StandingsCalculator : IStandingsCalculator
{
    private readonly AppDbContext _db;

    public StandingsCalculator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PoolStandings>> ForDivisionAsync(Guid divisionId, CancellationToken ct = default)
    {
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

            var rows = new List<PoolStandingRow>();

            foreach (var team in poolTeams)
            {
                var played = poolMatches.Where(m => m.HomeTeamId == team.Id || m.AwayTeamId == team.Id).ToList();
                var wins = played.Count(m => m.WinnerTeamId == team.Id);
                var losses = played.Count - wins;

                var (setsWon, setsLost, pointsFor, pointsAgainst) = AggregateSets(played, team.Id, sets);
                var setPercentage = (setsWon + setsLost) == 0 ? 0 : (double)setsWon / (setsWon + setsLost);
                var pointPercentage = (pointsFor + pointsAgainst) == 0 ? 0 : (double)pointsFor / (pointsFor + pointsAgainst);

                rows.Add(new PoolStandingRow(
                    team.Id, team.Name, 0, played.Count, wins, losses,
                    setsWon, setsLost, setPercentage, pointsFor, pointsAgainst, pointPercentage));
            }

            var ranked = rows
                .OrderByDescending(r => r.MatchWins)
                .ThenByDescending(r => r.SetPercentage)
                .ThenByDescending(r => r.PointPercentage)
                .Select((r, i) => r with { Rank = i + 1 })
                .ToList();

            result.Add(new PoolStandings(pool.Id, pool.Name, ranked));
        }

        return result;
    }

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
