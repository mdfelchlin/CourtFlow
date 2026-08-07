namespace CourtFlow.ApiClient;

public enum WaveDto { AM = 1, PM = 2 }
public enum DivisionFormatDto { PoolOnly = 1, PoolPlusBracket = 2, BracketOnly = 3 }
public enum MatchStateDto { NotStarted = 1, InProgress = 2, Final = 3, Canceled = 4 }
public enum BracketTypeDto { SingleElimination = 1, DoubleElimination = 2 }
public enum BracketBranchDto { Winners = 1, Losers = 2, GrandFinal = 3, ThirdPlace = 4 }
public enum CourtStatusKindDto { Idle = 0, OnTime = 1, SlightlyBehind = 2, Behind = 3, Ahead = 4 }
public enum RankedSeedStrategyDto { Snake = 1 }
public enum StageTypeDto { Pool = 1, Bracket = 2 }
public enum StageStatusDto { Draft = 1, Configured = 2, Seeded = 3, InProgress = 4, Completed = 5 }
public enum MatchSlotKindDto { Home = 1, Away = 2, Ref = 3 }
public enum MatchSlotSourceKindDto { FixedTeam = 1, MatchWinner = 2, MatchLoser = 3, PoolFinish = 4, Ranking = 5 }
public enum DivisionValidationSeverityDto { Info = 0, Warning = 1 }

public enum TournamentPublicationStatusDto { Draft, Published, Archived }

public enum TournamentSessionModeDto { SingleSession, MultiWave }

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
    TournamentSessionModeDto SessionMode,
    string? DefaultStartTime,
    Guid? TournamentPartnerId,
    TournamentPublicationStatusDto PublicationStatus,
    DateTime? PublishedAtUtc,
    DateTime? ArchivedAtUtc,
    DivisionFormatDto? PlayFormat);

public sealed record CreateTournamentDto(string Name, string? Description, string? WebsiteUrl, string? ContactEmail, DateOnly StartDate, DateOnly EndDate, string TimeZoneId, int DefaultMatchDurationMinutes);
public sealed record UpdateTournamentDto(Guid Id, string Name, string? Description, string? WebsiteUrl, string? ContactEmail, DateOnly StartDate, DateOnly EndDate, string TimeZoneId, int DefaultMatchDurationMinutes, Guid? TournamentPartnerId);

public sealed record UpsertTournamentSessionSettingsDto(
    TournamentSessionModeDto SessionMode,
    string? DefaultStartTime,
    string? AmStartTime,
    string? PmStartTime);

public sealed record SetTournamentPlayFormatDto(DivisionFormatDto Format);

public sealed record VenueDto(Guid Id, Guid TournamentId, string Name, string? Address, int CourtCount);
public sealed record CreateVenueDto(Guid TournamentId, string Name, string? Address, int CourtCount);
public sealed record UpdateVenueDto(Guid Id, string Name, string? Address, int CourtCount);

public sealed record CourtDto(Guid Id, Guid VenueId, string Name, string[] Tags);
public sealed record CreateCourtDto(Guid VenueId, string Name, string[] Tags);
public sealed record UpdateCourtDto(Guid Id, string Name, string[] Tags);

public sealed record TournamentWaveDto(
    Guid Id,
    Guid TournamentId,
    string Code,
    int DisplayOrder,
    string? DefaultStartTime,
    string? DefaultEndTime,
    string? Notes);
public sealed record CreateTournamentWaveDto(string Code, string? DefaultStartTime, string? DefaultEndTime, string? Notes);
public sealed record UpdateTournamentWaveDto(string? DefaultStartTime, string? DefaultEndTime, string? Notes);

public sealed record DivisionStageHintDto(StageTypeDto StageType, StageStatusDto Status);

public enum DivisionTiebreakerPresetDto
{
    WinsH2HSetsPoints = 0,
    WinsSetsPoints = 1,
    SetsPoints = 2,
    H2HSetsPoints = 3
}

public sealed record DivisionDto(
    Guid Id,
    Guid TournamentId,
    string Name,
    DivisionFormatDto Format,
    int MaxTeams,
    DivisionTiebreakerPresetDto TiebreakerPreset,
    IReadOnlyList<DivisionStageHintDto> StageHints);
public sealed record CreateDivisionDto(Guid TournamentId, string Name);
public sealed record UpdateDivisionDto(
    Guid Id,
    string Name,
    DivisionFormatDto Format,
    int MaxTeams,
    DivisionTiebreakerPresetDto? TiebreakerPreset = null);

public sealed record StageDto(
    Guid Id,
    Guid DivisionId,
    int StageNumber,
    StageTypeDto StageType,
    string Name,
    StageStatusDto Status,
    string? SeedingSource);
public sealed record TransitionStageDto(StageStatusDto TargetStatus);

