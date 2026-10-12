using System.Net;
using System.Net.Http.Json;
using CourtFlow.Application;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DivisionApiTests(ApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task List_is_ordered_by_name_then_female_before_male()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());

        await CreateAsync(client, tournament.Id, "14U Open", "Male");
        await CreateAsync(client, tournament.Id, "12U", "Male");
        await CreateAsync(client, tournament.Id, "14U Open", "Female");
        await CreateAsync(client, tournament.Id, "12u", "Female");

        var listed = await client.GetFromJsonAsync<List<DivisionDetails>>($"/api/tournaments/{tournament.Id}/divisions");
        Assert.Equal(
            [("12u", "Female"), ("12U", "Male"), ("14U Open", "Female"), ("14U Open", "Male")],
            listed!.Select(division => (division.Name, division.Gender)).ToArray());
    }

    [Fact]
    public async Task Create_trims_the_name_and_returns_an_id()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());

        var created = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/divisions",
            new SaveDivision("  14U Open  ", "Female"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var division = (await created.Content.ReadFromJsonAsync<DivisionDetails>())!;
        Assert.NotEqual(Guid.Empty, division.Id);
        Assert.Equal("14U Open", division.Name);
        Assert.Equal("Female", division.Gender);
        Assert.Equal(tournament.Id, division.TournamentId);
    }

    [Fact]
    public async Task Invalid_name_and_gender_are_rejected_with_the_spec_messages()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var path = $"/api/tournaments/{tournament.Id}/divisions";

        var blank = await client.PostAsJsonAsync(path, new SaveDivision("   ", "Female"));
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(Division.NameRequired, await ErrorAsync(blank));

        var missing = await client.PostAsJsonAsync(path, new { name = "14U Open" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(Division.GenderRequired, await ErrorAsync(missing));

        var other = await client.PostAsJsonAsync(path, new SaveDivision("14U Open", "Coed"));
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        Assert.Equal(Division.GenderRequired, await ErrorAsync(other));
    }

    [Fact]
    public async Task A_repeated_name_and_gender_is_rejected_and_another_tournament_may_reuse_it()
    {
        var client = factory.CreateClient();
        var first = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var second = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name: "Other Cup"));
        await CreateAsync(client, first.Id, "14U Open", "Female");

        var duplicate = await client.PostAsJsonAsync(
            $"/api/tournaments/{first.Id}/divisions",
            new SaveDivision(" 14u open ", "Female"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(Division.DuplicateNameAndGender, await ErrorAsync(duplicate));

        var reused = await client.PostAsJsonAsync(
            $"/api/tournaments/{second.Id}/divisions",
            new SaveDivision("14U Open", "Female"));
        Assert.Equal(HttpStatusCode.Created, reused.StatusCode);
    }

    [Fact]
    public async Task Updating_a_division_does_not_conflict_with_itself()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var female = await CreateAsync(client, tournament.Id, "14U Open", "Female");
        await CreateAsync(client, tournament.Id, "14U Open", "Male");

        var same = await client.PutAsJsonAsync($"/api/divisions/{female.Id}", new SaveDivision("14U Open", "Female"));
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);

        var conflict = await client.PutAsJsonAsync($"/api/divisions/{female.Id}", new SaveDivision("14U Open", "Male"));
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.Equal(Division.DuplicateNameAndGender, await ErrorAsync(conflict));
    }

    [Fact]
    public async Task Missing_tournament_or_division_is_not_found()
    {
        var client = factory.CreateClient();
        var missingTournament = Guid.NewGuid();
        var missingDivision = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/tournaments/{missingTournament}/divisions",
            new SaveDivision("14U Open", "Female"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"/api/divisions/{missingDivision}",
            new SaveDivision("14U Open", "Female"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/divisions/{missingDivision}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_division_leaves_the_tournament_and_the_other_division()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var female = await CreateAsync(client, tournament.Id, "14U Open", "Female");
        var male = await CreateAsync(client, tournament.Id, "14U Open", "Male");

        var deleted = await client.DeleteAsync($"/api/divisions/{female.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var stillThere = await client.GetFromJsonAsync<TournamentDetails>($"/api/tournaments/{tournament.Id}");
        Assert.Equal(tournament.Id, stillThere!.Id);
        var listed = await client.GetFromJsonAsync<List<DivisionDetails>>($"/api/tournaments/{tournament.Id}/divisions");
        Assert.Equal([male.Id], listed!.Select(division => division.Id).ToArray());
    }

    [Fact]
    public async Task Deleting_a_division_with_teams_is_rejected_and_keeps_it()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await CreateAsync(client, tournament.Id, "14U Open", "Female");
        for (var i = 1; i <= 3; i++)
            await TeamApiTests.AddOkAsync(client, tournament.Id, "Club", $"Team {i}", division.Id);

        var rejected = await client.DeleteAsync($"/api/divisions/{division.Id}");

        Assert.Equal("This division has 3 teams. Move or remove them on the Teams page first.", await TeamApiTests.RejectionAsync(rejected));
        var listed = await client.GetFromJsonAsync<List<DivisionDetails>>($"/api/tournaments/{tournament.Id}/divisions");
        Assert.Equal([division.Id], listed!.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task Deleting_a_tournament_deletes_its_divisions()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await CreateAsync(client, tournament.Id, "14U Open", "Female");

        var deleted = await client.DeleteAsync($"/api/tournaments/{tournament.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.False(await db.Divisions.IgnoreQueryFilters().AnyAsync(item => item.Id == division.Id));
    }

    [Fact]
    public async Task Another_organizations_division_cannot_be_changed()
    {
        var other = await InsertOtherDivisionAsync();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{other.TournamentId}/divisions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"/api/divisions/{other.DivisionId}",
            new SaveDivision("Hijack", "Male"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/divisions/{other.DivisionId}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var stillThere = await db.Divisions.IgnoreQueryFilters().SingleAsync(division => division.Id == other.DivisionId);
        Assert.Equal("14U Open", stillThere.Name);
        Assert.Equal(DivisionGender.Female, stillThere.Gender);
    }

    private static async Task<DivisionDetails> CreateAsync(HttpClient client, Guid tournamentId, string name, string gender)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/divisions",
            new SaveDivision(name, gender));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DivisionDetails>())!;
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiError>())?.Error;

    private async Task<(Guid TournamentId, Guid DivisionId)> InsertOtherDivisionAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new Organization(organizationId, "Other Organization"));
        var tournament = Tournament.Create(
            organizationId,
            "Secret Cup",
            null,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3),
            "America/Denver",
            45);
        var division = tournament.AddDivision("14U Open", "Female");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();
        return (tournament.Id, division.Id);
    }

    private sealed record ApiError(string? Error);
}
