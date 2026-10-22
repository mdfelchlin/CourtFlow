using System.Net;
using System.Net.Http.Json;

using CourtFlow.Application;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiFixture.Name)]
public sealed class TournamentApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ListReturnsTheSavedFieldsForTheTestOrganization()
    {
        var client = factory.CreateMemberClient();
        var created = await CreateAsync(client, Sample());

        var list = await client.GetFromJsonAsync<List<TournamentDetails>>("/api/tournaments", TestContext.Current.CancellationToken);

        var item = Assert.Single(list!);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal("City Cup", item.Name);
        Assert.Equal(new DateOnly(2026, 9, 1), item.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 3), item.EndDate);
        Assert.Equal("America/Chicago", item.TimeZoneId);
        Assert.Equal(60, item.DefaultMatchDurationMinutes);
    }

    [Fact]
    public async Task CreateGetAndUpdateRoundTrip()
    {
        var client = factory.CreateMemberClient();
        var created = await CreateAsync(client, Sample());

        var loaded = await client.GetFromJsonAsync<TournamentDetails>($"/api/tournaments/{created.Id}", TestContext.Current.CancellationToken);
        Assert.Equal("City Cup", loaded!.Name);

        var updated = await client.PutAsJsonAsync($"/api/tournaments/{created.Id}", Sample(name: "Updated Cup"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<TournamentDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Updated Cup", body!.Name);

        var again = await client.PutAsJsonAsync($"/api/tournaments/{created.Id}", Sample(name: "Latest Cup"), TestContext.Current.CancellationToken);
        var latest = await again.Content.ReadFromJsonAsync<TournamentDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Latest Cup", latest!.Name);
    }

    [Fact]
    public async Task MissingTournamentIsNotFound()
    {
        var client = factory.CreateMemberClient();
        var response = await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateNamesAndLongSpansAreSaved()
    {
        var client = factory.CreateMemberClient();
        var first = await client.PostAsJsonAsync("/api/tournaments", Sample(name: "City Cup"), TestContext.Current.CancellationToken);
        var second = await client.PostAsJsonAsync("/api/tournaments", Sample(name: "  city cup  "), TestContext.Current.CancellationToken);
        var longSpan = await client.PostAsJsonAsync("/api/tournaments", Sample(
            name: "Long",
            end: new DateOnly(2026, 9, 6)),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, longSpan.StatusCode);
        var saved = await second.Content.ReadFromJsonAsync<TournamentDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("city cup", saved!.Name);
    }

    [Theory]
    [InlineData("   ", "2026-09-01", "2026-09-03", "America/Chicago", 60)]
    [InlineData("City Cup", "2026-09-03", "2026-09-01", "America/Chicago", 60)]
    [InlineData("City Cup", "2026-09-01", "2026-09-01", "America/Chicago", 60)]
    [InlineData("City Cup", "2026-09-01", "2026-09-03", "UTC", 60)]
    [InlineData("City Cup", "2026-09-01", "2026-09-03", "America/Chicago", 14)]
    [InlineData("City Cup", "2026-09-01", "2026-09-03", "America/Chicago", 241)]
    public async Task InvalidDetailsAreRejected(string name, string start, string end, string timeZoneId, int duration)
    {
        var client = factory.CreateMemberClient();
        var response = await client.PostAsJsonAsync("/api/tournaments", new
        {
            name,
            description = "",
            startDate = start,
            endDate = end,
            timeZoneId,
            defaultMatchDurationMinutes = duration,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingRequiredDetailsAreRejected()
    {
        var client = factory.CreateMemberClient();
        var response = await client.PostAsJsonAsync("/api/tournaments", new { description = "" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FractionalDurationIsRejected()
    {
        var client = factory.CreateMemberClient();
        var response = await client.PostAsJsonAsync("/api/tournaments", new
        {
            name = "City Cup",
            description = "",
            startDate = "2026-09-01",
            endDate = "2026-09-03",
            timeZoneId = "America/Chicago",
            defaultMatchDurationMinutes = 15.5,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnotherOrganizationsTournamentIsHiddenAndCannotBeChanged()
    {
        var other = await InsertOtherOrganizationTournamentAsync();
        var client = factory.CreateMemberClient();

        var list = await client.GetFromJsonAsync<List<TournamentDetails>>("/api/tournaments", TestContext.Current.CancellationToken);
        Assert.DoesNotContain(list!, item => item.Id == other.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{other.Id}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/tournaments/{other.Id}", Sample(name: "Hijack"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/tournaments/{other.Id}", TestContext.Current.CancellationToken)).StatusCode);

        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var stillThere = await db.Tournaments.IgnoreQueryFilters().AnyAsync(tournament => tournament.Id == other.Id, TestContext.Current.CancellationToken);
        Assert.True(stillThere);
    }

    internal static SaveTournament Sample(
        string name = "City Cup",
        DateOnly? end = null) =>
        new(name, "Notes", new DateOnly(2026, 9, 1), end ?? new DateOnly(2026, 9, 3), "America/Chicago", 60);

    internal static async Task<TournamentDetails> CreateAsync(HttpClient client, SaveTournament command)
    {
        var response = await client.PostAsJsonAsync("/api/tournaments", command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TournamentDetails>())!;
    }

    private async Task<Tournament> InsertOtherOrganizationTournamentAsync()
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
        tournament.AddVenue("Secret Gym", "1 Other St", 2);
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();
        return tournament;
    }
}