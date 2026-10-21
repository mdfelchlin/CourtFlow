namespace CourtFlow.Domain;

public sealed class Venue
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public string Name { get; private set; } = "";
    public string Address { get; private set; } = "";

    private readonly List<Court> _courts = [];

    public IReadOnlyCollection<Court> Courts => _courts;

    private Venue()
    {
    }

    internal Venue(Guid organizationId, Guid tournamentId, string? name, string? address, int courtCount)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        Rename(name);
        SetAddress(address);
        SetCourtCount(courtCount);
    }

    public void Rename(string? name) => Name = RequireName(name);

    public void SetAddress(string? address) => Address = NormalizeOptional(address);

    public static void Validate(string? name, string? address, int? courtCount)
    {
        RequireName(name);
        NormalizeOptional(address);
        if (courtCount is null)
            throw new DomainRuleException("Court count is required.");
        if (courtCount is < 0 or > 99)
            throw new DomainRuleException("Court count must be a whole number from 0 through 99.");
    }

    public void SetCourtCount(int courtCount)
    {
        if (courtCount is < 0 or > 99)
            throw new DomainRuleException("Court count must be a whole number from 0 through 99.");

        _courts.RemoveAll(court => court.Number > courtCount);
        for (var number = 1; number <= courtCount; number++)
        {
            if (_courts.All(court => court.Number != number))
                _courts.Add(new Court(OrganizationId, Id, number));
        }
    }

    internal static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("Venue name is required.");

        return name.Trim();
    }

    internal static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}