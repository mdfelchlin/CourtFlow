using System.Net;
using System.Net.Http.Json;

using CourtFlow.Application;

namespace CourtFlow.Api.Tests;

internal static class AccessTestHelpers
{
    private sealed record Refusal(string? Reason);

    private sealed record ApiError(string? Error);

    public static async Task<string?> ReasonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Refusal>())?.Reason;
    }

    public static async Task<string?> ErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiError>())?.Error;
    }

    public static async Task<TournamentDetails> CreateWithChildrenAsync(HttpClient client, string name)
    {
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name));
        (await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Main Gym", "1 Court St", 2))).EnsureSuccessStatusCode();
        var division = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St A", division.Id);
        return tournament;
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object? body = null) =>
        await client.PostAsJsonAsync(path, body ?? new { });
}