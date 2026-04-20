using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.Features.Public;

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

public sealed record PublicVenueDto(Guid Id, string Name, string? Address, IReadOnlyList<PublicCourtDto> Courts);
public sealed record PublicCourtDto(Guid Id, string Name);

public sealed record PublicDivisionDto(Guid Id, string Name, DivisionFormat Format, int TeamCount);

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
    Wave Wave,
    DateTime ScheduledStartUtc,
    int DurationMinutes,
    DateTime? ActualStartUtc,
    MatchState State,
    Guid? WinnerTeamId,
    IReadOnlyList<PublicMatchSetDto> Sets);

public sealed record PublicMatchSetDto(int SetNumber, int HomeScore, int AwayScore);

public sealed record PublicBracketDto(
    Guid Id,
    Guid DivisionId,
    string DivisionName,
    BracketType Type,
    bool IfNecessaryFinal,
    int TeamCapacity,
    IReadOnlyList<PublicBracketNodeDto> Nodes);

public sealed record PublicBracketNodeDto(
    Guid Id,
    BracketBranch Branch,
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
