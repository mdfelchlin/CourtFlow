using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class RepairAction : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid RepairJobId { get; private set; }
    public RepairActionType ActionType { get; private set; }
    public string? TargetId { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }

    private RepairAction() { }

    internal RepairAction(Guid organizationId, Guid repairJobId, RepairActionType actionType, string? targetId, string? beforeJson, string? afterJson)
    {
        OrganizationId = organizationId;
        RepairJobId = repairJobId;
        ActionType = actionType;
        TargetId = targetId;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
    }
}
