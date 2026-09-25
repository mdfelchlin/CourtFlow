using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.ReadModels.TiebreakerChain;

public enum TiebreakerPreset
{
    WinsH2HSetsPoints = 0,
    WinsSetsPoints = 1,
    SetsPoints = 2,
    H2HSetsPoints = 3
}

public sealed record TiebreakerRankedTeam(
    Guid TeamId,
    string TeamName,
    int Rank,
    string? TiebreakStep);

public interface ITiebreakerChain
{
    IReadOnlyList<TiebreakerRankedTeam> OrderTeams(
        IReadOnlyList<(Guid TeamId, string TeamName)> teams,
        IReadOnlyList<Match> poolFinalMatches,
        IReadOnlyList<MatchSet> sets,
        TiebreakerPreset preset = TiebreakerPreset.WinsH2HSetsPoints);
}

public sealed class TiebreakerChainService : ITiebreakerChain
{
    public IReadOnlyList<TiebreakerRankedTeam> OrderTeams(
        IReadOnlyList<(Guid TeamId, string TeamName)> teams,
        IReadOnlyList<Match> poolFinalMatches,
        IReadOnlyList<MatchSet> sets,
        TiebreakerPreset preset = TiebreakerPreset.WinsH2HSetsPoints)
    {
        var stats = teams.Select(t =>
        {
            var played = poolFinalMatches
                .Where(m => m.HomeTeamId == t.TeamId || m.AwayTeamId == t.TeamId)
                .ToList();
            var wins = played.Count(m => m.WinnerTeamId == t.TeamId);
            var losses = played.Count - wins;
            var (setsWon, setsLost, pointsFor, pointsAgainst) = AggregateSets(played, t.TeamId, sets);
            var setPct = setsWon + setsLost == 0 ? 0 : (double)setsWon / (setsWon + setsLost);
            var pointPct = pointsFor + pointsAgainst == 0 ? 0 : (double)pointsFor / (pointsFor + pointsAgainst);
            return new TeamStats(t.TeamId, t.TeamName, played.Count, wins, losses, setsWon, setsLost, setPct, pointPct, pointsFor, pointsAgainst);
        }).ToList();

        var ordered = stats
            .OrderByDescending(s => s.MatchWins)
            .ThenByDescending(s => s.SetPercentage)
            .ThenByDescending(s => s.PointPercentage)
            .ThenBy(s => s.TeamName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ordered.Select((s, i) =>
        {
            var step = i == 0 || s.MatchWins != ordered[i - 1].MatchWins
                ? "match_wins"
                : s.SetPercentage != ordered[i - 1].SetPercentage
                    ? "set_percentage"
                    : s.PointPercentage != ordered[i - 1].PointPercentage
                        ? "point_percentage"
                        : "name";
            return new TiebreakerRankedTeam(s.TeamId, s.TeamName, i + 1, step);
        }).ToList();
    }

    private static (int setsWon, int setsLost, int pointsFor, int pointsAgainst) AggregateSets(
        IReadOnlyList<Match> matches, Guid teamId, IReadOnlyList<MatchSet> sets)
    {
        var setsWon = 0;
        var setsLost = 0;
        var pointsFor = 0;
        var pointsAgainst = 0;
        foreach (var m in matches)
        {
            var isHome = m.HomeTeamId == teamId;
            foreach (var s in sets.Where(x => x.MatchId == m.Id))
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

    private sealed record TeamStats(
        Guid TeamId,
        string TeamName,
        int MatchesPlayed,
        int MatchWins,
        int MatchLosses,
        int SetsWon,
        int SetsLost,
        double SetPercentage,
        double PointPercentage,
        int PointsFor,
        int PointsAgainst);
}
