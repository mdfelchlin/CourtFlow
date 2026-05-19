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

public sealed record DivisionStandingsResult(
    string TiebreakerPreset,
    IReadOnlyList<PoolStandings> Pools);

public interface IStandingsCalculator
{
    Task<DivisionStandingsResult> ForDivisionAsync(Guid divisionId, CancellationToken ct = default);
}
