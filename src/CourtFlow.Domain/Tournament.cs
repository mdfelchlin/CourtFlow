namespace CourtFlow.Domain;

public sealed class Tournament
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string TimeZoneId { get; private set; } = "";
    public int DefaultMatchDurationMinutes { get; private set; }

    private readonly List<Venue> _venues = [];
    private readonly List<Division> _divisions = [];

    public IReadOnlyCollection<Venue> Venues => _venues;
    public IReadOnlyCollection<Division> Divisions => _divisions;

    private Tournament()
    {
    }

    public static Tournament Create(
        Guid organizationId,
        string? name,
        string? description,
        DateOnly? startDate,
        DateOnly? endDate,
        string? timeZoneId,
        int? defaultMatchDurationMinutes)
    {
        Validate(name, description, startDate, endDate, timeZoneId, defaultMatchDurationMinutes);

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
        };
        tournament.Rename(name);
        tournament.SetDescription(description);
        tournament.SetDates(startDate!.Value, endDate!.Value);
        tournament.SetTimeZone(timeZoneId);
        tournament.SetDuration(defaultMatchDurationMinutes!.Value);
        return tournament;
    }

    public static void Validate(
        string? name,
        string? description,
        DateOnly? startDate,
        DateOnly? endDate,
        string? timeZoneId,
        int? defaultMatchDurationMinutes)
    {
        RequireName(name);
        NormalizeOptional(description);
        RequireDates(startDate, endDate);
        RequireTimeZone(timeZoneId);
        RequireDuration(defaultMatchDurationMinutes);
    }

    public void Rename(string? name) => Name = RequireName(name);

    public void SetDescription(string? description) => Description = NormalizeOptional(description);

    public void SetDates(DateOnly startDate, DateOnly endDate)
    {
        RequireDates(startDate, endDate);
        StartDate = startDate;
        EndDate = endDate;
    }

    public void SetTimeZone(string? timeZoneId) => TimeZoneId = RequireTimeZone(timeZoneId);

    public void SetDuration(int minutes) => DefaultMatchDurationMinutes = RequireDuration(minutes);

    public Venue AddVenue(string? name, string? address, int courtCount)
    {
        var venue = new Venue(OrganizationId, Id, name, address, courtCount);
        _venues.Add(venue);
        return venue;
    }

    public Venue UpdateVenue(Guid venueId, string? name, string? address, int courtCount)
    {
        var venue = _venues.FirstOrDefault(item => item.Id == venueId)
            ?? throw new DomainRuleException("Venue was not found.");
        venue.Rename(name);
        venue.SetAddress(address);
        venue.SetCourtCount(courtCount);
        return venue;
    }

    public void RemoveVenue(Guid venueId)
    {
        var venue = _venues.FirstOrDefault(item => item.Id == venueId)
            ?? throw new DomainRuleException("Venue was not found.");
        _venues.Remove(venue);
    }

    public Division AddDivision(string? name, string? gender)
    {
        Division.EnsureUnique(name, gender, ListedDivisions(), exceptId: null);
        var division = new Division(OrganizationId, Id, name, gender);
        _divisions.Add(division);
        return division;
    }

    public Division UpdateDivision(Guid divisionId, string? name, string? gender)
    {
        var division = _divisions.FirstOrDefault(item => item.Id == divisionId)
            ?? throw new DomainRuleException("Division was not found.");
        Division.EnsureUnique(name, gender, ListedDivisions(), division.Id);
        division.Rename(name);
        division.SetGender(gender);
        return division;
    }

    public void RemoveDivision(Guid divisionId)
    {
        var division = _divisions.FirstOrDefault(item => item.Id == divisionId)
            ?? throw new DomainRuleException("Division was not found.");
        _divisions.Remove(division);
    }

    private IEnumerable<(Guid Id, string Name, string Gender)> ListedDivisions() =>
        _divisions.Select(division => (division.Id, division.Name, division.Gender.ToString()));

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("Tournament name is required.");

        return name.Trim();
    }

    private static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim();

    private static void RequireDates(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate is null)
            throw new DomainRuleException("Start date is required.");
        if (endDate is null)
            throw new DomainRuleException("End date is required.");
        if (endDate.Value <= startDate.Value)
            throw new DomainRuleException("End date must be after the start date.");
    }

    private static string RequireTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new DomainRuleException("Time zone is required.");
        if (!UsTimeZones.IsAllowed(timeZoneId))
            throw new DomainRuleException("Time zone must be one of the listed US time zones.");

        return timeZoneId;
    }

    private static int RequireDuration(int? minutes)
    {
        if (minutes is null)
            throw new DomainRuleException("Default match duration is required.");
        if (minutes is < 15 or > 240)
            throw new DomainRuleException("Default match duration must be a whole number of minutes from 15 through 240.");

        return minutes.Value;
    }
}
