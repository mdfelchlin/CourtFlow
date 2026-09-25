using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class SchedulingTimeSlot : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid CourtId { get; private set; }
    public Wave Wave { get; private set; }
    public DateTime StartUtc { get; private set; }
    public int DurationMinutes { get; private set; }

    private SchedulingTimeSlot() { }

    public SchedulingTimeSlot(Guid organizationId, Guid tournamentId, Guid courtId, Wave wave, DateTime startUtc, int durationMinutes)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        CourtId = courtId;
        Wave = wave;
        StartUtc = startUtc;
        DurationMinutes = durationMinutes;
    }

    public DateTime EndUtc => StartUtc.AddMinutes(DurationMinutes);
}
