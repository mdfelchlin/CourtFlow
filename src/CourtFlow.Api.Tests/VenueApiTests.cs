using System.Net;
using System.Net.Http.Json;

using CourtFlow.Application;
using CourtFlow.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiFixture.Name)]
public sealed class VenueApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task VenueRulesMatchTheDomainAndCourtIdentityIsKept()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());

        var created = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("  Main Gym  ", "   ", 3), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var venue = (await created.Content.ReadFromJsonAsync<VenueDetails>(TestContext.Current.CancellationToken))!;
        Assert.Equal("Main Gym", venue.Name);
        Assert.Equal("", venue.Address);
        var original = venue.Courts.ToDictionary(court => court.Number, court => court.Id);

        var duplicate = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Main Gym", null, 0), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);

        var raised = await client.PutAsJsonAsync($"/api/venues/{venue.Id}", new SaveVenue("Main Gym", "100 Court St", 5), TestContext.Current.CancellationToken);
        var raisedBody = (await raised.Content.ReadFromJsonAsync<VenueDetails>(TestContext.Current.CancellationToken))!;
        Assert.Equal(original[1], raisedBody.Courts.Single(court => court.Number == 1).Id);
        Assert.Equal(original[3], raisedBody.Courts.Single(court => court.Number == 3).Id);
        Assert.Equal([1, 2, 3, 4, 5], raisedBody.Courts.Select(court => court.Number).ToArray());

        var lowered = await client.PutAsJsonAsync($"/api/venues/{venue.Id}", new SaveVenue("Main Gym", "100 Court St", 1), TestContext.Current.CancellationToken);
        var loweredBody = (await lowered.Content.ReadFromJsonAsync<VenueDetails>(TestContext.Current.CancellationToken))!;
        var remaining = Assert.Single(loweredBody.Courts);
        Assert.Equal(1, remaining.Number);
        Assert.Equal(original[1], remaining.Id);

        var cleared = await client.PutAsJsonAsync($"/api/venues/{venue.Id}", new SaveVenue("Main Gym", null, 0), TestContext.Current.CancellationToken);
        var clearedBody = (await cleared.Content.ReadFromJsonAsync<VenueDetails>(TestContext.Current.CancellationToken))!;
        Assert.Empty(clearedBody.Courts);
    }

    [Theory]
    [InlineData("   ", 1)]
    [InlineData("Main Gym", -1)]
    [InlineData("Main Gym", 100)]
    public async Task InvalidVenuesAreRejected(string name, int courtCount)
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/venues",
            new SaveVenue(name, null, courtCount),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AMissingCourtCountIsRejected()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournament.Id}/venues",
            new { name = "Main Gym", address = "" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VenueChangesForAMissingTournamentAreRejected()
    {
        var client = factory.CreateMemberClient();
        var missingTournament = Guid.NewGuid();
        var missingVenue = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/tournaments/{missingTournament}/venues", new SaveVenue("Gym", null, 1), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/venues/{missingVenue}", new SaveVenue("Gym", null, 1), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/venues/{missingVenue}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task DeletingATournamentDeletesItsVenuesAndCourts()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var created = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Main Gym", null, 2), TestContext.Current.CancellationToken);
        var venue = (await created.Content.ReadFromJsonAsync<VenueDetails>(TestContext.Current.CancellationToken))!;

        var deleted = await client.DeleteAsync($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken)).StatusCode);

        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.False(await db.Venues.IgnoreQueryFilters().AnyAsync(item => item.Id == venue.Id, TestContext.Current.CancellationToken));
        Assert.False(await db.Courts.IgnoreQueryFilters().AnyAsync(court => venue.Courts.Select(item => item.Id).Contains(court.Id), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeletingAVenueDeletesItsCourtsAndLeavesTheTournament()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var created = await client.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Main Gym", null, 2), TestContext.Current.CancellationToken);
        var venue = (await created.Content.ReadFromJsonAsync<VenueDetails>(TestContext.Current.CancellationToken))!;

        var deleted = await client.DeleteAsync($"/api/venues/{venue.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var stillThere = await client.GetFromJsonAsync<TournamentDetails>($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(tournament.Id, stillThere!.Id);
        var venues = await client.GetFromJsonAsync<List<VenueDetails>>($"/api/tournaments/{tournament.Id}/venues", TestContext.Current.CancellationToken);
        Assert.Empty(venues!);

        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.False(await db.Courts.IgnoreQueryFilters().AnyAsync(court => court.VenueId == venue.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnotherOrganizationsVenueCannotBeChanged()
    {
        var otherVenueId = await InsertOtherVenueAsync();
        var client = factory.CreateMemberClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/venues/{otherVenueId}", new SaveVenue("Hijack", null, 1), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/venues/{otherVenueId}", TestContext.Current.CancellationToken)).StatusCode);

        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.True(await db.Venues.IgnoreQueryFilters().AnyAsync(venue => venue.Id == otherVenueId, TestContext.Current.CancellationToken));
    }

    private async Task<Guid> InsertOtherVenueAsync()
    {
        using var scope = factory.CreateTestOrganizationScope();
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