using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class PoolSettings : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid StageId { get; private set; }
    public int TeamsPerPoolTarget { get; private set; } = 4;
    public string PoolSizeStrategy { get; private set; } = "auto_3_4";
    public string SeedingMethod { get; private set; } = "snake";
    public string ClubSeparationMode { get; private set; } = "none";
    public string PlayStyle { get; private set; } = "round_robin";
    public string CourtAllocationMode { get; private set; } = "preferred";
    public string RefAssignmentMode { get; private set; } = "next_in_pool";

    private PoolSettings() { }

    public PoolSettings(Guid organizationId, Guid stageId, int teamsPerPoolTarget = 4)
    {
        OrganizationId = organizationId;
        StageId = stageId;
        TeamsPerPoolTarget = teamsPerPoolTarget;
    }

    public void Update(
        int teamsPerPoolTarget,
        string poolSizeStrategy,
        string seedingMethod,
        string clubSeparationMode,
        string playStyle,
        string courtAllocationMode,
        string refAssignmentMode)
    {
        TeamsPerPoolTarget = teamsPerPoolTarget;
        PoolSizeStrategy = poolSizeStrategy;
        SeedingMethod = seedingMethod;
        ClubSeparationMode = clubSeparationMode;
        PlayStyle = playStyle;
        CourtAllocationMode = courtAllocationMode;
        RefAssignmentMode = refAssignmentMode;
        Touch();
    }
}
