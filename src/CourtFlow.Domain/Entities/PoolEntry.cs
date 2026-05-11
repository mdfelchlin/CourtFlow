using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class PoolEntry : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid PoolId { get; private set; }
    public Guid TournamentTeamId { get; private set; }
    public int SeedInPool { get; private set; }
    public int? FinishPosition { get; private set; }

    private PoolEntry() { }

    public PoolEntry(Guid organizationId, Guid poolId, Guid tournamentTeamId, int seedInPool)
    {
        OrganizationId = organizationId;
        PoolId = poolId;
        TournamentTeamId = tournamentTeamId;
        SeedInPool = seedInPool;
    }

    public void SetFinishPosition(int position)
    {
        FinishPosition = position;
        Touch();
    }
}
