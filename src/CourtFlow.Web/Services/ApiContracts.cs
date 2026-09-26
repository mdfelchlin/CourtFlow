namespace CourtFlow.Web.Services;

public sealed record TournamentResponse(
    Guid Id,
    string Name,
    string Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes);

public sealed record TournamentRequest(
    string? Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? TimeZoneId,
    int? DefaultMatchDurationMinutes);

public sealed record CourtResponse(Guid Id, int Number);

public sealed record VenueResponse(
    Guid Id,
    Guid TournamentId,
    string Name,
    string Address,
    int CourtCount,
    IReadOnlyList<CourtResponse> Courts);

public sealed record VenueRequest(string? Name, string? Address, int? CourtCount);
