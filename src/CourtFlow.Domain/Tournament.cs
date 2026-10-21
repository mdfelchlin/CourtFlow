namespace CourtFlow.Domain;

public sealed class Tournament
{
    public const string TeamAlreadyEntered = "This team is already entered in this tournament.";
    public const string EntryNotFound = "Team entry was not found.";
    public const string TeamsChanged = "The teams in this division changed. Review the ranking and save again.";
    public const string ArchivedMessage = "This tournament is archived. Restore it to make changes.";
    public const string OnlyUnpublishedCanBePublished = "Only an unpublished tournament can be published.";
    public const string OnlyPublishedCanBeUnpublished = "Only a published tournament can be unpublished.";
    public const string OnlyPublishedCanBeArchived = "Only a published tournament can be archived.";
    public const string OnlyArchivedCanBeRestored = "Only an archived tournament can be restored.";
    public const string PublishedCannotBeDeleted = "A published tournament cannot be deleted. Unpublish or archive it first.";

    /// <summary>A published tournament archives itself this long after its latest publish. Plain days, so time zones and daylight saving do not shift it.</summary>
    public static readonly TimeSpan AutoArchiveAfter = TimeSpan.FromDays(270);

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string TimeZoneId { get; private set; } = "";
    public int DefaultMatchDurationMinutes { get; private set; }
    public TournamentStatus Status { get; private set; } = TournamentStatus.Unpublished;
    public DateTimeOffset? LastPublishedAt { get; private set; }

    /// <summary>When the automatic archive becomes due, or null while nothing is published.</summary>
    public DateTimeOffset? ArchiveDueAt =>
        Status == TournamentStatus.Published && LastPublishedAt is { } published
            ? published + AutoArchiveAfter
            : null;

    private readonly List<Venue> _venues = [];
    private readonly List<Division> _divisions = [];
    private readonly List<TournamentTeam> _entries = [];
    private readonly List<RankingPlace> _rankings = [];

    public IReadOnlyCollection<Venue> Venues => _venues;
    public IReadOnlyCollection<Division> Divisions => _divisions;
    public IReadOnlyCollection<TournamentTeam> Entries => _entries;
    public IReadOnlyCollection<RankingPlace> Rankings => _rankings;

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

    public void Rename(string? name)
    {
        EnsureEditable();
        Name = RequireName(name);
    }

    public void SetDescription(string? description)
    {
        EnsureEditable();
        Description = NormalizeOptional(description);
    }

    public void SetDates(DateOnly startDate, DateOnly endDate)
    {
        EnsureEditable();
        RequireDates(startDate, endDate);
        StartDate = startDate;
        EndDate = endDate;
    }

    public void SetTimeZone(string? timeZoneId)
    {
        EnsureEditable();
        TimeZoneId = RequireTimeZone(timeZoneId);
    }

    public void SetDuration(int minutes)
    {
        EnsureEditable();
        DefaultMatchDurationMinutes = RequireDuration(minutes);
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != TournamentStatus.Unpublished)
            throw new DomainRuleException(OnlyUnpublishedCanBePublished);

