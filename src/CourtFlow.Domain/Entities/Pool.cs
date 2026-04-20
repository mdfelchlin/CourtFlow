using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class Pool : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid DivisionId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private Pool() { }

    public Pool(Guid organizationId, Guid divisionId, string name)
    {
        OrganizationId = organizationId;
        DivisionId = divisionId;
        Rename(name);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pool name required", nameof(name));
        Name = name.Trim();
        Touch();
    }
}
