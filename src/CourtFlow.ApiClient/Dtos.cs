namespace CourtFlow.ApiClient;

public enum WaveDto { AM = 1, PM = 2 }
public enum DivisionFormatDto { PoolOnly = 1, PoolPlusBracket = 2, BracketOnly = 3 }
public enum MatchStateDto { NotStarted = 1, InProgress = 2, Final = 3, Canceled = 4 }
public enum BracketTypeDto { SingleElimination = 1, DoubleElimination = 2 }
public enum BracketBranchDto { Winners = 1, Losers = 2, GrandFinal = 3 }
public enum CourtStatusKindDto { Idle = 0, OnTime = 1, SlightlyBehind = 2, Behind = 3, Ahead = 4 }
public enum RankedSeedStrategyDto { Snake = 1 }

public sealed record SignInRequestDto(string Email, string Password, Guid? OrganizationId);
public sealed record SignInResponseDto(string Token, DateTime ExpiresUtc, Guid UserId, Guid OrganizationId);

public sealed record TournamentDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes,
    Guid? TournamentPartnerId);

public sealed record CreateTournamentDto(string Name, string? Description, DateOnly StartDate, DateOnly EndDate, string TimeZoneId, int DefaultMatchDurationMinutes);
public sealed record UpdateTournamentDto(Guid Id, string Name, string? Description, DateOnly StartDate, DateOnly EndDate, string TimeZoneId, int DefaultMatchDurationMinutes, Guid? TournamentPartnerId);

public sealed record VenueDto(Guid Id, Guid TournamentId, string Name, string? Address);
public sealed record CreateVenueDto(Guid TournamentId, string Name, string? Address);
public sealed record UpdateVenueDto(Guid Id, string Name, string? Address);

public sealed record CourtDto(Guid Id, Guid VenueId, string Name, string[] Tags);
public sealed record CreateCourtDto(Guid VenueId, string Name, string[] Tags);
public sealed record UpdateCourtDto(Guid Id, string Name, string[] Tags);

public sealed record DivisionDto(Guid Id, Guid TournamentId, string Name, DivisionFormatDto Format, int MaxTeams);
public sealed record CreateDivisionDto(Guid TournamentId, string Name, DivisionFormatDto Format, int MaxTeams);
public sealed record UpdateDivisionDto(Guid Id, string Name, DivisionFormatDto Format, int MaxTeams);

public sealed record PoolDto(Guid Id, Guid DivisionId, string Name);
public sealed record CreatePoolDto(Guid DivisionId, string Name);
public sealed record UpdatePoolDto(Guid Id, string Name);

public sealed record TeamDto(Guid Id, Guid DivisionId, Guid? PoolId, string Name, string? Club, WaveDto Wave, int? SeedHint);
public sealed record CreateTeamDto(Guid DivisionId, string Name, string? Club);
public sealed record UpdateTeamDto(Guid Id, string Name, string? Club);
public sealed record MoveTeamDto(Guid DivisionId);

public sealed record PredefinedPoolImportRowDto(
    int RowNumber,
    string DivisionName,
    string PoolName,
    string TeamName,
    string? Club,
    WaveDto Wave,
    int? SeedHint);

public sealed record RankedTeamImportRowDto(
    int RowNumber,
    string DivisionName,
    int Rank,
    string TeamName,
    string? Club,
    WaveDto Wave);

public sealed record ImportPredefinedPoolsDto(Guid TournamentId, IReadOnlyList<PredefinedPoolImportRowDto> Rows);
public sealed record ImportRankedTeamsDto(Guid TournamentId, RankedSeedStrategyDto Strategy, IReadOnlyList<RankedTeamImportRowDto> Rows);

public sealed record ImportErrorDto(int RowNumber, string Message);
public sealed record ImportResultDto(int CreatedPools, int CreatedTeams, int UpdatedTeams, IReadOnlyList<ImportErrorDto> Errors);

public sealed record MatchSetDto(int SetNumber, int HomeScore, int AwayScore);
public sealed record MatchDto(
    Guid Id,
    Guid TournamentId,
    Guid DivisionId,
    Guid? PoolId,
    Guid? CourtId,
    Guid? HomeTeamId,
    Guid? AwayTeamId,
    Guid? RefTeamId,
    WaveDto Wave,
    DateTime ScheduledStartUtc,
    int DurationMinutes,
    DateTime? ActualStartUtc,
    DateTime? LastScoreAtUtc,
    MatchStateDto State,
    Guid? WinnerTeamId,
    Guid? BracketNodeId,
    IReadOnlyList<MatchSetDto> Sets);

public sealed record MoveMatchDto(Guid? CourtId, DateTime ScheduledStartUtc, int? DurationMinutes);
public sealed record ScoreMatchDto(IReadOnlyList<MatchSetDto> Sets, bool Finalize, Guid? WinnerTeamId);

