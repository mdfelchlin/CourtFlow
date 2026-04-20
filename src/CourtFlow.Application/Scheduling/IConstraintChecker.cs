namespace CourtFlow.Application.Scheduling;

public interface IConstraintChecker
{
    Task<ConflictReport> CheckTournamentAsync(Guid tournamentId, CancellationToken ct = default);
    Task<ConflictReport> CheckMatchAsync(Guid matchId, CancellationToken ct = default);
}
