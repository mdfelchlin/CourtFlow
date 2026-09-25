using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class MatchSlotSource : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid MatchId { get; private set; }
    public MatchSlotKind Slot { get; private set; }
    public MatchSlotSourceKind SourceKind { get; private set; }
    public Guid? FixedTournamentTeamId { get; private set; }
    public Guid? SourceMatchId { get; private set; }
    public Guid? SourcePoolId { get; private set; }
    public int? SourcePosition { get; private set; }
    public Guid? ResolvedTournamentTeamId { get; private set; }

    private MatchSlotSource() { }

    public MatchSlotSource(
        Guid organizationId,
        Guid matchId,
        MatchSlotKind slot,
        MatchSlotSourceKind sourceKind)
    {
        OrganizationId = organizationId;
        MatchId = matchId;
        Slot = slot;
        SourceKind = sourceKind;
    }

    public void ConfigureFixedTeam(Guid tournamentTeamId)
    {
        SourceKind = MatchSlotSourceKind.FixedTeam;
        FixedTournamentTeamId = tournamentTeamId;
        SourceMatchId = null;
        SourcePoolId = null;
        SourcePosition = null;
        ResolvedTournamentTeamId = tournamentTeamId;
        Touch();
    }

    public void ConfigureMatchOutcome(Guid sourceMatchId, MatchSlotSourceKind kind)
    {
        if (kind is not (MatchSlotSourceKind.MatchWinner or MatchSlotSourceKind.MatchLoser))
            throw new ArgumentException("Kind must be MatchWinner or MatchLoser.", nameof(kind));

        SourceKind = kind;
        SourceMatchId = sourceMatchId;
        FixedTournamentTeamId = null;
        SourcePoolId = null;
        SourcePosition = null;
        ResolvedTournamentTeamId = null;
        Touch();
    }

    public void ConfigurePoolFinish(Guid poolId, int finishPosition)
    {
        SourceKind = MatchSlotSourceKind.PoolFinish;
        SourcePoolId = poolId;
        SourcePosition = finishPosition;
        FixedTournamentTeamId = null;
        SourceMatchId = null;
        ResolvedTournamentTeamId = null;
        Touch();
    }

    public void ConfigureRanking(int rank)
    {
        SourceKind = MatchSlotSourceKind.Ranking;
        SourcePosition = rank;
        FixedTournamentTeamId = null;
        SourceMatchId = null;
        SourcePoolId = null;
        ResolvedTournamentTeamId = null;
        Touch();
    }

    public void Resolve(Guid? tournamentTeamId)
    {
        ResolvedTournamentTeamId = tournamentTeamId;
        Touch();
    }

    public void Clear()
    {
        SourceKind = MatchSlotSourceKind.FixedTeam;
        FixedTournamentTeamId = null;
        SourceMatchId = null;
        SourcePoolId = null;
        SourcePosition = null;
        ResolvedTournamentTeamId = null;
        Touch();
    }
}
