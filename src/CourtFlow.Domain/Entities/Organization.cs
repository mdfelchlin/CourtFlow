using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class Organization : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    private readonly List<OrganizationMembership> _memberships = new();
    public IReadOnlyCollection<OrganizationMembership> Memberships => _memberships.AsReadOnly();

    private readonly List<Tournament> _tournaments = new();
    public IReadOnlyCollection<Tournament> Tournaments => _tournaments.AsReadOnly();

    private Organization() { }

    public Organization(string name, string slug)
    {
        Rename(name);
        SetSlug(slug);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Organization name required", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void SetSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Slug required", nameof(slug));
        Slug = slug.Trim().ToLowerInvariant();
        Touch();
    }
}
