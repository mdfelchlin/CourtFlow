using System.Net.Http.Json;

namespace CourtFlow.ApiClient;

public class AuthEndpoints
{
    private readonly HttpClient _http;
    public AuthEndpoints(HttpClient http) => _http = http;

    public async Task<SignInResponseDto> SignInAsync(SignInRequestDto req, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/auth/sign-in", req, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SignInResponseDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }
}

public class TournamentEndpoints
{
    private readonly HttpClient _http;
    public TournamentEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<TournamentDto>> ListAsync(CancellationToken ct = default) =>
        _http.GetFromJsonAsync<IReadOnlyList<TournamentDto>>("api/v1/tournaments", CourtFlowApiClient.JsonOptions, ct)!;

    public Task<TournamentDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<TournamentDto?>($"api/v1/tournaments/{id}", CourtFlowApiClient.JsonOptions, ct);

    public async Task<TournamentDto> CreateAsync(CreateTournamentDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/tournaments", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<TournamentDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<TournamentDto> UpdateAsync(UpdateTournamentDto dto, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"api/v1/tournaments/{dto.Id}", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<TournamentDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"api/v1/tournaments/{id}", ct);
        res.EnsureSuccessStatusCode();
    }
}

public class VenueEndpoints
{
    private readonly HttpClient _http;
    public VenueEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<VenueDto>> ListAsync(Guid? tournamentId = null, CancellationToken ct = default)
    {
        var url = tournamentId is null ? "api/v1/venues" : $"api/v1/venues?tournamentId={tournamentId}";
        return _http.GetFromJsonAsync<IReadOnlyList<VenueDto>>(url, CourtFlowApiClient.JsonOptions, ct)!;
    }

    public Task<VenueDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<VenueDto?>($"api/v1/venues/{id}", CourtFlowApiClient.JsonOptions, ct);

    public async Task<VenueDto> CreateAsync(CreateVenueDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/venues", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<VenueDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<VenueDto> UpdateAsync(UpdateVenueDto dto, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"api/v1/venues/{dto.Id}", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<VenueDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"api/v1/venues/{id}", ct);
        res.EnsureSuccessStatusCode();
    }
}

public class CourtEndpoints
{
    private readonly HttpClient _http;
    public CourtEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<CourtDto>> ListAsync(Guid? venueId = null, CancellationToken ct = default)
    {
        var url = venueId is null ? "api/v1/courts" : $"api/v1/courts?venueId={venueId}";
        return _http.GetFromJsonAsync<IReadOnlyList<CourtDto>>(url, CourtFlowApiClient.JsonOptions, ct)!;
    }

    public async Task<CourtDto> CreateAsync(CreateCourtDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/courts", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CourtDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<CourtDto> UpdateAsync(UpdateCourtDto dto, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"api/v1/courts/{dto.Id}", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CourtDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"api/v1/courts/{id}", ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task DelayAsync(Guid courtId, int slots, CancellationToken ct = default)
    {
        var res = await _http.PostAsync($"api/v1/courts/{courtId}/delay?slots={slots}", null, ct);
        res.EnsureSuccessStatusCode();
    }
}

public class DivisionEndpoints
{
    private readonly HttpClient _http;
    public DivisionEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<DivisionDto>> ListAsync(Guid? tournamentId = null, CancellationToken ct = default)
    {
        var url = tournamentId is null ? "api/v1/divisions" : $"api/v1/divisions?tournamentId={tournamentId}";
        return _http.GetFromJsonAsync<IReadOnlyList<DivisionDto>>(url, CourtFlowApiClient.JsonOptions, ct)!;
    }

    public async Task<DivisionDto> CreateAsync(CreateDivisionDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/divisions", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DivisionDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<DivisionDto> UpdateAsync(UpdateDivisionDto dto, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"api/v1/divisions/{dto.Id}", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DivisionDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"api/v1/divisions/{id}", ct);
        res.EnsureSuccessStatusCode();
    }
}

public class PoolEndpoints
{
    private readonly HttpClient _http;
    public PoolEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<PoolDto>> ListAsync(Guid? divisionId = null, CancellationToken ct = default)
    {
        var url = divisionId is null ? "api/v1/pools" : $"api/v1/pools?divisionId={divisionId}";
        return _http.GetFromJsonAsync<IReadOnlyList<PoolDto>>(url, CourtFlowApiClient.JsonOptions, ct)!;
    }

    public async Task<PoolDto> CreateAsync(CreatePoolDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/pools", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PoolDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<PoolDto> UpdateAsync(UpdatePoolDto dto, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"api/v1/pools/{dto.Id}", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PoolDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"api/v1/pools/{id}", ct);
        res.EnsureSuccessStatusCode();
    }
}

public class TeamEndpoints
{
    private readonly HttpClient _http;
    public TeamEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<TeamDto>> ListAsync(Guid? tournamentId = null, Guid? divisionId = null, Guid? poolId = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (tournamentId is { }) qs.Add($"tournamentId={tournamentId}");
        if (divisionId is { }) qs.Add($"divisionId={divisionId}");
        if (poolId is { }) qs.Add($"poolId={poolId}");
        var url = qs.Count == 0 ? "api/v1/teams" : "api/v1/teams?" + string.Join("&", qs);
        return _http.GetFromJsonAsync<IReadOnlyList<TeamDto>>(url, CourtFlowApiClient.JsonOptions, ct)!;
    }

