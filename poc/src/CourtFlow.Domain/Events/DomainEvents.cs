using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Events;

public sealed record TournamentCreated(Guid TournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record TournamentPublished(Guid TournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record TournamentUnpublished(Guid TournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record TournamentArchived(Guid TournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record DivisionCreated(Guid DivisionId, Guid TournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record SchedulePublished(Guid ScheduleVersionId, Guid TournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record MatchStarted(Guid MatchId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record MatchResultReported(Guid MatchId, Guid? WinnerTeamId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record TournamentTeamWithdrawn(Guid TournamentTeamId, Guid TournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record WorkTeamReassigned(Guid MatchId, Guid? OldWorkTeamId, Guid? NewWorkTeamId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record PoolsGenerated(Guid StageId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record BracketConfigured(Guid StageId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record TournamentTemplateCaptured(Guid TemplateId, Guid SourceTournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record ReflowPreviewApplied(Guid RepairJobId, Guid TournamentId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
