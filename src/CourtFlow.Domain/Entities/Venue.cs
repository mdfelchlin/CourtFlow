using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class Venue : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }

    private readonly List<Court> _courts = new();
    public IReadOnlyCollection<Court> Courts => _courts.AsReadOnly();

    private Venue() { }

    public Venue(Guid organizationId, Guid tournamentId, string name, string? address = null)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        Rename(name);
        Address = address;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Venue name required", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void SetAddress(string? address) { Address = address; Touch(); }
}
