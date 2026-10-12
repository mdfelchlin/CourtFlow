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

public sealed record TournamentTeamDetails(
    Guid Id,
    Guid TournamentId,
    Guid TeamId,
    string TeamName,
    Guid ClubId,
    string ClubName,
    Guid DivisionId,
    string DivisionName,
    string DivisionGender);

public sealed record AddTournamentTeam(string? ClubName, string? TeamName, Guid? DivisionId);

public sealed record UpdateTournamentTeam(string? TeamName, Guid? DivisionId);

public sealed record ClubDetails(Guid Id, string Name);

public sealed record ClubTeamOption(Guid Id, string Name, bool AlreadyEntered);

public enum TeamImportOutcome
{
    AddNewClubAndTeam,
    AddNewTeam,
    AddExistingTeam,
    Move,
    NoChange,
    Rejected,
}

public sealed record TeamImportRow(
    int Line,
    string Club,
    string Team,
    string Division,
    string Gender,
    TeamImportOutcome Outcome,
    string? FromDivision,
    string? Message);

public sealed record TeamImportSummary(
    int Rows,
    int NewClubs,
    int NewTeams,
    int ToAdd,
    int ToMove,
    int Unchanged,
    int Rejected);

public sealed record TeamImportPreview(
    string? FileError,
    IReadOnlyList<TeamImportRow> Rows,
    TeamImportSummary Summary,
    bool Stale);

public sealed record TeamImportResult(int Imported);
