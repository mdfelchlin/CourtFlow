using System.Net;
using System.Net.Http.Json;

using CourtFlow.Application;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiFixture.Name)]
public sealed class DivisionApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ListIsOrderedByNameThenFemaleBeforeMale()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());

        await CreateAsync(client, tournament.Id, "14U Open", "Male");
        await CreateAsync(client, tournament.Id, "12U", "Male");
        await CreateAsync(client, tournament.Id, "14U Open", "Female");
        await CreateAsync(client, tournament.Id, "12u", "Female");

        var listed = await client.GetFromJsonAsync<List<DivisionDetails>>($"/api/tournaments/{tournament.Id}/divisions", TestContext.Current.CancellationToken);
        Assert.Equal(
            [("12u", "Female"), ("12U", "Male"), ("14U Open", "Female"), ("14U Open", "Male")],
            listed!.Select(division => (division.Name, division.Gender)).ToArray());
    }

    [Fact]
    public async Task CreateTrimsTheNameAndReturnsAnId()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());

        var created = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/divisions",
            new SaveDivision("  14U Open  ", "Female"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var division = (await created.Content.ReadFromJsonAsync<DivisionDetails>(TestContext.Current.CancellationToken))!;
        Assert.NotEqual(Guid.Empty, division.Id);
        Assert.Equal("14U Open", division.Name);
        Assert.Equal("Female", division.Gender);
        Assert.Equal(tournament.Id, division.TournamentId);
    }

    [Fact]
    public async Task InvalidNameAndGenderAreRejectedWithTheSpecMessages()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var path = $"/api/tournaments/{tournament.Id}/divisions";

        var blank = await client.PostAsJsonAsync(path, new SaveDivision("   ", "Female"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(Division.NameRequired, await ErrorAsync(blank));

        var missing = await client.PostAsJsonAsync(path, new { name = "14U Open" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(Division.GenderRequired, await ErrorAsync(missing));

        var other = await client.PostAsJsonAsync(path, new SaveDivision("14U Open", "Coed"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        Assert.Equal(Division.GenderRequired, await ErrorAsync(other));
    }

    [Fact]
    public async Task ARepeatedNameAndGenderIsRejectedAndAnotherTournamentMayReuseIt()
    {
        var client = factory.CreateMemberClient();
        var first = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var second = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name: "Other Cup"));
        await CreateAsync(client, first.Id, "14U Open", "Female");

        var duplicate = await client.PostAsJsonAsync(
            $"/api/tournaments/{first.Id}/divisions",
            new SaveDivision(" 14u open ", "Female"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(Division.DuplicateNameAndGender, await ErrorAsync(duplicate));

        var reused = await client.PostAsJsonAsync(
            $"/api/tournaments/{second.Id}/divisions",
            new SaveDivision("14U Open", "Female"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, reused.StatusCode);
    }

    [Fact]
    public async Task UpdatingADivisionDoesNotConflictWithItself()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var female = await CreateAsync(client, tournament.Id, "14U Open", "Female");
        await CreateAsync(client, tournament.Id, "14U Open", "Male");

        var same = await client.PutAsJsonAsync($"/api/divisions/{female.Id}", new SaveDivision("14U Open", "Female"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);

        var conflict = await client.PutAsJsonAsync($"/api/divisions/{female.Id}", new SaveDivision("14U Open", "Male"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.Equal(Division.DuplicateNameAndGender, await ErrorAsync(conflict));
    }

    [Fact]
    public async Task MissingTournamentOrDivisionIsNotFound()
    {
        var client = factory.CreateMemberClient();
        var missingTournament = Guid.NewGuid();
        var missingDivision = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/tournaments/{missingTournament}/divisions",
            new SaveDivision("14U Open", "Female"),
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"/api/divisions/{missingDivision}",
            new SaveDivision("14U Open", "Female"),
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/divisions/{missingDivision}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task DeletingADivisionLeavesTheTournamentAndTheOtherDivision()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var female = await CreateAsync(client, tournament.Id, "14U Open", "Female");
        var male = await CreateAsync(client, tournament.Id, "14U Open", "Male");

        var deleted = await client.DeleteAsync($"/api/divisions/{female.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var stillThere = await client.GetFromJsonAsync<TournamentDetails>($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(tournament.Id, stillThere!.Id);
        var listed = await client.GetFromJsonAsync<List<DivisionDetails>>($"/api/tournaments/{tournament.Id}/divisions", TestContext.Current.CancellationToken);
        Assert.Equal([male.Id], listed!.Select(division => division.Id).ToArray());
    }

    [Fact]
    public async Task DeletingADivisionWithTeamsIsRejectedAndKeepsIt()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await CreateAsync(client, tournament.Id, "14U Open", "Female");
        for (var i = 1; i <= 3; i++)
            await TeamApiTests.AddOkAsync(client, tournament.Id, "Club", $"Team {i}", division.Id);

        var rejected = await client.DeleteAsync($"/api/divisions/{division.Id}", TestContext.Current.CancellationToken);

        Assert.Equal("This division has 3 teams. Move or remove them on the Teams page first.", await TeamApiTests.RejectionAsync(rejected));
        var listed = await client.GetFromJsonAsync<List<DivisionDetails>>($"/api/tournaments/{tournament.Id}/divisions", TestContext.Current.CancellationToken);
        Assert.Equal([division.Id], listed!.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task DeletingATournamentDeletesItsDivisions()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await CreateAsync(client, tournament.Id, "14U Open", "Female");

        var deleted = await client.DeleteAsync($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.False(await db.Divisions.IgnoreQueryFilters().AnyAsync(item => item.Id == division.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnotherOrganizationsDivisionCannotBeChanged()
    {
        var other = await InsertOtherDivisionAsync();
        var client = factory.CreateMemberClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{other.TournamentId}/divisions", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"/api/divisions/{other.DivisionId}",
            new SaveDivision("Hijack", "Male"),
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/divisions/{other.DivisionId}", TestContext.Current.CancellationToken)).StatusCode);

        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var stillThere = await db.Divisions.IgnoreQueryFilters().SingleAsync(division => division.Id == other.DivisionId, TestContext.Current.CancellationToken);
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
        using var scope = factory.CreateTestOrganizationScope();
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