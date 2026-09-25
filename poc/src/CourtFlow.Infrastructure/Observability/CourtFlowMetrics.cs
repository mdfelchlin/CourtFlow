using System.Diagnostics.Metrics;

namespace CourtFlow.Infrastructure.Observability;

/// <summary>Product metrics for schedule generation, conflicts, and court delay (INF-05).</summary>
public sealed class CourtFlowMetrics
{
    public const string MeterName = "CourtFlow";

    private readonly Counter<long> _scheduleGenerations;
    private readonly Counter<long> _scheduleConflicts;
    private readonly Histogram<double> _delayMinutes;

    public CourtFlowMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _scheduleGenerations = meter.CreateCounter<long>("courtflow.schedule.generations");
        _scheduleConflicts = meter.CreateCounter<long>("courtflow.schedule.conflicts");
        _delayMinutes = meter.CreateHistogram<double>("courtflow.match.delay_minutes");
    }

    public void RecordScheduleGeneration(Guid tournamentId, int matchCount) =>
        _scheduleGenerations.Add(1, new KeyValuePair<string, object?>("tournament_id", tournamentId.ToString()));

    public void RecordConflictCheck(Guid tournamentId, int conflictCount) =>
        _scheduleConflicts.Add(conflictCount, new KeyValuePair<string, object?>("tournament_id", tournamentId.ToString()));

    public void RecordDelayMinutes(double minutes) => _delayMinutes.Record(minutes);
}
