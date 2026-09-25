using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;
using CourtFlow.Domain.Events;

namespace CourtFlow.Domain.Entities;

public class Tournament : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? ContactEmail { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string TimeZoneId { get; private set; } = "America/Los_Angeles";
    public int DefaultMatchDurationMinutes { get; private set; } = TournamentDefaults.DefaultMatchDurationMinutes;
    public TournamentSessionMode SessionMode { get; private set; } = TournamentSessionMode.SingleSession;
    public string? DefaultStartTime { get; private set; }
    public Guid? TournamentPartnerId { get; private set; }

    /// <summary>Whether this tournament appears on the public landing and public event APIs.</summary>
    public TournamentPublicationStatus PublicationStatus { get; private set; } = TournamentPublicationStatus.Draft;
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }

    private readonly List<Venue> _venues = new();
    public IReadOnlyCollection<Venue> Venues => _venues.AsReadOnly();

    private readonly List<Division> _divisions = new();
    public IReadOnlyCollection<Division> Divisions => _divisions.AsReadOnly();

    private Tournament() { }

    public Tournament(Guid organizationId, string name, DateOnly start, DateOnly end, string timeZoneId, int defaultMatchDurationMinutes = TournamentDefaults.DefaultMatchDurationMinutes)
    {
        OrganizationId = organizationId;
        Rename(name);
        SetDates(start, end);
        SetTimeZone(timeZoneId);
        DefaultMatchDurationMinutes = defaultMatchDurationMinutes;
    }

    public void Rename(string name)
    {
        EnsureNotArchived();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tournament name required", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void SetDescription(string? description)
    {
        EnsureNotArchived();
        Description = description;
        Touch();
    }

    public void SetContactInfo(string? websiteUrl, string? contactEmail)
    {
        EnsureNotArchived();
        WebsiteUrl = string.IsNullOrWhiteSpace(websiteUrl) ? null : websiteUrl.Trim();
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        Touch();
    }

    public void SetDates(DateOnly start, DateOnly end)
    {
        EnsureNotArchived();
        if (end < start) throw new ArgumentException("End date must be on or after start date");
        StartDate = start;
        EndDate = end;
        Touch();
    }

    public void SetTimeZone(string timeZoneId)
    {
        EnsureNotArchived();
        if (string.IsNullOrWhiteSpace(timeZoneId)) throw new ArgumentException("Time zone required", nameof(timeZoneId));
        TimeZoneId = timeZoneId;
        Touch();
    }

    public void SetDefaultMatchDuration(int minutes)
    {
        EnsureNotArchived();
        if (minutes <= 0) throw new ArgumentException("Match duration must be > 0", nameof(minutes));
        DefaultMatchDurationMinutes = minutes;
        Touch();
    }

    public void SetSessionMode(TournamentSessionMode mode)
    {
        EnsureNotArchived();
        SessionMode = mode;
        Touch();
    }

    public void SetDefaultStartTime(string? startTime)
    {
        EnsureNotArchived();
        DefaultStartTime = string.IsNullOrWhiteSpace(startTime) ? null : startTime.Trim();
        Touch();
    }

    public void SetPartner(Guid? partnerTournamentId)
    {
        EnsureNotArchived();
        TournamentPartnerId = partnerTournamentId;
        Touch();
    }

    public void Publish()
    {
        if (PublicationStatus == TournamentPublicationStatus.Archived)
            throw new InvalidOperationException("Restore this tournament from archive before publishing.");

        PublicationStatus = TournamentPublicationStatus.Published;
        PublishedAtUtc = DateTime.UtcNow;
        ArchivedAtUtc = null;
        Touch();
        RaiseEvent(new TournamentPublished(Id));
    }

    public void Unpublish()
    {
        if (PublicationStatus != TournamentPublicationStatus.Published)
            return;

        PublicationStatus = TournamentPublicationStatus.Draft;
        Touch();
        RaiseEvent(new TournamentUnpublished(Id));
    }

    public void Archive()
    {
        if (PublicationStatus == TournamentPublicationStatus.Archived)
            return;

        PublicationStatus = TournamentPublicationStatus.Archived;
        ArchivedAtUtc = DateTime.UtcNow;
        Touch();
        RaiseEvent(new TournamentArchived(Id));
    }

    public void RestoreFromArchive()
    {
        if (PublicationStatus != TournamentPublicationStatus.Archived)
            throw new InvalidOperationException("Tournament is not archived.");

        PublicationStatus = TournamentPublicationStatus.Draft;
        ArchivedAtUtc = null;
        Touch();
    }

    private void EnsureNotArchived()
    {
        if (PublicationStatus == TournamentPublicationStatus.Archived)
            throw new InvalidOperationException("Tournament is archived.");
    }
}
