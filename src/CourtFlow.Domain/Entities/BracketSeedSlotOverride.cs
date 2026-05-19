using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class BracketSeedSlotOverride : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid BracketSeedSlotId { get; private set; }
    public Guid? PreviousManualTournamentTeamId { get; private set; }
    public Guid? NewManualTournamentTeamId { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime AtUtc { get; private set; }

    private BracketSeedSlotOverride() { }

    public BracketSeedSlotOverride(
        Guid organizationId,
        Guid bracketSeedSlotId,
        Guid? previousManualTournamentTeamId,
        Guid? newManualTournamentTeamId,
        string? actorUserId = null,
        string? reason = null)
    {
        OrganizationId = organizationId;
        BracketSeedSlotId = bracketSeedSlotId;
        PreviousManualTournamentTeamId = previousManualTournamentTeamId;
        NewManualTournamentTeamId = newManualTournamentTeamId;
        ActorUserId = actorUserId;
        Reason = reason;
        AtUtc = DateTime.UtcNow;
    }
}
