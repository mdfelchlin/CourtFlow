using CourtFlow.Domain;

namespace CourtFlow.Web.Services;

public sealed record TournamentResponse(
    Guid Id,
    string Name,
    string Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes,
    TournamentStatus Status = TournamentStatus.Unpublished,
    DateTimeOffset? LastPublishedAt = null,
    DateOnly? PublishedOn = null,
    DateOnly? ArchivesOn = null);

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

public sealed record DivisionResponse(
    Guid Id,
    Guid TournamentId,
    string Name,
    string Gender);

public sealed record DivisionRequest(string? Name, string? Gender);

public sealed record TournamentTeamResponse(
    Guid Id,
    Guid TournamentId,
    Guid TeamId,
    string TeamName,
    Guid ClubId,
    string ClubName,
    Guid DivisionId,
    string DivisionName,
    string DivisionGender);

public sealed record TournamentTeamRequest(string? ClubName, string? TeamName, Guid? DivisionId);

public sealed record TournamentTeamUpdateRequest(string? TeamName, Guid? DivisionId);

public sealed record ClubResponse(Guid Id, string Name);

public sealed record ClubTeamOptionResponse(Guid Id, string Name, bool AlreadyEntered);

/// <summary>Matches the API's enum order; the API sends it as a number.</summary>
public enum TeamImportOutcome
{
    AddNewClubAndTeam,
    AddNewTeam,
    AddExistingTeam,
    Move,
    NoChange,
    Rejected,
}

public sealed record TeamImportRowResponse(
    int Line,
    string Club,
    string Team,
    string Division,
    string Gender,
    TeamImportOutcome Outcome,
    string? FromDivision,
    string? Message);

public sealed record TeamImportSummaryResponse(
    int Rows,
    int NewClubs,
    int NewTeams,
    int ToAdd,
    int ToMove,
    int Unchanged,
    int Rejected);

public sealed record TeamImportPreviewResponse(
    string? FileError,
    IReadOnlyList<TeamImportRowResponse> Rows,
    TeamImportSummaryResponse Summary,
    bool Stale);

public sealed record TeamImportResultResponse(int Imported);

/// <summary>An import either was written, or came back with a fresh preview because something changed.</summary>
public sealed record TeamImportResponse(TeamImportResultResponse? Imported, TeamImportPreviewResponse? Stale);

public sealed record FormatDivisionResponse(
    Guid Id,
    Guid TournamentId,
    string Name,
    string Gender,
    int TeamCount,
    bool Ranked,
    int NewTeams,
    string Ranking);

public sealed record RankingTeamResponse(
    Guid EntryId,
    string ClubName,
    string TeamName,
    int Rank,
    bool IsNew);

public sealed record DivisionRankingResponse(
    Guid DivisionId,
    Guid TournamentId,
    string TournamentName,
    TournamentStatus TournamentStatus,
    string DivisionName,
    string DivisionGender,
    bool Ranked,
    IReadOnlyList<RankingTeamResponse> Teams);

public sealed record SaveRankingRequest(IReadOnlyList<Guid> EntryIds);