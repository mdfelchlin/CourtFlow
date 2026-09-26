namespace CourtFlow.Domain;

public sealed class Court
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid VenueId { get; private set; }
    public int Number { get; private set; }

    public string Name => Number.ToString();

    private Court()
    {
    }

    internal Court(Guid organizationId, Guid venueId, int number)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        VenueId = venueId;
        Number = number;
    }
}
