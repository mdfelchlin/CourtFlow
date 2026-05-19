using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.ReadModels.MatchClockDelaySignal;

public interface IDelayCalculation
{
    int? ComputeDelayMinutes(
        MatchState state,
        DateTime scheduledStartUtc,
        DateTime? actualStartUtc,
        DateTime? lastScoreAtUtc,
        DateTime? nowUtc = null);
}

public sealed class DelayCalculationService : IDelayCalculation
{
    public int? ComputeDelayMinutes(
        MatchState state,
        DateTime scheduledStartUtc,
        DateTime? actualStartUtc,
        DateTime? lastScoreAtUtc,
        DateTime? nowUtc = null) =>
        MatchClockDelaySignal.ComputeDelayMinutes(
            state,
            scheduledStartUtc,
            actualStartUtc,
            lastScoreAtUtc,
            nowUtc ?? DateTime.UtcNow);
}
