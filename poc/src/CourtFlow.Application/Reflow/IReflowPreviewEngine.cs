namespace CourtFlow.Application.Reflow;

/// <summary>Preview + apply reflow (ADR-0004 / ADR-0007 seam).</summary>
public interface IReflowPreviewEngine
{
    Task<ReflowPreviewDelta> PreviewAsync(Guid tournamentId, CancellationToken ct = default);

    Task<ReflowApplyResult> ApplyAsync(Guid tournamentId, string applyToken, string? createdByUserId, CancellationToken ct = default);
}

public sealed record ReflowAssignmentMove(
    Guid MatchAssignmentId,
    Guid MatchId,
    DateTime OldScheduledStart,
    DateTime NewScheduledStart,
    int ShiftMinutes);

public sealed record ReflowPreviewDelta(
    IReadOnlyList<string> Notes,
    IReadOnlyList<ReflowAssignmentMove> Moves,
    string? ApplyToken);

public sealed record ReflowApplyResult(Guid RepairJobId, int AssignmentsUpdated);
