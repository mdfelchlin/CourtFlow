namespace CourtFlow.Application.Reflow;

public sealed record ReflowMovePayload(
    Guid MatchAssignmentId,
    Guid MatchId,
    DateTime NewScheduledStart,
    DateTime NewScheduledEnd);

public sealed record ReflowApplyPayload(
    Guid TournamentId,
    Guid ScheduleVersionId,
    long MutationSequence,
    IReadOnlyList<ReflowMovePayload> Moves);

public interface IReflowApplyTokenIssuer
{
    string Sign(ReflowApplyPayload payload);

    bool TryVerify(string token, out ReflowApplyPayload? payload);
}