public sealed record PoolDto(Guid Id, Guid DivisionId, string Name);
public sealed record CreatePoolDto(Guid DivisionId, string Name);
public sealed record UpdatePoolDto(Guid Id, string Name);

public sealed record TeamDto(Guid Id, Guid? TournamentTeamId, Guid DivisionId, Guid? PoolId, string Name, string? Club, WaveDto Wave, int? SeedHint);
public sealed record CreateTeamDto(Guid DivisionId, string Name, string? Club);
public sealed record UpdateTeamDto(Guid Id, string Name, string? Club);
public sealed record MoveTeamDto(Guid DivisionId);

public sealed record AssignTeamPoolDto(Guid? PoolId);
public sealed record ReorderDivisionTeamsDto(IReadOnlyList<Guid> OrderedTeamIds);
public sealed record ApplyDivisionSeedingDto(RankedSeedStrategyDto Strategy);
public sealed record ApplyDivisionSeedingResultDto(bool Ok, string? Error);
public sealed record GenerateAdvancementBracketsDto(
    int TopNPerPool,
    BracketTypeDto Type,
    bool IfNecessaryFinal,
    IReadOnlyList<Guid>? GoldTeamIds,
    IReadOnlyList<Guid>? SilverTeamIds);
public sealed record GenerateAdvancementBracketsResultDto(IReadOnlyList<BracketDto> Brackets, string? Error);

public sealed record PoolCourtAllocationDto(Guid CourtId, string AllocationType, int PriorityOrder);

public sealed record DivisionPoolStructureDto(
    Guid Id,
    string Name,
    int Sequence,
    int TargetSize,
    IReadOnlyList<PoolCourtAllocationDto> CourtAllocations);

public sealed record DivisionBracketStructureDto(
    Guid? Id,
    string Name,
    BracketTypeDto Type,
    int TeamCount,
    bool IfNecessaryFinal,
    int Sequence,
    bool ThirdPlaceMatch = false);

public sealed record DivisionAdvancementRuleDto(
    Guid? Id,
    Guid? SourcePoolId,
    int SourceFinishPosition,
    string TargetBracketName,
    int TargetSeedSlot,
    string OrderBy,
    int Priority);

public sealed record BracketSlotOverrideDto(Guid? ManualTournamentTeamId, string? Reason);

public sealed record BracketSeedSlotDto(
    Guid Id,
    Guid BracketId,
    string BracketName,
    int SeedPosition,
    string? Label,
    Guid? AdvancementRuleId,
    Guid? ManualTournamentTeamId,
    Guid? ResolvedTournamentTeamId,
    string ExpectedSourceType);

public sealed record PoolSettingsDto(
    int TeamsPerPoolTarget,
    string PoolSizeStrategy,
    string SeedingMethod,
    string ClubSeparationMode,
    string PlayStyle,
    string CourtAllocationMode,
    string RefAssignmentMode);

public sealed record DivisionWizardStateDto(
    Guid DivisionId,
    IReadOnlyList<DivisionPoolStructureDto> Pools,
    IReadOnlyList<DivisionBracketStructureDto> Brackets,
    IReadOnlyList<DivisionAdvancementRuleDto> AdvancementRules,
    IReadOnlyList<BracketSeedSlotDto> BracketSeedSlots,
    IReadOnlyList<Guid> AvailableCourtIds,
    PoolSettingsDto? PoolSettings);

public sealed record SaveDivisionPoolStructureDto(IReadOnlyList<DivisionPoolStructureDto> Pools);
public sealed record SaveDivisionPoolSettingsDto(PoolSettingsDto Settings);
public sealed record SaveDivisionBracketStructureDto(IReadOnlyList<DivisionBracketStructureDto> Brackets);
public sealed record SaveDivisionAdvancementRulesDto(IReadOnlyList<DivisionAdvancementRuleDto> Rules);

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
public sealed record ForfeitMatchDto(Guid WinningTeamId);
public sealed record ChangeRefereeDto(Guid? RefTeamId);

public sealed record DivisionValidationIssueDto(
    DivisionValidationSeverityDto Severity,
    string Message,
    string? Location);

public sealed record DivisionValidationResultDto(IReadOnlyList<DivisionValidationIssueDto> Issues);

public sealed record MatchSlotSourceDto(
    Guid Id,
    Guid MatchId,
    MatchSlotKindDto Slot,
    MatchSlotSourceKindDto SourceKind,
    Guid? FixedTournamentTeamId,
    Guid? SourceMatchId,
    Guid? SourcePoolId,
    int? SourcePosition,
    Guid? ResolvedTournamentTeamId);

public sealed record DivisionMatchGraphMatchDto(
    MatchDto Match,
    IReadOnlyList<MatchSlotSourceDto> SlotSources,
    int DisplayNumber);

public sealed record DivisionMatchGraphDto(
    Guid DivisionId,
    IReadOnlyList<DivisionMatchGraphMatchDto> Matches);

