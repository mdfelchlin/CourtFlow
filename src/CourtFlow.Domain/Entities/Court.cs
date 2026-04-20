using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class Court : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid VenueId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string[] Tags { get; private set; } = Array.Empty<string>();

    private Court() { }

    public Court(Guid organizationId, Guid venueId, string name, string[]? tags = null)
    {
        OrganizationId = organizationId;
        VenueId = venueId;
        Rename(name);
        Tags = tags ?? Array.Empty<string>();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Court name required", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void SetTags(string[] tags) { Tags = tags ?? Array.Empty<string>(); Touch(); }
}
