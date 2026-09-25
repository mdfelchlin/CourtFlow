using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class MatchAssignment : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid ScheduleVersionId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid CourtId { get; private set; }
    public DateTime ScheduledStart { get; private set; }
    public DateTime ScheduledEnd { get; private set; }
    public string AssignmentStatus { get; private set; } = "scheduled";

    private MatchAssignment() { }

    public MatchAssignment(Guid organizationId, Guid scheduleVersionId, Guid matchId, Guid courtId, DateTime scheduledStart, DateTime scheduledEnd)
    {
        OrganizationId = organizationId;
        ScheduleVersionId = scheduleVersionId;
        MatchId = matchId;
        CourtId = courtId;
        ScheduledStart = scheduledStart;
        ScheduledEnd = scheduledEnd;
    }

    public void Reassign(Guid courtId, DateTime scheduledStart, DateTime scheduledEnd)
    {
        CourtId = courtId;
        ScheduledStart = scheduledStart;
        ScheduledEnd = scheduledEnd;
        Touch();
    }
}
