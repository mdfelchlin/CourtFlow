using CourtFlow.Domain;

namespace CourtFlow.Application;

public sealed record TournamentDetails(
    Guid Id,
    string Name,
    string Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes,
    TournamentStatus Status,
    DateTimeOffset? LastPublishedAt)
{
    /// <summary>The day the tournament went public, in its own time zone. Only a published tournament has one.</summary>
    public DateOnly? PublishedOn => LocalDay(LastPublishedAt);

    /// <summary>The day the automatic archive comes due. The archive itself may run up to a day later.</summary>
    public DateOnly? ArchivesOn => LocalDay(LastPublishedAt + Tournament.AutoArchiveAfter);

    private DateOnly? LocalDay(DateTimeOffset? instant)
    {
        if (Status != TournamentStatus.Published || instant is not { } moment)
            return null;

        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);
    }
}

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