        Status = TournamentStatus.Published;
        LastPublishedAt = now;
    }

    public void Unpublish()
    {
        if (Status != TournamentStatus.Published)
            throw new DomainRuleException(OnlyPublishedCanBeUnpublished);

        Status = TournamentStatus.Unpublished;
    }

    public void Archive()
    {
        if (Status != TournamentStatus.Published)
            throw new DomainRuleException(OnlyPublishedCanBeArchived);

        Status = TournamentStatus.Archived;
    }

    /// <summary>Restoring returns the tournament to unpublished, so the director can check it before it goes public again.</summary>
    public void Restore()
    {
        if (Status != TournamentStatus.Archived)
            throw new DomainRuleException(OnlyArchivedCanBeRestored);

        Status = TournamentStatus.Unpublished;
    }

    /// <summary>The automatic archive. Reads the current state, so a republish since the query ran is honored.</summary>
    public bool ArchiveIfDue(DateTimeOffset now)
    {
        if (ArchiveDueAt is not { } due || now < due)
            return false;

        Status = TournamentStatus.Archived;
        return true;
    }

    public void EnsureCanDelete()
    {
        if (Status == TournamentStatus.Published)
            throw new DomainRuleException(PublishedCannotBeDeleted);
    }

    public void EnsureEditable()
    {
        if (Status == TournamentStatus.Archived)
            throw new DomainRuleException(ArchivedMessage);
    }

    public Venue AddVenue(string? name, string? address, int courtCount)
    {
        EnsureEditable();
        var venue = new Venue(OrganizationId, Id, name, address, courtCount);
        _venues.Add(venue);
        return venue;
    }

    public Venue UpdateVenue(Guid venueId, string? name, string? address, int courtCount)
    {
        EnsureEditable();
        var venue = _venues.FirstOrDefault(item => item.Id == venueId)
            ?? throw new DomainRuleException("Venue was not found.");
        venue.Rename(name);
        venue.SetAddress(address);
        venue.SetCourtCount(courtCount);
        return venue;
    }

    public void RemoveVenue(Guid venueId)
    {
        EnsureEditable();
        var venue = _venues.FirstOrDefault(item => item.Id == venueId)
            ?? throw new DomainRuleException("Venue was not found.");
        _venues.Remove(venue);
    }

    public Division AddDivision(string? name, string? gender)
    {
        EnsureEditable();
        Division.EnsureUnique(name, gender, ListedDivisions(), exceptId: null);
        var division = new Division(OrganizationId, Id, name, gender);
        _divisions.Add(division);
        return division;
    }

    public Division UpdateDivision(Guid divisionId, string? name, string? gender)
    {
        EnsureEditable();
        var division = _divisions.FirstOrDefault(item => item.Id == divisionId)
            ?? throw new DomainRuleException("Division was not found.");
        Division.EnsureUnique(name, gender, ListedDivisions(), division.Id);
        division.Rename(name);
        division.SetGender(gender);
        return division;
    }

    public void RemoveDivision(Guid divisionId)
    {
        EnsureEditable();
        var division = _divisions.FirstOrDefault(item => item.Id == divisionId)
            ?? throw new DomainRuleException("Division was not found.");
        var teamCount = _entries.Count(entry => entry.DivisionId == divisionId);
        if (teamCount > 0)
            throw new DomainRuleException(Division.HasTeams(teamCount));

        ClearRanking(divisionId);
        _divisions.Remove(division);
    }

    public TournamentTeam? EntryFor(Guid teamId) => _entries.FirstOrDefault(entry => entry.TeamId == teamId);

    public TournamentTeam EnterTeam(Guid teamId, Guid? divisionId)
    {
        EnsureEditable();
        var division = RequireOwnDivision(divisionId);
        if (EntryFor(teamId) is not null)
            throw new DomainRuleException(TeamAlreadyEntered);

        var entry = new TournamentTeam(OrganizationId, Id, teamId, division);
        _entries.Add(entry);
        AppendIfRanked(division, entry.Id);
        return entry;
    }

    public TournamentTeam MoveEntry(Guid entryId, Guid? divisionId)
    {
        EnsureEditable();
        var entry = FindEntry(entryId);
        var target = RequireOwnDivision(divisionId);
        if (entry.DivisionId != target)
        {
            RelocateRanking(entry, target);
            entry.MoveTo(target);
        }

        return entry;
    }

    public void RemoveEntry(Guid entryId)
    {
        EnsureEditable();
        var entry = FindEntry(entryId);
        RemoveFromRanking(entry);
        _entries.Remove(entry);
    }

    /// <summary>A division is ranked only after a director saves an order. Adding teams before that stores nothing.</summary>
    public bool IsRanked(Guid divisionId) => _rankings.Any(place => place.DivisionId == divisionId);

    public int NewTeamCount(Guid divisionId) =>
        _rankings.Count(place => place.DivisionId == divisionId && place.IsNew);

    public IReadOnlyList<RankingPlace> RankingOf(Guid divisionId) => Places(divisionId);

    public void SaveRanking(Guid divisionId, IReadOnlyList<Guid> entryIds)
    {
        EnsureEditable();
        if (_divisions.All(division => division.Id != divisionId))
            throw new DomainRuleException("Division was not found.");

        var current = _entries.Where(entry => entry.DivisionId == divisionId).Select(entry => entry.Id).ToHashSet();
        if (entryIds.Count != current.Count
            || entryIds.Distinct().Count() != entryIds.Count
            || entryIds.Any(id => !current.Contains(id)))
            throw new DomainRuleException(TeamsChanged);

        var existing = Places(divisionId).ToDictionary(place => place.EntryId);
        if (existing.Count == 0)
        {
            for (var index = 0; index < entryIds.Count; index++)
                _rankings.Add(new RankingPlace(OrganizationId, Id, divisionId, entryIds[index], index + 1, isNew: false));
            return;
        }

        for (var index = 0; index < entryIds.Count; index++)
        {
            var place = existing[entryIds[index]];
            place.SetRank(index + 1);
            place.Accept();
        }
    }

    private TournamentTeam FindEntry(Guid entryId) =>
        _entries.FirstOrDefault(item => item.Id == entryId)
            ?? throw new DomainRuleException(EntryNotFound);

    private Guid RequireOwnDivision(Guid? divisionId) =>
        divisionId is { } id && _divisions.Any(division => division.Id == id)
            ? id
            : throw new DomainRuleException(Division.Required);

    private void AppendIfRanked(Guid divisionId, Guid entryId)
    {
        if (!IsRanked(divisionId))
            return;

        _rankings.Add(new RankingPlace(OrganizationId, Id, divisionId, entryId, Places(divisionId).Count + 1, isNew: true));
    }

    private void RelocateRanking(TournamentTeam entry, Guid targetDivisionId)
    {
        var place = _rankings.FirstOrDefault(item => item.EntryId == entry.Id);
        if (place is null)
        {
            AppendIfRanked(targetDivisionId, entry.Id);
            return;
        }

        var sourceDivisionId = place.DivisionId;
        if (!IsRanked(targetDivisionId))
        {
            _rankings.Remove(place);
            Renumber(sourceDivisionId);
            return;
        }

        // Rank 0 keeps this place out of the source order while the source closes up, then it takes the bottom of the target.
        place.MoveTo(targetDivisionId, rank: 0, isNew: true);
        Renumber(sourceDivisionId);
        place.SetRank(Places(targetDivisionId).Count);
    }

    private void RemoveFromRanking(TournamentTeam entry)
    {
        var place = _rankings.FirstOrDefault(item => item.EntryId == entry.Id);
        if (place is null)
            return;

        var divisionId = place.DivisionId;
        _rankings.Remove(place);
        Renumber(divisionId);
    }

    private void ClearRanking(Guid divisionId)
    {
        foreach (var place in Places(divisionId))
            _rankings.Remove(place);
    }

    private void Renumber(Guid divisionId)
    {
        var places = Places(divisionId);
        for (var index = 0; index < places.Count; index++)
            places[index].SetRank(index + 1);
    }

    private List<RankingPlace> Places(Guid divisionId) =>
        _rankings.Where(place => place.DivisionId == divisionId).OrderBy(place => place.Rank).ThenBy(place => place.Id).ToList();

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
