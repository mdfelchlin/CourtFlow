using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;
using CourtFlow.Domain.Events;

namespace CourtFlow.Domain.Entities;

/// <summary>Published schedule lineage for staleness-checked moves (ADR-0004-lite).</summary>
public sealed class ScheduleVersion : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public ScheduleVersionStatus Status { get; private set; }

    /// <summary>Increments whenever a guarded schedule mutation commits (preview apply).</summary>
    public long MutationSequence { get; private set; }

    public string? CreatedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? SupersededAt { get; private set; }
    public Guid? SupersededByScheduleVersionId { get; private set; }

    private ScheduleVersion() { }

    public ScheduleVersion(Guid organizationId, Guid tournamentId, string? createdByUserId = null)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        Status = ScheduleVersionStatus.Draft;
        CreatedByUserId = createdByUserId;
    }

    public void TransitionTo(ScheduleVersionStatus target)
    {
        var valid = (Status, target) switch
        {
            (ScheduleVersionStatus.Draft, ScheduleVersionStatus.Preview) => true,
            (ScheduleVersionStatus.Draft, ScheduleVersionStatus.Published) => true,
            (ScheduleVersionStatus.Preview, ScheduleVersionStatus.Published) => true,
            (ScheduleVersionStatus.Published, ScheduleVersionStatus.Superseded) => true,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException($"Cannot transition ScheduleVersion from {Status} to {target}.");

        Status = target;
        if (target == ScheduleVersionStatus.Published)
        {
            PublishedAt = DateTime.UtcNow;
            RaiseEvent(new SchedulePublished(Id, TournamentId));
        }
        Touch();
    }

    public void Supersede(Guid newVersionId)
    {
        TransitionTo(ScheduleVersionStatus.Superseded);
        SupersededAt = DateTime.UtcNow;
        SupersededByScheduleVersionId = newVersionId;
    }

    public void BumpAfterCommittedApply()
    {
        MutationSequence++;
        Touch();
    }
}
