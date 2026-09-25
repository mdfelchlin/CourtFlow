namespace CourtFlow.Application.Scheduling;

public enum ConflictKind
{
    TeamDoubleBooking = 1,
    RefConflict = 2,
    CourtDoubleBooking = 3,
    WaveViolation = 4,
    PartnerTournamentConflict = 5
}

public sealed record ScheduleConflict(
    ConflictKind Kind,
    Guid MatchId,
    Guid? OtherMatchId,
    Guid? TeamId,
    Guid? CourtId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Message);

public sealed record ConflictReport(IReadOnlyCollection<ScheduleConflict> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;
}
