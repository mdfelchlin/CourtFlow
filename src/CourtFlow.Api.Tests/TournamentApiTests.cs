using System.Net;
using System.Net.Http.Json;
using CourtFlow.Application;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class TournamentApiTests(ApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task List_returns_the_saved_fields_for_the_test_organization()
    {
        var client = factory.CreateClient();
        var created = await CreateAsync(client, Sample());

        var list = await client.GetFromJsonAsync<List<TournamentDetails>>("/api/tournaments");

        var item = Assert.Single(list!);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal("City Cup", item.Name);
        Assert.Equal(new DateOnly(2026, 9, 1), item.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 3), item.EndDate);
        Assert.Equal("America/Chicago", item.TimeZoneId);
        Assert.Equal(60, item.DefaultMatchDurationMinutes);
    }

    [Fact]
    public async Task Create_get_and_update_round_trip()
    {
        var client = factory.CreateClient();
        var created = await CreateAsync(client, Sample());

        var loaded = await client.GetFromJsonAsync<TournamentDetails>($"/api/tournaments/{created.Id}");
        Assert.Equal("City Cup", loaded!.Name);

        var updated = await client.PutAsJsonAsync($"/api/tournaments/{created.Id}", Sample(name: "Updated Cup"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<TournamentDetails>();
        Assert.Equal("Updated Cup", body!.Name);

        var again = await client.PutAsJsonAsync($"/api/tournaments/{created.Id}", Sample(name: "Latest Cup"));
        var latest = await again.Content.ReadFromJsonAsync<TournamentDetails>();
        Assert.Equal("Latest Cup", latest!.Name);
    }

    [Fact]
    public async Task Missing_tournament_is_not_found()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_names_and_long_spans_are_saved()
    {
        var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/tournaments", Sample(name: "City Cup"));
        var second = await client.PostAsJsonAsync("/api/tournaments", Sample(name: "  city cup  "));
        var longSpan = await client.PostAsJsonAsync("/api/tournaments", Sample(
            name: "Long",
            end: new DateOnly(2026, 9, 6)));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, longSpan.StatusCode);
        var saved = await second.Content.ReadFromJsonAsync<TournamentDetails>();
        Assert.Equal("city cup", saved!.Name);
    }

    [Theory]
    [InlineData("   ", "2026-09-01", "2026-09-03", "America/Chicago", 60)]
    [InlineData("City Cup", "2026-09-03", "2026-09-01", "America/Chicago", 60)]
    [InlineData("City Cup", "2026-09-01", "2026-09-01", "America/Chicago", 60)]
    [InlineData("City Cup", "2026-09-01", "2026-09-03", "UTC", 60)]
    [InlineData("City Cup", "2026-09-01", "2026-09-03", "America/Chicago", 14)]
    [InlineData("City Cup", "2026-09-01", "2026-09-03", "America/Chicago", 241)]
    public async Task Invalid_details_are_rejected(string name, string start, string end, string timeZoneId, int duration)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/tournaments", new
        {
            name,
            description = "",
            startDate = start,
            endDate = end,
            timeZoneId,
            defaultMatchDurationMinutes = duration,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_required_details_are_rejected()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/tournaments", new { description = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Fractional_duration_is_rejected()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/tournaments", new
        {
            name = "City Cup",
            description = "",
            startDate = "2026-09-01",
            endDate = "2026-09-03",
            timeZoneId = "America/Chicago",
            defaultMatchDurationMinutes = 15.5,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Another_organizations_tournament_is_hidden_and_cannot_be_changed()
    {
        var other = await InsertOtherOrganizationTournamentAsync();
        var client = factory.CreateClient();

        var list = await client.GetFromJsonAsync<List<TournamentDetails>>("/api/tournaments");
        Assert.DoesNotContain(list!, item => item.Id == other.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/tournaments/{other.Id}", Sample(name: "Hijack"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/tournaments/{other.Id}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var stillThere = await db.Tournaments.IgnoreQueryFilters().AnyAsync(tournament => tournament.Id == other.Id);
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
        tournament.AddVenue("Secret Gym", "1 Other St", 2);
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();
        return tournament;
    }
}
