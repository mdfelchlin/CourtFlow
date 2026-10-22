using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using CourtFlow.Domain;

namespace CourtFlow.Web.Services;

public sealed class CourtFlowApi(HttpClient http) : ICourtFlowApi
{
    public async Task<IReadOnlyList<TournamentResponse>> ListTournamentsAsync(CancellationToken cancellationToken = default)
    {
        var items = await http.GetFromJsonAsync<List<TournamentResponse>>("api/tournaments", cancellationToken);
        return items ?? [];
    }

    public async Task<TournamentResponse?> GetTournamentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/tournaments/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TournamentResponse>(cancellationToken);
    }

    public async Task<TournamentResponse> CreateTournamentAsync(TournamentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/tournaments", request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentResponse>(cancellationToken))!;
    }

    public async Task<TournamentResponse> UpdateTournamentAsync(Guid id, TournamentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync($"api/tournaments/{id}", request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentResponse>(cancellationToken))!;
    }

    public async Task DeleteTournamentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/tournaments/{id}", cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
    }

    public Task<TournamentResponse> PublishTournamentAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, "publish", cancellationToken);

    public Task<TournamentResponse> UnpublishTournamentAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, "unpublish", cancellationToken);

    public Task<TournamentResponse> ArchiveTournamentAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, "archive", cancellationToken);

    public Task<TournamentResponse> RestoreTournamentAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, "restore", cancellationToken);

    private async Task<TournamentResponse> TransitionAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        var response = await http.PostAsync($"api/tournaments/{id}/{action}", null, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentResponse>(cancellationToken))!;
    }

    public async Task<IReadOnlyList<VenueResponse>> ListVenuesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var items = await http.GetFromJsonAsync<List<VenueResponse>>($"api/tournaments/{tournamentId}/venues", cancellationToken);
        return items ?? [];
    }

    public async Task<VenueResponse> CreateVenueAsync(Guid tournamentId, VenueRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"api/tournaments/{tournamentId}/venues", request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<VenueResponse>(cancellationToken))!;
    }

    public async Task<VenueResponse> UpdateVenueAsync(Guid venueId, VenueRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync($"api/venues/{venueId}", request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<VenueResponse>(cancellationToken))!;
    }

    public async Task DeleteVenueAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/venues/{venueId}", cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
    }

    public async Task<IReadOnlyList<DivisionResponse>> ListDivisionsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var items = await http.GetFromJsonAsync<List<DivisionResponse>>($"api/tournaments/{tournamentId}/divisions", cancellationToken);
        return items ?? [];
    }

    public async Task<DivisionResponse> CreateDivisionAsync(Guid tournamentId, DivisionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"api/tournaments/{tournamentId}/divisions", request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DivisionResponse>(cancellationToken))!;
    }

    public async Task<DivisionResponse> UpdateDivisionAsync(Guid divisionId, DivisionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync($"api/divisions/{divisionId}", request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DivisionResponse>(cancellationToken))!;
    }

    public async Task DeleteDivisionAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/divisions/{divisionId}", cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
    }

    public async Task<IReadOnlyList<TournamentTeamResponse>> ListTournamentTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var items = await http.GetFromJsonAsync<List<TournamentTeamResponse>>($"api/tournaments/{tournamentId}/teams", cancellationToken);
        return items ?? [];
    }

    public async Task<TournamentTeamResponse> AddTournamentTeamAsync(Guid tournamentId, TournamentTeamRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"api/tournaments/{tournamentId}/teams", request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentTeamResponse>(cancellationToken))!;
    }

    public async Task<TournamentTeamResponse> UpdateTournamentTeamAsync(Guid entryId, TournamentTeamUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync($"api/tournament-teams/{entryId}", request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentTeamResponse>(cancellationToken))!;
    }

    public async Task DeleteTournamentTeamAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"api/tournament-teams/{entryId}", cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ClubResponse>> SearchClubsAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(search) ? "" : $"?search={Uri.EscapeDataString(search)}";
        var items = await http.GetFromJsonAsync<List<ClubResponse>>($"api/clubs{query}", cancellationToken);
        return items ?? [];
    }

    public async Task<IReadOnlyList<ClubTeamOptionResponse>> ListClubTeamsAsync(Guid clubId, Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var items = await http.GetFromJsonAsync<List<ClubTeamOptionResponse>>($"api/clubs/{clubId}/teams?tournamentId={tournamentId}", cancellationToken);
        return items ?? [];
    }

    public async Task<TeamImportPreviewResponse> PreviewTeamImportAsync(Guid tournamentId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/tournaments/{tournamentId}/team-imports/preview", Upload(fileName, content), cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TeamImportPreviewResponse>(cancellationToken))!;
    }

    public async Task<IReadOnlyList<FormatDivisionResponse>?> ListFormatAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/tournaments/{tournamentId}/format", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FormatDivisionResponse>>(cancellationToken) ?? [];
    }

    public async Task<DivisionRankingResponse?> GetRankingAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/divisions/{divisionId}/ranking", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DivisionRankingResponse>(cancellationToken);
    }

    public async Task<DivisionRankingResponse> SaveRankingAsync(Guid divisionId, IReadOnlyList<Guid> entryIds, CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync($"api/divisions/{divisionId}/ranking", new SaveRankingRequest(entryIds), cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DivisionRankingResponse>(cancellationToken))!;
    }

    public async Task<TeamImportResponse> ImportTeamsAsync(Guid tournamentId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"api/tournaments/{tournamentId}/team-imports", Upload(fileName, content), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
            return new TeamImportResponse(null, await response.Content.ReadFromJsonAsync<TeamImportPreviewResponse>(cancellationToken));

        await ThrowIfRejected(response, cancellationToken);
        return new TeamImportResponse(await response.Content.ReadFromJsonAsync<TeamImportResultResponse>(cancellationToken), null);
    }

    public async Task<AccessRequestResultResponse> SubmitAccessRequestAsync(AccessRequestForm form, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/access-requests", form, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<AccessRequestResultResponse>(cancellationToken))!;
    }

    public async Task<OrganizationOverviewResponse> GetOrganizationAsync(CancellationToken cancellationToken = default) =>
        (await http.GetFromJsonAsync<OrganizationOverviewResponse>("api/organization", cancellationToken))!;

    public Task InviteMemberAsync(string email, OrganizationRole role, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/organization/invitations", new { email, role }, cancellationToken);

    public Task ResendInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/organization/invitations/{invitationId}/resend", null, cancellationToken);

    public Task CancelInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/organization/invitations/{invitationId}", null, cancellationToken);

    public Task ChangeMemberRoleAsync(Guid membershipId, OrganizationRole role, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"api/organization/members/{membershipId}/role", new { role }, cancellationToken);

    public Task RemoveMemberAsync(Guid membershipId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/organization/members/{membershipId}", null, cancellationToken);

    public async Task<IReadOnlyList<AccessRequestResponse>> ListAccessRequestsAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<List<AccessRequestResponse>>("api/operator/requests", cancellationToken) ?? [];

    public Task ApproveAccessRequestAsync(Guid id, ApproveAccessRequestRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/operator/requests/{id}/approve", request, cancellationToken);

    public Task DeclineAccessRequestAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/operator/requests/{id}/decline", null, cancellationToken);

    public async Task<IReadOnlyList<OperatorOrganizationResponse>> ListOrganizationsAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<List<OperatorOrganizationResponse>>("api/operator/organizations", cancellationToken) ?? [];

    public Task InviteOwnerAsync(Guid organizationId, string email, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/operator/organizations/{organizationId}/owner-invitations", new { email }, cancellationToken);

    public Task DeactivateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/operator/organizations/{organizationId}/deactivate", null, cancellationToken);

    public Task ReactivateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/operator/organizations/{organizationId}/reactivate", null, cancellationToken);

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        var response = await http.SendAsync(request, cancellationToken);
        await ThrowIfRejected(response, cancellationToken);
    }

    private static MultipartFormDataContent Upload(string fileName, byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static async Task ThrowIfRejected(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
        throw new DomainRuleException(error?.Error ?? "Save was rejected.");
    }

    private sealed record ApiError(string? Error);
}