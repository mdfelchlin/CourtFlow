namespace CourtFlow.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
