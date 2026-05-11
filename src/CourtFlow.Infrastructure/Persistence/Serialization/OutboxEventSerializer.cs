using CourtFlow.Domain.Common;
using System.Text.Json;

namespace CourtFlow.Infrastructure.Persistence.Serialization;

/// <remarks>Prefer assembly-qualified names for round-trip from the outbox poller.</remarks>
public sealed class OutboxEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public (string TypeName, string Json) Serialize(IDomainEvent domainEvent)
    {
        var type = domainEvent.GetType();
        var json = JsonSerializer.Serialize(domainEvent, type, Options);
        return (type.AssemblyQualifiedName ?? type.FullName ?? type.Name, json);
    }
}
