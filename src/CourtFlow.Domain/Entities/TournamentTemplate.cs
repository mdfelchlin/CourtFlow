using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class TournamentTemplate : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? SourceTournamentId { get; private set; }
    public int UsageCount { get; private set; }

    private TournamentTemplate() { }

    public TournamentTemplate(Guid organizationId, string name, Guid? sourceTournamentId = null, string? description = null)
    {
        OrganizationId = organizationId;
        Name = name;
        SourceTournamentId = sourceTournamentId;
        Description = description;
    }

    public void IncrementUsage()
    {
        UsageCount++;
        Touch();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        Name = name.Trim();
        Touch();
    }
}
