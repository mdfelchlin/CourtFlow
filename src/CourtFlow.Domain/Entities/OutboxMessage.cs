namespace CourtFlow.Domain.Entities;

/// <summary>Transactional outbox envelope (same-db transaction as domain writes).</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>Assembly-qualified CLR type name of the persisted domain event snapshot.</summary>
    public string TypeName { get; set; } = "";

    public string PayloadJson { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
