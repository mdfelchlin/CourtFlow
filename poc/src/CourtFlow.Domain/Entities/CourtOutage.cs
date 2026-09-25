using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class CourtOutage : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid CourtId { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string? Reason { get; private set; }

    private CourtOutage() { }

    public CourtOutage(Guid organizationId, Guid courtId, DateTime startsAt, DateTime endsAt, string? reason = null)
    {
        OrganizationId = organizationId;
        CourtId = courtId;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Reason = reason;
    }

    public void UpdateWindow(DateTime startsAt, DateTime endsAt)
    {
        if (endsAt <= startsAt) throw new ArgumentException("End must be after start.");
        StartsAt = startsAt;
        EndsAt = endsAt;
        Touch();
    }
}