public sealed record CreateDivisionMatchDto(
    Guid DivisionId,
    Guid? StageId,
    Guid? PoolId,
    Guid? CourtId,
    DateTime ScheduledStartUtc,
    int DurationMinutes,
    Guid? HomeTeamId,
    Guid? AwayTeamId,
    Guid? RefTeamId);

public sealed record SetMatchSlotSourceDto(
    MatchSlotKindDto Slot,
    MatchSlotSourceKindDto SourceKind,
    Guid? FixedTournamentTeamId,
    Guid? SourceMatchId,
    Guid? SourcePoolId,
    int? SourcePosition);

public sealed record BracketDto(Guid Id, Guid TournamentId, Guid DivisionId, BracketTypeDto Type, bool IfNecessaryFinal, int TeamCapacity, string? DisplayName, bool ThirdPlaceMatch = false);
public sealed record BracketNodeDto(Guid Id, Guid BracketId, BracketBranchDto Branch, int Round, int Slot, Guid? MatchId, Guid? HomeTeamId, Guid? AwayTeamId, int? HomeSeed, int? AwaySeed, Guid? WinsToNodeId, Guid? LosesToNodeId);
public sealed record BracketWithNodesDto(BracketDto Bracket, IReadOnlyList<BracketNodeDto> Nodes);
public sealed record BracketTreeSetScoreDto(int SetNumber, int HomeScore, int AwayScore);
public sealed record BracketTreeNodeDto(
    Guid Id,
    Guid BracketId,
    BracketBranchDto Branch,
    int Round,
    int Slot,
    Guid? MatchId,
    Guid? HomeTeamId,
    Guid? AwayTeamId,
    int? HomeSeed,
    int? AwaySeed,
    Guid? WinsToNodeId,
    Guid? LosesToNodeId,
    string HomeLabel,
    string AwayLabel,
    bool HomeIsBye,
    bool AwayIsBye,
    bool IsPinnedHome,
    bool IsPinnedAway,
    MatchStateDto? MatchState,
    IReadOnlyList<BracketTreeSetScoreDto> Sets,
    string? HomeSetsSummary,
    string? AwaySetsSummary,
    Guid? WinnerTeamId,
    string? WinnerSide,
    string? CourtName,
    DateTime? ScheduledStartUtc,
    string? MatchLabel = null);

public enum BracketDisplayGroupKindDto
{
    Main = 1,
    Lower = 2,
    SingleMatch = 3,
    GrandFinal = 4,
    ThirdPlace = 5
}

public sealed record BracketDisplayGroupDto(
    string Id,
    int Sequence,
    string Title,
    string? Subtitle,
    BracketDisplayGroupKindDto Kind,
    IReadOnlyList<Guid> NodeIds);

public sealed record BracketTreeDto(
    BracketDto Bracket,
    IReadOnlyList<BracketTreeNodeDto> Nodes,
    IReadOnlyList<BracketDisplayGroupDto> Groups);
public sealed record GenerateBracketDto(Guid TournamentId, Guid DivisionId, BracketTypeDto Type, int TeamCapacity, bool IfNecessaryFinal, string? DisplayName = null, bool ThirdPlaceMatch = false);

public enum ConflictKindDto { TeamDoubleBooking = 1, RefConflict = 2, CourtDoubleBooking = 3, WaveViolation = 4, PartnerTournamentConflict = 5 }
public sealed record ScheduleConflictDto(ConflictKindDto Kind, Guid MatchId, Guid? OtherMatchId, Guid? TeamId, Guid? CourtId, DateTime StartUtc, DateTime EndUtc, string Message);
public sealed record ConflictReportDto(IReadOnlyList<ScheduleConflictDto> Conflicts);

public enum ScheduleVersionStatusDto { Draft = 1, Preview = 2, Published = 3, Superseded = 4 }

public sealed record ScheduleVersionSummaryDto(
    Guid Id,
    ScheduleVersionStatusDto Status,
    long MutationSequence,
    DateTime? PublishedAt,
    bool IsWorking);
public sealed record PreviewScheduleMoveRequestDto(Guid MatchId, Guid? CourtId, DateTime ScheduledStartUtc, int? DurationMinutes);
public sealed record PreviewScheduleMoveResponseDto(Guid ScheduleVersionId, long MutationSequence, ConflictReportDto Conflicts, string ApplyToken, bool CanApply);
public sealed record ApplyScheduleMoveRequestDto(string ApplyToken);

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
public sealed record DivisionStandingsDto(string TiebreakerPreset, IReadOnlyList<PoolStandingsDto> Pools);

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

public sealed record PublicDivisionTeamsDto(Guid DivisionId, string DivisionName, IReadOnlyList<PublicTeamRosterDto> Teams);
public sealed record PublicTeamRosterDto(Guid TeamId, string TeamName, Guid? PoolId, string? PoolName);

