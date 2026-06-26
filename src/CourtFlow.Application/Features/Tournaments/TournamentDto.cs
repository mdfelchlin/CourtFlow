using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record TournamentDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    string? WebsiteUrl,
    string? ContactEmail,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes,
    TournamentSessionMode SessionMode,
    string? DefaultStartTime,
    Guid? TournamentPartnerId,
    TournamentPublicationStatus PublicationStatus,
    DateTime? PublishedAtUtc,
    DateTime? ArchivedAtUtc);
