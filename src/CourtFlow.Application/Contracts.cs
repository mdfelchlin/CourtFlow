namespace CourtFlow.Application;

public sealed record TournamentDetails(
    Guid Id,
    string Name,
    string Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes);

public sealed record CourtDetails(Guid Id, int Number);

public sealed record VenueDetails(
    Guid Id,
    Guid TournamentId,
    string Name,
    string Address,
    int CourtCount,
    IReadOnlyList<CourtDetails> Courts);

public sealed record SaveTournament(
    string? Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? TimeZoneId,
    int? DefaultMatchDurationMinutes);

public sealed record SaveVenue(string? Name, string? Address, int? CourtCount);

public sealed record DivisionDetails(
    Guid Id,
    Guid TournamentId,
    string Name,
    string Gender);

public sealed record SaveDivision(string? Name, string? Gender);
