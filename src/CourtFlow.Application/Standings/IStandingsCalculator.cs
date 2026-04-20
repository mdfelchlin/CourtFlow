namespace CourtFlow.Application.Standings;

public sealed record PoolStandingRow(
    Guid TeamId,
    string TeamName,
    int Rank,
    int MatchesPlayed,
    int MatchWins,
    int MatchLosses,
    int SetsWon,
    int SetsLost,
    double SetPercentage,
    int PointsFor,
    int PointsAgainst,
    double PointPercentage);

public sealed record PoolStandings(Guid PoolId, string PoolName, IReadOnlyList<PoolStandingRow> Rows);

public interface IStandingsCalculator
{
    Task<IReadOnlyList<PoolStandings>> ForDivisionAsync(Guid divisionId, CancellationToken ct = default);
}
