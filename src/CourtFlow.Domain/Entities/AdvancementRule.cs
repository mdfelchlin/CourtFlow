using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class AdvancementRule : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid SourceStageId { get; private set; }
    public Guid? SourcePoolId { get; private set; }
    public int SourceFinishPosition { get; private set; }
    public Guid TargetBracketId { get; private set; }
    public int TargetSeedSlot { get; private set; }
    public string OrderBy { get; private set; } = "pool_sequence";
    public int Priority { get; private set; }

    private AdvancementRule() { }

    public AdvancementRule(
        Guid organizationId, Guid sourceStageId, int sourceFinishPosition,
        Guid targetBracketId, int targetSeedSlot,
        Guid? sourcePoolId = null, string orderBy = "pool_sequence", int priority = 0)
    {
        OrganizationId = organizationId;
        SourceStageId = sourceStageId;
        SourcePoolId = sourcePoolId;
        SourceFinishPosition = sourceFinishPosition;
        TargetBracketId = targetBracketId;
        TargetSeedSlot = targetSeedSlot;
        OrderBy = orderBy;
        Priority = priority;
    }
}
