using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class MatchSet : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid MatchId { get; private set; }
    public int SetNumber { get; private set; }
    public int HomeScore { get; private set; }
    public int AwayScore { get; private set; }

    private MatchSet() { }

    internal MatchSet(Guid organizationId, Guid matchId, int setNumber, int homeScore, int awayScore)
    {
        OrganizationId = organizationId;
        MatchId = matchId;
        SetNumber = setNumber;
        HomeScore = homeScore;
        AwayScore = awayScore;
    }

    internal void UpdateScores(int homeScore, int awayScore)
    {
        HomeScore = homeScore;
        AwayScore = awayScore;
        Touch();
    }
}