public sealed record BracketDto(Guid Id, Guid TournamentId, Guid DivisionId, BracketTypeDto Type, bool IfNecessaryFinal, int TeamCapacity);
public sealed record BracketNodeDto(Guid Id, Guid BracketId, BracketBranchDto Branch, int Round, int Slot, Guid? MatchId, Guid? HomeTeamId, Guid? AwayTeamId, int? HomeSeed, int? AwaySeed, Guid? WinsToNodeId, Guid? LosesToNodeId);
public sealed record BracketWithNodesDto(BracketDto Bracket, IReadOnlyList<BracketNodeDto> Nodes);
public sealed record GenerateBracketDto(Guid TournamentId, Guid DivisionId, BracketTypeDto Type, int TeamCapacity, bool IfNecessaryFinal);

public enum ConflictKindDto { TeamDoubleBooking = 1, RefConflict = 2, CourtDoubleBooking = 3, WaveViolation = 4, PartnerTournamentConflict = 5 }
public sealed record ScheduleConflictDto(ConflictKindDto Kind, Guid MatchId, Guid? OtherMatchId, Guid? TeamId, Guid? CourtId, DateTime StartUtc, DateTime EndUtc, string Message);
public sealed record ConflictReportDto(IReadOnlyList<ScheduleConflictDto> Conflicts);

public sealed record CourtStatusDto(
    Guid CourtId,
    string CourtName,
    string VenueName,
    Guid? CurrentMatchId,
    string? CurrentMatchLabel,
    Guid? DivisionId,
    string? DivisionName,
    WaveDto? Wave,
    DateTime? ScheduledStartUtc,
    DateTime? ActualStartUtc,
    DateTime? LastScoreAtUtc,
    int MinutesBehind,
    CourtStatusKindDto Status);

public sealed record PoolStandingRowDto(Guid TeamId, string TeamName, int Rank, int MatchesPlayed, int MatchWins, int MatchLosses, int SetsWon, int SetsLost, double SetPercentage, int PointsFor, int PointsAgainst, double PointPercentage);
public sealed record PoolStandingsDto(Guid PoolId, string PoolName, IReadOnlyList<PoolStandingRowDto> Rows);

public sealed record PublicEventDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    string OrganizationName,
    string OrganizationSlug,
    int VenueCount,
    int DivisionCount,
    int TeamCount);

public sealed record PublicVenueDto(Guid Id, string Name, string? Address, IReadOnlyList<PublicCourtDto> Courts);
public sealed record PublicCourtDto(Guid Id, string Name);
public sealed record PublicDivisionDto(Guid Id, string Name, DivisionFormatDto Format, int TeamCount);

public sealed record PublicEventDetailDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    string OrganizationName,
    IReadOnlyList<PublicVenueDto> Venues,
    IReadOnlyList<PublicDivisionDto> Divisions);

public sealed record PublicMatchSetDto(int SetNumber, int HomeScore, int AwayScore);

public sealed record PublicMatchDto(
    Guid Id,
    Guid DivisionId,
    string DivisionName,
    Guid? PoolId,
    string? PoolName,
    Guid? CourtId,
    string? CourtName,
    string? VenueName,
    Guid? HomeTeamId,
    string? HomeTeamName,
    Guid? AwayTeamId,
    string? AwayTeamName,
    WaveDto Wave,
    DateTime ScheduledStartUtc,
    int DurationMinutes,
    DateTime? ActualStartUtc,
    MatchStateDto State,
    Guid? WinnerTeamId,
    IReadOnlyList<PublicMatchSetDto> Sets);

public sealed record PublicBracketNodeDto(
    Guid Id,
    BracketBranchDto Branch,
    int Round,
    int Slot,
    Guid? MatchId,
    Guid? HomeTeamId,
    string? HomeTeamName,
    Guid? AwayTeamId,
    string? AwayTeamName,
    int? HomeSeed,
    int? AwaySeed,
    Guid? WinsToNodeId,
    Guid? LosesToNodeId);

public sealed record PublicBracketDto(
    Guid Id,
    Guid DivisionId,
    string DivisionName,
    BracketTypeDto Type,
    bool IfNecessaryFinal,
    int TeamCapacity,
    IReadOnlyList<PublicBracketNodeDto> Nodes);

public sealed record PublicStandingsRowDto(
    Guid TeamId,
    string TeamName,
    int Rank,
    int MatchesPlayed,
    int MatchWins,
    int MatchLosses,
    int SetsWon,
    int SetsLost,
    double SetPercentage,
    int PointsFor,
    int PointsAgainst,
    double PointPercentage);

public sealed record PublicPoolStandingsDto(
    Guid PoolId,
    string PoolName,
    Guid DivisionId,
    string DivisionName,
    IReadOnlyList<PublicStandingsRowDto> Rows);