    public async Task<TeamDto> CreateAsync(CreateTeamDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/teams", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<TeamDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<TeamDto> UpdateAsync(UpdateTeamDto dto, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"api/v1/teams/{dto.Id}", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<TeamDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<TeamDto> MoveDivisionAsync(Guid teamId, MoveTeamDto dto, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"api/v1/teams/{teamId}/division", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<TeamDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"api/v1/teams/{id}", ct);
        res.EnsureSuccessStatusCode();
    }
}

public class ImportEndpoints
{
    private readonly HttpClient _http;
    public ImportEndpoints(HttpClient http) => _http = http;

    public async Task<ImportResultDto> ImportPredefinedPoolsAsync(ImportPredefinedPoolsDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/imports/predefined-pools", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ImportResultDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<ImportResultDto> ImportRankedTeamsAsync(ImportRankedTeamsDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/imports/ranked-teams", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ImportResultDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }
}

public class MatchEndpoints
{
    private readonly HttpClient _http;
    public MatchEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<MatchDto>> ListAsync(Guid? tournamentId = null, Guid? divisionId = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (tournamentId is { }) qs.Add($"tournamentId={tournamentId}");
        if (divisionId is { }) qs.Add($"divisionId={divisionId}");
        var url = qs.Count == 0 ? "api/v1/matches" : "api/v1/matches?" + string.Join("&", qs);
        return _http.GetFromJsonAsync<IReadOnlyList<MatchDto>>(url, CourtFlowApiClient.JsonOptions, ct)!;
    }

    public Task<MatchDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<MatchDto?>($"api/v1/matches/{id}", CourtFlowApiClient.JsonOptions, ct);

    public async Task<MatchDto> MoveAsync(Guid id, MoveMatchDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync($"api/v1/matches/{id}/move", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<MatchDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }

    public async Task<MatchDto> ScoreAsync(Guid id, ScoreMatchDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync($"api/v1/matches/{id}/score", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<MatchDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }
}

public class BracketEndpoints
{
    private readonly HttpClient _http;
    public BracketEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<BracketDto>> ListAsync(Guid? divisionId = null, CancellationToken ct = default)
    {
        var url = divisionId is null ? "api/v1/brackets" : $"api/v1/brackets?divisionId={divisionId}";
        return _http.GetFromJsonAsync<IReadOnlyList<BracketDto>>(url, CourtFlowApiClient.JsonOptions, ct)!;
    }

    public Task<BracketWithNodesDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<BracketWithNodesDto?>($"api/v1/brackets/{id}", CourtFlowApiClient.JsonOptions, ct);

    public async Task<BracketDto> GenerateAsync(GenerateBracketDto dto, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/v1/brackets/generate", dto, CourtFlowApiClient.JsonOptions, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<BracketDto>(CourtFlowApiClient.JsonOptions, ct))!;
    }
}

public class ScheduleEndpoints
{
    private readonly HttpClient _http;
    public ScheduleEndpoints(HttpClient http) => _http = http;

    public async Task GenerateAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var res = await _http.PostAsync($"api/v1/tournaments/{tournamentId}/schedule/generate", null, ct);
        res.EnsureSuccessStatusCode();
    }

    public Task<ConflictReportDto> CheckAsync(Guid tournamentId, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<ConflictReportDto>($"api/v1/tournaments/{tournamentId}/schedule/check", CourtFlowApiClient.JsonOptions, ct)!;
}

public class CourtStatusEndpoints
{
    private readonly HttpClient _http;
    public CourtStatusEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<CourtStatusDto>> GetAsync(Guid tournamentId, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<IReadOnlyList<CourtStatusDto>>($"api/v1/tournaments/{tournamentId}/court-status", CourtFlowApiClient.JsonOptions, ct)!;
}

public class StandingsEndpoints
{
    private readonly HttpClient _http;
    public StandingsEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<PoolStandingsDto>> ForDivisionAsync(Guid divisionId, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<IReadOnlyList<PoolStandingsDto>>($"api/v1/divisions/{divisionId}/standings", CourtFlowApiClient.JsonOptions, ct)!;
}

public class PublicEventEndpoints
{
    private readonly HttpClient _http;
    public PublicEventEndpoints(HttpClient http) => _http = http;

    public Task<IReadOnlyList<PublicEventDto>> ListAsync(CancellationToken ct = default) =>
        _http.GetFromJsonAsync<IReadOnlyList<PublicEventDto>>("api/v1/public/events", CourtFlowApiClient.JsonOptions, ct)!;

    public Task<PublicEventDetailDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<PublicEventDetailDto?>($"api/v1/public/events/{id}", CourtFlowApiClient.JsonOptions, ct);

    public Task<IReadOnlyList<PublicMatchDto>?> GetScheduleAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<IReadOnlyList<PublicMatchDto>?>($"api/v1/public/events/{id}/schedule", CourtFlowApiClient.JsonOptions, ct);

    public Task<IReadOnlyList<PublicBracketDto>?> GetBracketsAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<IReadOnlyList<PublicBracketDto>?>($"api/v1/public/events/{id}/brackets", CourtFlowApiClient.JsonOptions, ct);

    public Task<IReadOnlyList<PublicPoolStandingsDto>?> GetStandingsAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<IReadOnlyList<PublicPoolStandingsDto>?>($"api/v1/public/events/{id}/standings", CourtFlowApiClient.JsonOptions, ct);
}
