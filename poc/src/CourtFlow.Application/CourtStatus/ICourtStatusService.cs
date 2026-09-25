using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.CourtStatus;

public sealed record CourtStatusView(
    Guid CourtId,
    string CourtName,
    string VenueName,
    Guid? CurrentMatchId,
    string? CurrentMatchLabel,
    Guid? DivisionId,
    string? DivisionName,
    Wave? Wave,
    DateTime? ScheduledStartUtc,
    DateTime? ActualStartUtc,
    DateTime? LastScoreAtUtc,
    int MinutesBehind,
    CourtStatusKind Status);

public interface ICourtStatusService
{
    Task<IReadOnlyList<CourtStatusView>> GetForTournamentAsync(Guid tournamentId, CancellationToken ct = default);
}
