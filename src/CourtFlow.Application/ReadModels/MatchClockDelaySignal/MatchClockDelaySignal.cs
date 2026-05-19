using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.ReadModels.MatchClockDelaySignal;

/// <summary>Per-match delay minutes from clock vs scheduled start (ADR-0006).</summary>
public static class MatchClockDelaySignal
{
    /// <returns>NULL when no delay signal yet (future on-schedule match).</returns>
    public static int? ComputeDelayMinutes(
        MatchState state,
        DateTime scheduledStartUtc,
        DateTime? actualStartUtc,
        DateTime? lastScoreAtUtc,
        DateTime nowUtc)
    {
        if (state == MatchState.NotStarted)
            return null;

        var reference = state == MatchState.InProgress
            ? (lastScoreAtUtc ?? actualStartUtc ?? nowUtc)
            : (actualStartUtc ?? scheduledStartUtc);

        return (int)Math.Round((reference - scheduledStartUtc).TotalMinutes);
    }
}
