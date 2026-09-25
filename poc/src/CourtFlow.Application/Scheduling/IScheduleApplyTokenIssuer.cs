namespace CourtFlow.Application.Scheduling;

public sealed record ScheduleApplyPayload(
    Guid ScheduleVersionId,
    long MutationSequence,
    Guid MatchId,
    Guid? CourtId,
    DateTime ScheduledStartUtc,
    int DurationMinutes);

public interface IScheduleApplyTokenIssuer
{
    /// <summary>Creates a tamper-evident preview token locking mutation sequence snapshot.</summary>
    string Sign(ScheduleApplyPayload payload);

    bool TryVerify(string token, out ScheduleApplyPayload? payload);
}
