using System.Net;
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
        response.EnsureSuccessStatusCode();
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
        response.EnsureSuccessStatusCode();
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
