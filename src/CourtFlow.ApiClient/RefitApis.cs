using System.Net.Http;
using Refit;

namespace CourtFlow.ApiClient;

public interface ICourtFlowTournamentsApi
{
    [Get("/api/v1/tournaments")]
    Task<List<TournamentDto>> ListAsync(CancellationToken cancellationToken);

    [Get("/api/v1/tournaments/{id}")]
    Task<TournamentDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments")]
    Task<TournamentDto> CreateAsync([Body] CreateTournamentDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/tournaments/{id}")]
    Task<TournamentDto> UpdateAsync(Guid id, [Body] UpdateTournamentDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/tournaments/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{id}/publish")]
    Task<TournamentDto> PublishAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{id}/unpublish")]
    Task<TournamentDto> UnpublishAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{id}/archive")]
    Task<TournamentDto> ArchiveAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{id}/restore")]
    Task<TournamentDto> RestoreAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{id}/clone")]
    Task<TournamentDto> CloneAsync(Guid id, [Query] bool structureOnly = false, CancellationToken cancellationToken = default);

    [Put("/api/v1/tournaments/{id}/session-settings")]
    Task<TournamentDto> UpsertSessionSettingsAsync(Guid id, [Body] UpsertTournamentSessionSettingsDto dto, CancellationToken cancellationToken = default);

    [Put("/api/v1/tournaments/{id}/play-format")]
    Task<TournamentDto> SetPlayFormatAsync(Guid id, [Body] SetTournamentPlayFormatDto dto, CancellationToken cancellationToken = default);
}

public interface ICourtFlowVenuesApi
{
    [Get("/api/v1/venues")]
    Task<List<VenueDto>> ListAsync([Query] Guid? tournamentId, CancellationToken cancellationToken);

    [Get("/api/v1/venues/{id}")]
    Task<VenueDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/venues")]
    Task<VenueDto> CreateAsync([Body] CreateVenueDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/venues/{id}")]
    Task<VenueDto> UpdateAsync(Guid id, [Body] UpdateVenueDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/venues/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICourtFlowCourtsApi
{
    [Get("/api/v1/courts")]
    Task<List<CourtDto>> ListAsync([Query] Guid? venueId, CancellationToken cancellationToken);

    [Post("/api/v1/courts")]
    Task<CourtDto> CreateAsync([Body] CreateCourtDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/courts/{id}")]
    Task<CourtDto> UpdateAsync(Guid id, [Body] UpdateCourtDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/courts/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/courts/{courtId}/delay")]
    Task<HttpResponseMessage> DelayCourtAsync(Guid courtId, [AliasAs("slots")] int slots, CancellationToken cancellationToken);
}

public interface ICourtFlowDivisionsApi
{
    [Get("/api/v1/divisions")]
    Task<List<DivisionDto>> ListAsync([Query] Guid? tournamentId, CancellationToken cancellationToken);

    [Post("/api/v1/divisions")]
    Task<DivisionDto> CreateAsync([Body] CreateDivisionDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/divisions/{id}")]
    Task<DivisionDto> UpdateAsync(Guid id, [Body] UpdateDivisionDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/divisions/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken);

    [Put("/api/v1/divisions/{divisionId}/team-order")]
    Task<List<TeamDto>> ReorderTeamsAsync(Guid divisionId, [Body] ReorderDivisionTeamsDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/randomize-seeds")]
    Task<List<TeamDto>> RandomizeSeedsAsync(Guid divisionId, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/apply-seeding")]
    Task<ApplyDivisionSeedingResultDto> ApplySeedingAsync(Guid divisionId, [Body] ApplyDivisionSeedingDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/advancement-brackets")]
    Task<GenerateAdvancementBracketsResultDto> GenerateAdvancementBracketsAsync(Guid divisionId, [Body] GenerateAdvancementBracketsDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/snake-into-pools")]
    Task<HttpResponseMessage> SnakeIntoPoolsAsync(Guid divisionId, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/generate-pool-matches")]
    Task<HttpResponseMessage> GeneratePoolMatchesAsync(Guid divisionId, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/generate-brackets")]
    Task<HttpResponseMessage> GenerateBracketsAsync(Guid divisionId, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/resolve-bracket-slots")]
    Task<HttpResponseMessage> ResolveBracketSlotsAsync(Guid divisionId, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/seed-bracket")]
    Task<HttpResponseMessage> SeedBracketFromRankingsAsync(Guid divisionId, CancellationToken cancellationToken);

    [Get("/api/v1/divisions/{divisionId}/wizard")]
    Task<DivisionWizardStateDto?> GetWizardStateAsync(Guid divisionId, CancellationToken cancellationToken);

    [Put("/api/v1/divisions/{divisionId}/wizard/pools")]
    Task<DivisionWizardStateDto?> SavePoolStructureAsync(Guid divisionId, [Body] SaveDivisionPoolStructureDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/divisions/{divisionId}/wizard/pool-settings")]
    Task<DivisionWizardStateDto?> SavePoolSettingsAsync(Guid divisionId, [Body] SaveDivisionPoolSettingsDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/divisions/{divisionId}/wizard/brackets")]
    Task<DivisionWizardStateDto?> SaveBracketStructureAsync(Guid divisionId, [Body] SaveDivisionBracketStructureDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/divisions/{divisionId}/wizard/advancement-rules")]
    Task<DivisionWizardStateDto?> SaveAdvancementRulesAsync(Guid divisionId, [Body] SaveDivisionAdvancementRulesDto dto, CancellationToken cancellationToken);

    [Get("/api/v1/divisions/{divisionId}/work-rotation")]
    Task<List<DivisionPoolWorkRotationDto>> GetWorkRotationAsync(Guid divisionId, CancellationToken cancellationToken);

    [Get("/api/v1/divisions/{divisionId}/validation")]
    Task<DivisionValidationResultDto?> GetValidationAsync(Guid divisionId, CancellationToken cancellationToken);

    [Get("/api/v1/divisions/{divisionId}/match-graph")]
    Task<DivisionMatchGraphDto?> GetMatchGraphAsync(Guid divisionId, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/{divisionId}/resolve-match-sources")]
    Task<HttpResponseMessage> ResolveMatchSourcesAsync(Guid divisionId, CancellationToken cancellationToken);

    [Post("/api/v1/divisions/bracket-seed-slots/{slotId}/override")]
    Task<BracketSeedSlotDto> SetBracketSlotOverrideAsync(Guid slotId, [Body] BracketSlotOverrideDto dto, CancellationToken cancellationToken);
}

public interface ICourtFlowStagesApi
{
    [Get("/api/v1/stages")]
    Task<List<StageDto>> ListAsync([Query] Guid? divisionId, CancellationToken cancellationToken);

    [Get("/api/v1/stages/{id}")]
    Task<StageDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    [Put("/api/v1/stages/{id}/status")]
    Task<StageDto?> TransitionAsync(Guid id, [Body] TransitionStageDto dto, CancellationToken cancellationToken);
}

public interface ICourtFlowPoolsApi
{
    [Get("/api/v1/pools")]
    Task<List<PoolDto>> ListAsync([Query] Guid? divisionId, CancellationToken cancellationToken);

    [Post("/api/v1/pools")]
    Task<PoolDto> CreateAsync([Body] CreatePoolDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/pools/{id}")]
    Task<PoolDto> UpdateAsync(Guid id, [Body] UpdatePoolDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/pools/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICourtFlowTeamsApi
{
    [Get("/api/v1/teams")]
    Task<List<TeamDto>> ListAsync([Query] Guid? tournamentId, [Query] Guid? divisionId, [Query] Guid? poolId, CancellationToken cancellationToken);

    [Post("/api/v1/teams")]
    Task<TeamDto> CreateAsync([Body] CreateTeamDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/teams/{id}")]
    Task<TeamDto> UpdateAsync(Guid id, [Body] UpdateTeamDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/teams/{teamId}/division")]
    Task<TeamDto> MoveDivisionAsync(Guid teamId, [Body] MoveTeamDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/teams/{teamId}/pool")]
    Task<AssignTeamPoolResultDto> AssignPoolAsync(Guid teamId, [Body] AssignTeamPoolDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/teams/{teamId}/swap-pool")]
    Task<SwapTeamPoolsResultDto> SwapPoolAsync(Guid teamId, [Body] SwapTeamPoolDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/teams/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICourtFlowImportsApi
{
    [Post("/api/v1/imports/predefined-pools")]
    Task<ImportResultDto> ImportPredefinedPoolsAsync([Body] ImportPredefinedPoolsDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/imports/ranked-teams")]
    Task<ImportResultDto> ImportRankedTeamsAsync([Body] ImportRankedTeamsDto dto, CancellationToken cancellationToken);
}

public interface ICourtFlowMatchesApi
{
    [Get("/api/v1/matches")]
    Task<List<MatchDto>> ListAsync([Query] Guid? tournamentId, [Query] Guid? divisionId, CancellationToken cancellationToken);

    [Get("/api/v1/matches/{id}")]
    Task<MatchDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/matches/{id}/move")]
    Task<MatchDto> MoveAsync(Guid id, [Body] MoveMatchDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/matches/{id}/score")]
    Task<MatchDto> ScoreAsync(Guid id, [Body] ScoreMatchDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/matches/{id}/forfeit")]
    Task<MatchDto> ForfeitAsync(Guid id, [Body] ForfeitMatchDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/matches/{id}/start")]
    Task<MatchDto> StartAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/matches/{id}/referee")]
    Task<MatchDto> ChangeRefereeAsync(Guid id, [Body] ChangeRefereeDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/matches")]
    Task<MatchDto> CreateAsync([Body] CreateDivisionMatchDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/matches/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken);

    [Put("/api/v1/matches/{id}/slot-source")]
    Task<MatchSlotSourceDto> SetSlotSourceAsync(Guid id, [Body] SetMatchSlotSourceDto dto, CancellationToken cancellationToken);
}

public interface ICourtFlowBracketsApi
{
    [Get("/api/v1/brackets")]
    Task<List<BracketDto>> ListAsync([Query] Guid? divisionId, CancellationToken cancellationToken);

    [Get("/api/v1/brackets/{id}")]
    Task<BracketWithNodesDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    [Get("/api/v1/brackets/{id}/tree")]
    Task<BracketTreeDto?> GetTreeAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/brackets/generate")]
    Task<BracketDto> GenerateAsync([Body] GenerateBracketDto dto, CancellationToken cancellationToken);
}

public interface ICourtFlowScheduleApi
{
    [Post("/api/v1/tournaments/{tournamentId}/schedule/generate")]
    Task<HttpResponseMessage> GenerateAsync(Guid tournamentId, CancellationToken cancellationToken);

    [Get("/api/v1/tournaments/{tournamentId}/schedule/check")]
    Task<ConflictReportDto> CheckAsync(Guid tournamentId, CancellationToken cancellationToken);
}

public interface ICourtFlowScheduleVersionsApi
{
    [Get("/api/v1/tournaments/{tournamentId}/schedule-versions")]
    Task<List<ScheduleVersionSummaryDto>> ListAsync(Guid tournamentId, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{tournamentId}/schedule-versions/preview-move")]
    Task<PreviewScheduleMoveResponseDto> PreviewMoveAsync(Guid tournamentId, [Body] PreviewScheduleMoveRequestDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{tournamentId}/schedule-versions/apply-move")]
    Task<MatchDto> ApplyMoveAsync(Guid tournamentId, [Body] ApplyScheduleMoveRequestDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{tournamentId}/schedule-versions/publish")]
    Task<ScheduleVersionSummaryDto> PublishAsync(Guid tournamentId, CancellationToken cancellationToken);
}

public interface ICourtFlowCourtStatusApi
{
    [Get("/api/v1/tournaments/{tournamentId}/court-status")]
    Task<List<CourtStatusDto>> GetAsync(Guid tournamentId, CancellationToken cancellationToken);

    [Get("/api/v1/tournaments/{tournamentId}/reflow/preview")]
    Task<ReflowPreviewDto> ReflowPreviewAsync(Guid tournamentId, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{tournamentId}/reflow/apply")]
    Task<ReflowApplyResultDto> ReflowApplyAsync(Guid tournamentId, [Body] ReflowApplyDto dto, CancellationToken cancellationToken);
}

public interface ICourtFlowStandingsApi
{
    [Get("/api/v1/divisions/{divisionId}/standings")]
    Task<DivisionStandingsDto> ForDivisionAsync(Guid divisionId, CancellationToken cancellationToken);
}

public interface ICourtFlowPublicEventsApi
{
    [Get("/api/v1/public/events")]
    Task<List<PublicEventDto>> ListAsync(CancellationToken cancellationToken);

    [Get("/api/v1/public/events/{id}")]
    Task<PublicEventDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    [Get("/api/v1/public/events/{id}/schedule")]
    Task<List<PublicMatchDto>> GetScheduleAsync(Guid id, CancellationToken cancellationToken);

    [Get("/api/v1/public/events/{id}/brackets")]
    Task<List<PublicBracketDto>> GetBracketsAsync(Guid id, CancellationToken cancellationToken);

    [Get("/api/v1/public/events/{id}/standings")]
    Task<List<PublicPoolStandingsDto>> GetStandingsAsync(Guid id, CancellationToken cancellationToken);

    [Get("/api/v1/public/events/past")]
    Task<List<PublicEventDto>> ListPastAsync(CancellationToken cancellationToken);

    [Get("/api/v1/public/events/{id}/readiness")]
    Task<PublicEventReadinessDto?> GetReadinessAsync(Guid id, CancellationToken cancellationToken);

    [Get("/api/v1/public/events/{id}/teams")]
    Task<List<PublicDivisionTeamsDto>> GetTeamsAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICourtFlowWavesApi
{
    [Get("/api/v1/tournaments/{tournamentId}/waves")]
    Task<List<TournamentWaveDto>> ListAsync(Guid tournamentId, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{tournamentId}/waves")]
    Task<TournamentWaveDto> CreateAsync(Guid tournamentId, [Body] CreateTournamentWaveDto dto, CancellationToken cancellationToken);

    [Put("/api/v1/tournaments/{tournamentId}/waves/{id}")]
    Task<TournamentWaveDto> UpdateAsync(Guid tournamentId, Guid id, [Body] UpdateTournamentWaveDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/tournaments/{tournamentId}/waves/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid tournamentId, Guid id, CancellationToken cancellationToken);
}

public interface ICourtFlowRepairsApi
{
    [Get("/api/v1/tournaments/{tournamentId}/repairs")]
    Task<List<RepairJobDto>> ListAsync(Guid tournamentId, CancellationToken cancellationToken);

    [Get("/api/v1/tournaments/{tournamentId}/repairs/{id}")]
    Task<RepairJobDetailDto?> GetAsync(Guid tournamentId, Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{tournamentId}/repairs/team-withdrawal")]
    Task<RepairJobDto> TeamWithdrawalAsync(Guid tournamentId, [Body] TeamWithdrawalDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{tournamentId}/repairs/court-outage")]
    Task<RepairJobDto> CourtOutageAsync(Guid tournamentId, [Body] CourtOutageDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/tournaments/{tournamentId}/repairs/referee-reassignment")]
    Task<RepairJobDto> RefereeReassignmentAsync(Guid tournamentId, [Body] RefereeReassignmentDto dto, CancellationToken cancellationToken);
}

public interface ICourtFlowTemplatesApi
{
    [Get("/api/v1/templates")]
    Task<List<TemplateDto>> ListAsync(CancellationToken cancellationToken);

    [Get("/api/v1/templates/{id}")]
    Task<TemplateDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    [Post("/api/v1/templates/capture")]
    Task<TemplateDto> CaptureAsync([Body] CaptureTemplateDto dto, CancellationToken cancellationToken);

    [Post("/api/v1/templates/{id}/create-tournament")]
    Task<TournamentFromTemplateDto> CreateTournamentAsync(Guid id, [Body] CreateTournamentFromTemplateDto dto, CancellationToken cancellationToken);

    [Delete("/api/v1/templates/{id}")]
    Task<HttpResponseMessage> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
