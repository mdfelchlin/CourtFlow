using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class TournamentTeam : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid DivisionId { get; private set; }
    public Guid TeamId { get; private set; }
    public TournamentTeamEntryStatus EntryStatus { get; private set; } = TournamentTeamEntryStatus.Active;
    public string? CheckedInStatus { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? WithdrawnAt { get; private set; }
    public string? WithdrawalReason { get; private set; }
    public Guid? EffectiveAfterMatchId { get; private set; }

    private TournamentTeam() { }

    public TournamentTeam(Guid organizationId, Guid tournamentId, Guid divisionId, Guid teamId)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        DivisionId = divisionId;
        TeamId = teamId;
    }

    public void Withdraw(string? reason, Guid? effectiveAfterMatchId = null)
    {
        if (EntryStatus is TournamentTeamEntryStatus.WithdrawnPreSchedule or TournamentTeamEntryStatus.WithdrawnPostSchedule)
            throw new InvalidOperationException("Team already withdrawn.");

        EntryStatus = effectiveAfterMatchId.HasValue
            ? TournamentTeamEntryStatus.WithdrawnPostSchedule
            : TournamentTeamEntryStatus.WithdrawnPreSchedule;
        WithdrawnAt = DateTime.UtcNow;
        WithdrawalReason = reason;
        EffectiveAfterMatchId = effectiveAfterMatchId;
        Touch();
    }

    public void Reinstate()
    {
        if (EntryStatus is not (TournamentTeamEntryStatus.WithdrawnPreSchedule or TournamentTeamEntryStatus.WithdrawnPostSchedule))
            throw new InvalidOperationException("Team is not withdrawn.");

        EntryStatus = TournamentTeamEntryStatus.Active;
        WithdrawnAt = null;
        WithdrawalReason = null;
        EffectiveAfterMatchId = null;
        Touch();
    }
}
