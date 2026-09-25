using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class Stage : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid DivisionId { get; private set; }
    public int StageNumber { get; private set; }
    public StageType StageType { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public StageStatus Status { get; private set; } = StageStatus.Draft;
    public string? SeedingSource { get; private set; }

    private Stage() { }

    public Stage(Guid organizationId, Guid divisionId, int stageNumber, StageType stageType, string name, string? seedingSource = null)
    {
        OrganizationId = organizationId;
        DivisionId = divisionId;
        StageNumber = stageNumber;
        StageType = stageType;
        Name = name;
        SeedingSource = seedingSource ?? (stageNumber == 1 ? "division_rankings" : "prior_stage_finish_positions");
    }

    public void TransitionTo(StageStatus target)
    {
        var valid = (Status, target) switch
        {
            (StageStatus.Draft, StageStatus.Configured) => true,
            (StageStatus.Configured, StageStatus.Seeded) => true,
            (StageStatus.Seeded, StageStatus.InProgress) => true,
            (StageStatus.Seeded, StageStatus.Configured) => true,
            (StageStatus.InProgress, StageStatus.Completed) => true,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException($"Cannot transition Stage from {Status} to {target}.");

        Status = target;
        Touch();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Stage name required", nameof(name));
        Name = name.Trim();
        Touch();
    }
}
