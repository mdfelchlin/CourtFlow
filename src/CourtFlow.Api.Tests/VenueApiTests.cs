using System.Net;
using System.Net.Http.Json;
using CourtFlow.Application;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class VenueApiTests(ApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Venue_rules_match_the_domain_and_court_identity_is_kept()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());

        var created = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("  Main Gym  ", "   ", 3));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var venue = (await created.Content.ReadFromJsonAsync<VenueDetails>())!;
        Assert.Equal("Main Gym", venue.Name);
        Assert.Equal("", venue.Address);
        var original = venue.Courts.ToDictionary(court => court.Number, court => court.Id);

        var duplicate = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Main Gym", null, 0));
        Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);

        var raised = await client.PutAsJsonAsync($"/api/venues/{venue.Id}", new SaveVenue("Main Gym", "100 Court St", 5));
        var raisedBody = (await raised.Content.ReadFromJsonAsync<VenueDetails>())!;
        Assert.Equal(original[1], raisedBody.Courts.Single(court => court.Number == 1).Id);
        Assert.Equal(original[3], raisedBody.Courts.Single(court => court.Number == 3).Id);
        Assert.Equal([1, 2, 3, 4, 5], raisedBody.Courts.Select(court => court.Number).ToArray());

        var lowered = await client.PutAsJsonAsync($"/api/venues/{venue.Id}", new SaveVenue("Main Gym", "100 Court St", 1));
        var loweredBody = (await lowered.Content.ReadFromJsonAsync<VenueDetails>())!;
        var remaining = Assert.Single(loweredBody.Courts);
        Assert.Equal(1, remaining.Number);
        Assert.Equal(original[1], remaining.Id);

        var cleared = await client.PutAsJsonAsync($"/api/venues/{venue.Id}", new SaveVenue("Main Gym", null, 0));
        var clearedBody = (await cleared.Content.ReadFromJsonAsync<VenueDetails>())!;
        Assert.Empty(clearedBody.Courts);
    }

    [Theory]
    [InlineData("   ", 1)]
    [InlineData("Main Gym", -1)]
    [InlineData("Main Gym", 100)]
    public async Task Invalid_venues_are_rejected(string name, int courtCount)
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/venues",
            new SaveVenue(name, null, courtCount));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_missing_court_count_is_rejected()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/venues",
            new { name = "Main Gym", address = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Venue_changes_for_a_missing_tournament_are_rejected()
    {
        var client = factory.CreateClient();
        var missingTournament = Guid.NewGuid();
        var missingVenue = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/tournaments/{missingTournament}/venues", new SaveVenue("Gym", null, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/venues/{missingVenue}", new SaveVenue("Gym", null, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/venues/{missingVenue}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_tournament_deletes_its_venues_and_courts()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var created = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Main Gym", null, 2));
        var venue = (await created.Content.ReadFromJsonAsync<VenueDetails>())!;

        var deleted = await client.DeleteAsync($"/api/tournaments/{tournament.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{tournament.Id}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.False(await db.Venues.IgnoreQueryFilters().AnyAsync(item => item.Id == venue.Id));
        Assert.False(await db.Courts.IgnoreQueryFilters().AnyAsync(court => venue.Courts.Select(item => item.Id).Contains(court.Id)));
    }

    [Fact]
    public async Task Deleting_a_venue_deletes_its_courts_and_leaves_the_tournament()
    {
        var client = factory.CreateClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var created = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Main Gym", null, 2));
        var venue = (await created.Content.ReadFromJsonAsync<VenueDetails>())!;

        var deleted = await client.DeleteAsync($"/api/venues/{venue.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var stillThere = await client.GetFromJsonAsync<TournamentDetails>($"/api/tournaments/{tournament.Id}");
        Assert.Equal(tournament.Id, stillThere!.Id);
        var venues = await client.GetFromJsonAsync<List<VenueDetails>>($"/api/tournaments/{tournament.Id}/venues");
        Assert.Empty(venues!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.False(await db.Courts.IgnoreQueryFilters().AnyAsync(court => court.VenueId == venue.Id));
    }

    [Fact]
    public async Task Another_organizations_venue_cannot_be_changed()
    {
        var otherVenueId = await InsertOtherVenueAsync();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/venues/{otherVenueId}", new SaveVenue("Hijack", null, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/venues/{otherVenueId}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.True(await db.Venues.IgnoreQueryFilters().AnyAsync(venue => venue.Id == otherVenueId));
    }

    private async Task<Guid> InsertOtherVenueAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new Domain.Organization(organizationId, "Other Organization"));
        var tournament = Domain.Tournament.Create(
            organizationId,
            "Secret Cup",
            null,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3),
            "America/Denver",
            45);
        var venue = tournament.AddVenue("Secret Gym", null, 1);
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();
        return venue.Id;
    }
}
