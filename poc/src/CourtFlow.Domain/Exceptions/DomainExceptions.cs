namespace CourtFlow.Domain.Exceptions;

public class LifecycleViolationException : InvalidOperationException
{
    public LifecycleViolationException(string message) : base(message) { }
    public LifecycleViolationException(string entityType, string currentStatus, string attemptedStatus)
        : base($"Cannot transition {entityType} from '{currentStatus}' to '{attemptedStatus}'.") { }
}

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
}

public class EntityNotFoundException : Exception
{
    public string EntityType { get; }
    public string EntityId { get; }

    public EntityNotFoundException(string entityType, string entityId)
        : base($"{entityType} '{entityId}' not found.")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    public EntityNotFoundException(string entityType, Guid entityId)
        : this(entityType, entityId.ToString()) { }
}

public class ForbiddenDomainException : Exception
{
    public ForbiddenDomainException(string message) : base(message) { }
}
