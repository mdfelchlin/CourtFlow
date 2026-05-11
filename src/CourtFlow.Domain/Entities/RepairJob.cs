using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class RepairJob : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid? DivisionId { get; private set; }
    public RepairTriggerType TriggerType { get; private set; }
    public RepairJobStatus Status { get; private set; } = RepairJobStatus.Pending;
    public string? PreviewDiagnosticsJson { get; private set; }
    public string? CreatedByUserId { get; private set; }

    private readonly List<RepairAction> _actions = new();
    public IReadOnlyCollection<RepairAction> Actions => _actions.AsReadOnly();

    private RepairJob() { }

    public RepairJob(Guid organizationId, Guid tournamentId, RepairTriggerType triggerType, string? createdByUserId = null, Guid? divisionId = null)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        TriggerType = triggerType;
        CreatedByUserId = createdByUserId;
        DivisionId = divisionId;
    }

    public RepairAction AddAction(RepairActionType actionType, string? targetId, string? beforeJson, string? afterJson)
    {
        var action = new RepairAction(OrganizationId, Id, actionType, targetId, beforeJson, afterJson);
        _actions.Add(action);
        Touch();
        return action;
    }

    public void MarkApplied(string? diagnosticsJson = null)
    {
        Status = RepairJobStatus.Applied;
        PreviewDiagnosticsJson = diagnosticsJson;
        Touch();
    }

    public void Reject()
    {
        Status = RepairJobStatus.Rejected;
        Touch();
    }
}
