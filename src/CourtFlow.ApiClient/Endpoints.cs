using System.Net.Http;
using System.Text.Json;

namespace CourtFlow.ApiClient;

file static class ReadOnlyAdapt
{
    public static Task<IReadOnlyList<T>> From<T>(Task<List<T>> task) => Map(task);

    private static async Task<IReadOnlyList<T>> Map<T>(Task<List<T>> task) => await task.ConfigureAwait(false);
}

public sealed class TournamentEndpoints(ICourtFlowTournamentsApi api)
{
    private readonly ICourtFlowTournamentsApi _api = api;

    public Task<IReadOnlyList<TournamentDto>> ListAsync(CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(ct));

    public Task<TournamentDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _api.GetAsync(id, ct);

    public Task<TournamentDto> CreateAsync(CreateTournamentDto dto, CancellationToken ct = default)
        => _api.CreateAsync(dto, ct);

    public Task<TournamentDto> UpdateAsync(UpdateTournamentDto dto, CancellationToken ct = default)
        => _api.UpdateAsync(dto.Id, dto, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(id, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public Task<TournamentDto> PublishAsync(Guid id, CancellationToken ct = default)
        => _api.PublishAsync(id, ct);

    public Task<TournamentDto> UnpublishAsync(Guid id, CancellationToken ct = default)
        => _api.UnpublishAsync(id, ct);

    public Task<TournamentDto> ArchiveAsync(Guid id, CancellationToken ct = default)
        => _api.ArchiveAsync(id, ct);

    public Task<TournamentDto> RestoreAsync(Guid id, CancellationToken ct = default)
        => _api.RestoreAsync(id, ct);

    public Task<TournamentDto> CloneAsync(Guid id, CancellationToken ct = default)
        => _api.CloneAsync(id, ct);
}

public sealed class VenueEndpoints(ICourtFlowVenuesApi api)
{
    private readonly ICourtFlowVenuesApi _api = api;

    public Task<IReadOnlyList<VenueDto>> ListAsync(Guid? tournamentId = null, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(tournamentId, ct));

    public Task<VenueDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _api.GetAsync(id, ct);

    public Task<VenueDto> CreateAsync(CreateVenueDto dto, CancellationToken ct = default)
        => _api.CreateAsync(dto, ct);

    public Task<VenueDto> UpdateAsync(UpdateVenueDto dto, CancellationToken ct = default)
        => _api.UpdateAsync(dto.Id, dto, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(id, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }
}

public sealed class CourtEndpoints(ICourtFlowCourtsApi api)
{
    private readonly ICourtFlowCourtsApi _api = api;

    public Task<IReadOnlyList<CourtDto>> ListAsync(Guid? venueId = null, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(venueId, ct));

    public Task<CourtDto> CreateAsync(CreateCourtDto dto, CancellationToken ct = default)
        => _api.CreateAsync(dto, ct);

    public Task<CourtDto> UpdateAsync(UpdateCourtDto dto, CancellationToken ct = default)
        => _api.UpdateAsync(dto.Id, dto, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(id, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public async Task DelayAsync(Guid courtId, int slots, CancellationToken ct = default)
    {
        var res = await _api.DelayCourtAsync(courtId, slots, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }
}

public sealed class DivisionEndpoints(ICourtFlowDivisionsApi api)
{
    private readonly ICourtFlowDivisionsApi _api = api;

    public Task<IReadOnlyList<DivisionDto>> ListAsync(Guid? tournamentId = null, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(tournamentId, ct));

    public Task<DivisionDto> CreateAsync(CreateDivisionDto dto, CancellationToken ct = default)
        => _api.CreateAsync(dto, ct);

    public Task<DivisionDto> UpdateAsync(UpdateDivisionDto dto, CancellationToken ct = default)
        => _api.UpdateAsync(dto.Id, dto, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(id, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<TeamDto>> ReorderTeamsAsync(Guid divisionId, ReorderDivisionTeamsDto dto, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ReorderTeamsAsync(divisionId, dto, ct));

    public Task<IReadOnlyList<TeamDto>> RandomizeSeedsAsync(Guid divisionId, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.RandomizeSeedsAsync(divisionId, ct));

    public Task<ApplyDivisionSeedingResultDto> ApplySeedingAsync(Guid divisionId, ApplyDivisionSeedingDto dto, CancellationToken ct = default)
        => _api.ApplySeedingAsync(divisionId, dto, ct);

    public Task<GenerateAdvancementBracketsResultDto> GenerateAdvancementBracketsAsync(Guid divisionId, GenerateAdvancementBracketsDto dto, CancellationToken ct = default)
        => _api.GenerateAdvancementBracketsAsync(divisionId, dto, ct);
}

public sealed class PoolEndpoints(ICourtFlowPoolsApi api)
{
    private readonly ICourtFlowPoolsApi _api = api;

    public Task<IReadOnlyList<PoolDto>> ListAsync(Guid? divisionId = null, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(divisionId, ct));

    public Task<PoolDto> CreateAsync(CreatePoolDto dto, CancellationToken ct = default)
        => _api.CreateAsync(dto, ct);

    public Task<PoolDto> UpdateAsync(UpdatePoolDto dto, CancellationToken ct = default)
        => _api.UpdateAsync(dto.Id, dto, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(id, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }
}

public sealed class TeamEndpoints(ICourtFlowTeamsApi api)
{
    private readonly ICourtFlowTeamsApi _api = api;

    public Task<IReadOnlyList<TeamDto>> ListAsync(Guid? tournamentId = null, Guid? divisionId = null, Guid? poolId = null, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(tournamentId, divisionId, poolId, ct));

    public Task<TeamDto> CreateAsync(CreateTeamDto dto, CancellationToken ct = default)
        => _api.CreateAsync(dto, ct);

    public Task<TeamDto> UpdateAsync(UpdateTeamDto dto, CancellationToken ct = default)
        => _api.UpdateAsync(dto.Id, dto, ct);

    public Task<TeamDto> MoveDivisionAsync(Guid teamId, MoveTeamDto dto, CancellationToken ct = default)
        => _api.MoveDivisionAsync(teamId, dto, ct);

    public Task<TeamDto> AssignPoolAsync(Guid teamId, AssignTeamPoolDto dto, CancellationToken ct = default)
        => _api.AssignPoolAsync(teamId, dto, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(id, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }
}

public sealed class ImportEndpoints(ICourtFlowImportsApi api)
{
    private readonly ICourtFlowImportsApi _api = api;

    public Task<ImportResultDto> ImportPredefinedPoolsAsync(ImportPredefinedPoolsDto dto, CancellationToken ct = default)
        => _api.ImportPredefinedPoolsAsync(dto, ct);

    public Task<ImportResultDto> ImportRankedTeamsAsync(ImportRankedTeamsDto dto, CancellationToken ct = default)
        => _api.ImportRankedTeamsAsync(dto, ct);
}

public sealed class MatchEndpoints(ICourtFlowMatchesApi api)
{
    private readonly ICourtFlowMatchesApi _api = api;

    public Task<IReadOnlyList<MatchDto>> ListAsync(Guid? tournamentId = null, Guid? divisionId = null, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(tournamentId, divisionId, ct));

    public Task<MatchDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _api.GetAsync(id, ct);

    public Task<MatchDto> MoveAsync(Guid id, MoveMatchDto dto, CancellationToken ct = default)
        => _api.MoveAsync(id, dto, ct);

    public Task<MatchDto> ScoreAsync(Guid id, ScoreMatchDto dto, CancellationToken ct = default)
        => _api.ScoreAsync(id, dto, ct);

    public Task<MatchDto> ForfeitAsync(Guid id, ForfeitMatchDto dto, CancellationToken ct = default)
        => _api.ForfeitAsync(id, dto, ct);
}

public sealed class BracketEndpoints(ICourtFlowBracketsApi api)
{
    private readonly ICourtFlowBracketsApi _api = api;

    public Task<IReadOnlyList<BracketDto>> ListAsync(Guid? divisionId = null, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(divisionId, ct));

    public Task<BracketWithNodesDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _api.GetAsync(id, ct);

    public Task<BracketDto> GenerateAsync(GenerateBracketDto dto, CancellationToken ct = default)
        => _api.GenerateAsync(dto, ct);
}

public sealed class ScheduleEndpoints(ICourtFlowScheduleApi api)
{
    private readonly ICourtFlowScheduleApi _api = api;

    public async Task GenerateAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var res = await _api.GenerateAsync(tournamentId, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public Task<ConflictReportDto> CheckAsync(Guid tournamentId, CancellationToken ct = default)
        => _api.CheckAsync(tournamentId, ct);
}

public sealed class ScheduleVersionsEndpoints(ICourtFlowScheduleVersionsApi api)
{
    private readonly ICourtFlowScheduleVersionsApi _api = api;

    public Task<IReadOnlyList<ScheduleVersionSummaryDto>> ListAsync(Guid tournamentId, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(tournamentId, ct));

    public Task<PreviewScheduleMoveResponseDto> PreviewMoveAsync(Guid tournamentId, PreviewScheduleMoveRequestDto dto, CancellationToken ct = default)
        => _api.PreviewMoveAsync(tournamentId, dto, ct);

    public Task<MatchDto> ApplyMoveAsync(Guid tournamentId, ApplyScheduleMoveRequestDto dto, CancellationToken ct = default)
        => _api.ApplyMoveAsync(tournamentId, dto, ct);
}

public sealed class CourtStatusEndpoints(ICourtFlowCourtStatusApi api)
{
    private readonly ICourtFlowCourtStatusApi _api = api;

    public Task<IReadOnlyList<CourtStatusDto>> GetAsync(Guid tournamentId, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.GetAsync(tournamentId, ct));
}

public sealed class StandingsEndpoints(ICourtFlowStandingsApi api)
{
    private readonly ICourtFlowStandingsApi _api = api;

    public Task<IReadOnlyList<PoolStandingsDto>> ForDivisionAsync(Guid divisionId, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ForDivisionAsync(divisionId, ct));
}

public sealed class PublicEventEndpoints(ICourtFlowPublicEventsApi api)
{
    private readonly ICourtFlowPublicEventsApi _api = api;

    public Task<IReadOnlyList<PublicEventDto>> ListAsync(CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(ct));

    public Task<PublicEventDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _api.GetAsync(id, ct);

    public async Task<IReadOnlyList<PublicMatchDto>?> GetScheduleAsync(Guid id, CancellationToken ct = default)
        => await _api.GetScheduleAsync(id, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PublicBracketDto>?> GetBracketsAsync(Guid id, CancellationToken ct = default)
        => await _api.GetBracketsAsync(id, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PublicPoolStandingsDto>?> GetStandingsAsync(Guid id, CancellationToken ct = default)
        => await _api.GetStandingsAsync(id, ct).ConfigureAwait(false);
}

public sealed class RepairsEndpoints(ICourtFlowRepairsApi api)
{
    private readonly ICourtFlowRepairsApi _api = api;

    public Task<IReadOnlyList<RepairJobDto>> ListAsync(Guid tournamentId, CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(tournamentId, ct));

    public Task<RepairJobDetailDto?> GetAsync(Guid tournamentId, Guid id, CancellationToken ct = default)
        => _api.GetAsync(tournamentId, id, ct);

    public Task<RepairJobDto> TeamWithdrawalAsync(Guid tournamentId, TeamWithdrawalDto dto, CancellationToken ct = default)
        => _api.TeamWithdrawalAsync(tournamentId, dto, ct);

    public Task<RepairJobDto> CourtOutageAsync(Guid tournamentId, CourtOutageDto dto, CancellationToken ct = default)
        => _api.CourtOutageAsync(tournamentId, dto, ct);
}

public sealed class TemplatesEndpoints(ICourtFlowTemplatesApi api)
{
    private readonly ICourtFlowTemplatesApi _api = api;

    public Task<IReadOnlyList<TemplateDto>> ListAsync(CancellationToken ct = default)
        => ReadOnlyAdapt.From(_api.ListAsync(ct));

    public Task<TemplateDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _api.GetAsync(id, ct);

    public Task<TemplateDto> CaptureAsync(CaptureTemplateDto dto, CancellationToken ct = default)
        => _api.CaptureAsync(dto, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => _api.DeleteAsync(id, ct);
}