public sealed record PublicEventReadinessDto(bool TeamsReady, bool ScheduleReady, bool BracketsReady, bool StandingsReady);

public sealed record PublicEventDetailDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    string OrganizationName,
    string? WebsiteUrl,
    string? ContactEmail,
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
    Guid? RefTeamId,
    string? RefTeamName,
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
    Guid? LosesToNodeId,
    string HomeLabel,
    string AwayLabel,
    bool HomeIsBye,
    bool AwayIsBye,
    bool IsPinnedHome,
    bool IsPinnedAway,
    MatchStateDto? MatchState,
    IReadOnlyList<PublicBracketSetScoreDto> Sets,
    string? HomeSetsSummary,
    string? AwaySetsSummary,
    Guid? WinnerTeamId,
    string? WinnerSide,
    string? CourtName,
    DateTime? ScheduledStartUtc,
    string? MatchLabel = null);

public sealed record PublicBracketSetScoreDto(int SetNumber, int HomeScore, int AwayScore);

public sealed record PublicBracketDisplayGroupDto(
    string Id,
    int Sequence,
    string Title,
    string? Subtitle,
    BracketDisplayGroupKindDto Kind,
    IReadOnlyList<Guid> NodeIds);

public sealed record PublicBracketDto(
    Guid Id,
    Guid DivisionId,
    string DivisionName,
    BracketTypeDto Type,
    bool IfNecessaryFinal,
    int TeamCapacity,
    string? DisplayName,
    bool ThirdPlaceMatch,
    IReadOnlyList<PublicBracketNodeDto> Nodes,
    IReadOnlyList<PublicBracketDisplayGroupDto> Groups);

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

// --- Repairs ---
public enum RepairTriggerTypeDto { TeamWithdrawal = 1, CourtOutage = 2, RefereeReassignment = 3, WorkTeamReassignment = 4, ScheduleReflow = 5, PoolCourtSwap = 6 }
public enum RepairJobStatusDto { Pending = 1, Previewing = 2, Applied = 3, Rejected = 4 }
public enum RepairActionTypeDto { MatchAssignmentMove = 1, MatchCancellation = 2, TeamWithdrawal = 3, WorkTeamReassignment = 4, PoolCourtAllocationChange = 5, ScheduleReflow = 6, CourtOutageMarked = 7 }

public sealed record RepairJobDto(Guid Id, RepairTriggerTypeDto TriggerType, RepairJobStatusDto Status, Guid? DivisionId, DateTime CreatedAt);
public sealed record RepairActionDto(Guid Id, RepairActionTypeDto ActionType, string? TargetId, string? Before, string? After);
public sealed record RepairJobDetailDto(Guid Id, RepairTriggerTypeDto TriggerType, RepairJobStatusDto Status, Guid? DivisionId, DateTime CreatedAt, string? PreviewDiagnosticsJson, IReadOnlyList<RepairActionDto> Actions);
public sealed record TeamWithdrawalDto(Guid TournamentTeamId, string? Reason, Guid? EffectiveAfterMatchId);
public sealed record CourtOutageDto(Guid CourtId, DateTime StartsAt, DateTime EndsAt, string? Reason);
public sealed record RefereeReassignmentDto(Guid MatchId, Guid? RefTeamId);

public sealed record ReflowPreviewDto(
    IReadOnlyList<string> Notes,
    IReadOnlyList<ReflowMoveDto> Moves,
    string? ApplyToken);

public sealed record ReflowMoveDto(
    Guid MatchAssignmentId,
    Guid MatchId,
    DateTime OldScheduledStart,
    DateTime NewScheduledStart,
    int ShiftMinutes);

public sealed record ReflowApplyDto(string ApplyToken);
public sealed record ReflowApplyResultDto(Guid RepairJobId, int AssignmentsUpdated);

public sealed record PoolMatchWorkTeamDto(
    int Order,
    Guid MatchId,
    string HomeTeamName,
    string AwayTeamName,
    string? HomeTeamDisplay,
    string? AwayTeamDisplay,
    Guid? WorkTeamTournamentTeamId,
    string? WorkTeamName,
    string? WorkTeamDisplay,
    string AssignmentSource);

public sealed record DivisionPoolWorkRotationDto(
    Guid PoolId,
    string PoolName,
    IReadOnlyList<PoolMatchWorkTeamDto> Matches);

// --- Templates ---
public sealed record TemplateDto(Guid Id, string Name, string? Description, Guid? SourceTournamentId, int UsageCount, DateTime CreatedAt);
public sealed record CaptureTemplateDto(Guid SourceTournamentId, string? Name);
public sealed record CreateTournamentFromTemplateDto(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes);
public sealed record TournamentFromTemplateDto(Guid TournamentId, string Name);
