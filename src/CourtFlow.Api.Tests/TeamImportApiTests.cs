using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CourtFlow.Application;
using CourtFlow.Application.Teams.Import;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class TeamImportApiTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Header = "Club,Team,Division,Gender\n";

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Preview_writes_nothing()
    {
        var client = factory.CreateClient();
        var (tournament, _, _) = await SetupAsync(client);

        var preview = await PreviewAsync(client, tournament, Header + "Ohio State University,Ohio St U,14U Open,F\nKent State,Kent A,14u gold,female\n");

        Assert.Null(preview.FileError);
        Assert.Equal(new TeamImportSummary(2, 2, 2, 2, 0, 0, 0), preview.Summary);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task A_clean_file_writes_clubs_teams_entries_and_moves_in_one_import()
    {
        var client = factory.CreateClient();
        var (tournament, open, gold) = await SetupAsync(client);
        var moving = await TeamApiTests.AddOkAsync(client, tournament, "Purdue University", "Purdue A", open);
        var kept = await TeamApiTests.AddOkAsync(client, tournament, "Kept Club", "Kept", open);

        var response = await ImportAsync(client, tournament,
            Header +
            "Ohio State University,Ohio St U,14U Open,F\n" +
            "OHIO STATE UNIVERSITY,Ohio St B,14U Gold,F\n" +
            "purdue university,purdue a,14U Gold,F\n");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, (await response.Content.ReadFromJsonAsync<TeamImportResult>())!.Imported);
        var listed = await TeamApiTests.ListAsync(client, tournament);
        Assert.Equal(4, listed.Count);
        Assert.Equal(gold, listed.Single(team => team.TeamId == moving.TeamId).DivisionId);
        Assert.Equal(open, listed.Single(team => team.TeamId == kept.TeamId).DivisionId);
        Assert.Single(listed.Where(team => team.ClubName == "Ohio State University").Select(team => team.ClubId).Distinct());
    }

    [Fact]
    public async Task A_file_with_a_rejected_row_returns_the_preview_and_writes_nothing()
    {
        var client = factory.CreateClient();
        var (tournament, _, _) = await SetupAsync(client);

        var response = await ImportAsync(client, tournament, Header + "Ohio State University,Ohio St U,14U Open,F\nKent State,Kent A,Men's Division II,M\n");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var preview = (await response.Content.ReadFromJsonAsync<TeamImportPreview>())!;
        Assert.True(preview.Stale);
        Assert.Equal(1, preview.Summary.Rejected);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Data_that_changed_since_the_preview_returns_a_fresh_preview()
    {
        var client = factory.CreateClient();
        var (tournament, _, gold) = await SetupAsync(client);
        const string csv = Header + "Ohio State University,Ohio St U,14U Open,F\nKent State,Kent A,14U Gold,F\n";
        Assert.Equal(0, (await PreviewAsync(client, tournament, csv)).Summary.Rejected);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/divisions/{gold}")).StatusCode);
        var response = await ImportAsync(client, tournament, csv);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var preview = (await response.Content.ReadFromJsonAsync<TeamImportPreview>())!;
        Assert.True(preview.Stale);
        Assert.Equal(TeamImportOutcome.Rejected, preview.Rows.Single(row => row.Line == 3).Outcome);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task An_oversized_file_gets_the_spec_message()
    {
        var client = factory.CreateClient();
        var (tournament, _, _) = await SetupAsync(client);

        var preview = await PreviewAsync(client, tournament, Header + new string('x', TeamCsvReader.MaxBytes));

        Assert.Equal(TeamCsvReader.TooLarge, preview.FileError);
        Assert.Empty(preview.Rows);
    }

    [Fact]
    public async Task Importing_the_same_file_twice_changes_nothing_the_second_time()
    {
        var client = factory.CreateClient();
        var (tournament, _, _) = await SetupAsync(client);
        const string csv = Header + "Ohio State University,Ohio St U,14U Open,F\nKent State,Kent A,14U Gold,F\n";
        Assert.Equal(HttpStatusCode.OK, (await ImportAsync(client, tournament, csv)).StatusCode);

        var preview = await PreviewAsync(client, tournament, csv);
        Assert.All(preview.Rows, row => Assert.Equal(TeamImportOutcome.NoChange, row.Outcome));

        Assert.Equal(HttpStatusCode.OK, (await ImportAsync(client, tournament, csv)).StatusCode);
        Assert.Equal(2, (await TeamApiTests.ListAsync(client, tournament)).Count);
    }

    [Fact]
    public async Task A_missing_tournament_is_not_found()
    {
        var client = factory.CreateClient();
        var missing = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await Post(client, $"/api/tournaments/{missing}/team-imports/preview", Header)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ImportAsync(client, missing, Header)).StatusCode);
    }

    private static async Task<(Guid Tournament, Guid Open, Guid Gold)> SetupAsync(HttpClient client)
    {
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var open = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var gold = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Gold", "Female");
        return (tournament.Id, open.Id, gold.Id);
    }

    private static async Task<TeamImportPreview> PreviewAsync(HttpClient client, Guid tournamentId, string csv)
    {
        var response = await Post(client, $"/api/tournaments/{tournamentId}/team-imports/preview", csv);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TeamImportPreview>())!;
    }

    private static Task<HttpResponseMessage> ImportAsync(HttpClient client, Guid tournamentId, string csv) =>
        Post(client, $"/api/tournaments/{tournamentId}/team-imports", csv);

    private static Task<HttpResponseMessage> Post(HttpClient client, string path, string csv)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        var form = new MultipartFormDataContent { { file, "file", "teams.csv" } };
        return client.PostAsync(path, form);
    }

    private async Task<int> CountAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        return await db.Clubs.CountAsync() + await db.Teams.CountAsync() + await db.TournamentTeams.CountAsync();
    }
}
