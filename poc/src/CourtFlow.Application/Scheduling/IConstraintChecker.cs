namespace CourtFlow.Application.Scheduling;

public interface IConstraintChecker
{
    Task<ConflictReport> CheckTournamentAsync(Guid tournamentId, CancellationToken ct = default);

    Task<ConflictReport> CheckHypotheticalMoveAsync(
        Guid tournamentId,
        Guid matchId,
        Guid? proposedCourtId,
        DateTime proposedStartUtc,
        int proposedDurationMinutes,
        CancellationToken ct = default);

    Task<ConflictReport> CheckMatchAsync(Guid matchId, CancellationToken ct = default);
}
