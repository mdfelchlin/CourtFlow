using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class DivisionRanking : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid DivisionId { get; private set; }
    public Guid TournamentTeamId { get; private set; }
    public int Rank { get; private set; }
    public string Source { get; private set; } = "manual";
    public bool IsManualOverride { get; private set; }

    private DivisionRanking() { }

    public DivisionRanking(Guid organizationId, Guid divisionId, Guid tournamentTeamId, int rank, string source = "manual")
    {
        OrganizationId = organizationId;
        DivisionId = divisionId;
        TournamentTeamId = tournamentTeamId;
        Rank = rank;
        Source = source;
    }

    public void SetRank(int rank, string source, bool isManualOverride = false)
    {
        Rank = rank;
        Source = source;
        IsManualOverride = isManualOverride;
        Touch();
    }
}
