using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class MatchOfficials : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid? WorkTeamTournamentTeamId { get; private set; }
    public Guid? R1TournamentTeamId { get; private set; }
    public Guid? R2TournamentTeamId { get; private set; }
    public OfficialAssignmentSource AssignmentSource { get; private set; } = OfficialAssignmentSource.AutoRotation;
    public DateTime? ManualAssignedAt { get; private set; }
    public string? ManualReason { get; private set; }

    private MatchOfficials() { }

    public MatchOfficials(Guid organizationId, Guid matchId, Guid? workTeamId, OfficialAssignmentSource source = OfficialAssignmentSource.AutoRotation)
    {
        OrganizationId = organizationId;
        MatchId = matchId;
        WorkTeamTournamentTeamId = workTeamId;
        AssignmentSource = source;
    }

    public void ReassignWorkTeam(Guid? workTeamId, OfficialAssignmentSource source, string? reason = null)
    {
        WorkTeamTournamentTeamId = workTeamId;
        AssignmentSource = source;
        if (source == OfficialAssignmentSource.ManualOverride)
            ManualAssignedAt = DateTime.UtcNow;
        ManualReason = reason;
        Touch();
    }
}
