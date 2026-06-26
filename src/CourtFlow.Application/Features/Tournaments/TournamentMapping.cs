using CourtFlow.Domain.Entities;

namespace CourtFlow.Application.Features.Tournaments;

internal static class TournamentMapping
{
    public static TournamentDto ToDto(this Tournament t) => new(
        t.Id,
        t.OrganizationId,
        t.Name,
        t.Description,
        t.WebsiteUrl,
        t.ContactEmail,
        t.StartDate,
        t.EndDate,
        t.TimeZoneId,
        t.DefaultMatchDurationMinutes,
        t.SessionMode,
        t.DefaultStartTime,
        t.TournamentPartnerId,
        t.PublicationStatus,
        t.PublishedAtUtc,
        t.ArchivedAtUtc);
}
