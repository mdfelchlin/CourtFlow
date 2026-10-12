namespace CourtFlow.Domain;

/// <summary>One team's entry in one tournament, in one division of that tournament.</summary>
public sealed class TournamentTeam
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid DivisionId { get; private set; }

    private TournamentTeam()
    {
    }

    internal TournamentTeam(Guid organizationId, Guid tournamentId, Guid teamId, Guid divisionId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        TeamId = teamId;
        DivisionId = divisionId;
    }

    internal void MoveTo(Guid divisionId) => DivisionId = divisionId;
}
