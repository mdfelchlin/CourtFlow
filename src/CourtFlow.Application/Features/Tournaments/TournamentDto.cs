using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record TournamentDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes,
    Guid? TournamentPartnerId,
    TournamentPublicationStatus PublicationStatus,
    DateTime? PublishedAtUtc,
    DateTime? ArchivedAtUtc);
