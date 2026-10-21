using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using CourtFlow.Application;
using CourtFlow.Domain;

namespace CourtFlow.Api.Tests;

[Collection(ApiFixture.Name)]
public sealed class ArchivedTournamentApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task EveryWriteIntoAnArchivedTournamentIsRejected()
    {
        var client = factory.CreateMemberClient();
        var archived = await ArchivedAsync(client);

        foreach (var (name, send) in Writes(client, archived))
        {
            var response = await send();
            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest,
                $"{name} returned {response.StatusCode} instead of BadRequest.");
            var error = await response.Content.ReadFromJsonAsync<ApiError>(TestContext.Current.CancellationToken);
            Assert.Equal(Tournament.ArchivedMessage, error?.Error);
        }
    }

    [Fact]
    public async Task AnArchivedTournamentKeepsItsVenuesDivisionsAndTeams()
    {
        var client = factory.CreateMemberClient();
        var archived = await ArchivedAsync(client);

        var venues = await client.GetFromJsonAsync<List<VenueDetails>>($"/api/tournaments/{archived.Id}/venues", TestContext.Current.CancellationToken);
        var divisions = await client.GetFromJsonAsync<List<DivisionDetails>>($"/api/tournaments/{archived.Id}/divisions", TestContext.Current.CancellationToken);
        var teams = await TeamApiTests.ListAsync(client, archived.Id);

        Assert.Single(venues!);
        Assert.Single(divisions!);
        Assert.Single(teams);
    }

    [Fact]
    public async Task ARenameThroughAnArchivedTournamentLeavesTheTeamAlone()
    {
        var client = factory.CreateMemberClient();
        var archived = await ArchivedAsync(client);
        var entry = (await TeamApiTests.ListAsync(client, archived.Id)).Single();

        var response = await client.PutAsJsonAsync(
            $"/api/tournament-teams/{entry.Id}",
            new UpdateTournamentTeam("Renamed", entry.DivisionId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(entry.TeamName, (await TeamApiTests.ListAsync(client, archived.Id)).Single().TeamName);
    }

    [Fact]
    public async Task TheSameTeamCanStillBeRenamedThroughAnotherTournament()
    {
        var client = factory.CreateMemberClient();
        var archived = await ArchivedAsync(client);
        var entry = (await TeamApiTests.ListAsync(client, archived.Id)).Single();

        var live = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name: "Live Cup"));
        var division = await TeamApiTests.CreateDivisionAsync(client, live.Id, "14U Open", "Female");
        var liveEntry = await TeamApiTests.AddOkAsync(client, live.Id, entry.ClubName, entry.TeamName, division.Id);

        var response = await client.PutAsJsonAsync(
            $"/api/tournament-teams/{liveEntry.Id}",
            new UpdateTournamentTeam("Renamed", division.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Renamed", (await TeamApiTests.ListAsync(client, archived.Id)).Single().TeamName);
    }

    [Fact]
    public async Task ARestoredTournamentAcceptsWritesAgain()
    {
        var client = factory.CreateMemberClient();
        var archived = await ArchivedAsync(client);

        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "restore");

        var response = await client.PutAsJsonAsync(
            $"/api/tournaments/{archived.Id}",
            TournamentApiTests.Sample(name: "Renamed Cup"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Every write the spec locks, with ids taken from the archived tournament's own children.</summary>
    private static IEnumerable<(string Name, Func<Task<HttpResponseMessage>> Send)> Writes(HttpClient client, ArchivedTournament archived)
    {
        yield return ("save details", () => client.PutAsJsonAsync($"/api/tournaments/{archived.Id}", TournamentApiTests.Sample(name: "Renamed")));
        yield return ("add venue", () => client.PostAsJsonAsync($"/api/tournaments/{archived.Id}/venues", new SaveVenue("New Gym", "", 1)));
        yield return ("edit venue", () => client.PutAsJsonAsync($"/api/venues/{archived.VenueId}", new SaveVenue("Renamed Gym", "", 4)));
        yield return ("delete venue", () => client.DeleteAsync($"/api/venues/{archived.VenueId}"));
        yield return ("add division", () => client.PostAsJsonAsync($"/api/tournaments/{archived.Id}/divisions", new SaveDivision("16U Open", "Male")));
        yield return ("edit division", () => client.PutAsJsonAsync($"/api/divisions/{archived.DivisionId}", new SaveDivision("Renamed", "Female")));
        yield return ("delete division", () => client.DeleteAsync($"/api/divisions/{archived.DivisionId}"));
        yield return ("add team", () => TeamApiTests.AddAsync(client, archived.Id, "New Club", "New Team", archived.DivisionId));
        yield return ("update team", () => client.PutAsJsonAsync($"/api/tournament-teams/{archived.EntryId}", new UpdateTournamentTeam("Renamed", archived.DivisionId)));
        yield return ("delete team", () => client.DeleteAsync($"/api/tournament-teams/{archived.EntryId}"));
        yield return ("import preview", () => PostCsvAsync(client, $"/api/tournaments/{archived.Id}/team-imports/preview"));
        yield return ("import", () => PostCsvAsync(client, $"/api/tournaments/{archived.Id}/team-imports"));
    }

    private static Task<HttpResponseMessage> PostCsvAsync(HttpClient client, string path)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("Club,Team,Division,Gender\nNew Club,New Team,14U Open,F\n"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return client.PostAsync(path, new MultipartFormDataContent { { file, "file", "teams.csv" } });
    }

    private static async Task<ArchivedTournament> ArchivedAsync(HttpClient client)
    {
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var venue = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Main Gym", "", 2));
        venue.EnsureSuccessStatusCode();
        var division = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var entry = await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", division.Id);

        await TournamentLifecycleApiTests.TransitionAsync(client, tournament.Id, "publish");
        await TournamentLifecycleApiTests.TransitionAsync(client, tournament.Id, "archive");

        var saved = (await venue.Content.ReadFromJsonAsync<VenueDetails>())!;
        return new ArchivedTournament(tournament.Id, saved.Id, division.Id, entry.Id);
    }

    private sealed record ArchivedTournament(Guid Id, Guid VenueId, Guid DivisionId, Guid EntryId);

    private sealed record ApiError(string? Error);
}