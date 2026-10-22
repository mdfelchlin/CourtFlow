using System.Net.Http.Json;

using CourtFlow.Application;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiFixture.Name)]
public sealed class AutoArchiveApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task NothingIsArchivedUntilThe270DaysAreUp()
    {
        var client = factory.CreateMemberClient();
        var published = await PublishedAsync(client);

        factory.Clock.Advance(TimeSpan.FromDays(269));
        Assert.Equal(0, await factory.RunAutoArchiveAsync());
        Assert.Equal(TournamentStatus.Published, await StatusAsync(client, published.Id));

        factory.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, await factory.RunAutoArchiveAsync());
        Assert.Equal(TournamentStatus.Archived, await StatusAsync(client, published.Id));
    }

    [Fact]
    public async Task UnpublishedAndArchivedTournamentsAreLeftAlone()
    {
        var client = factory.CreateMemberClient();
        var unpublished = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var archived = await PublishedAsync(client, "Archived Cup");
        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "archive");

        factory.Clock.Advance(TimeSpan.FromDays(400));
        Assert.Equal(0, await factory.RunAutoArchiveAsync());

        Assert.Equal(TournamentStatus.Unpublished, await StatusAsync(client, unpublished.Id));
        Assert.Equal(TournamentStatus.Archived, await StatusAsync(client, archived.Id));
    }

    [Fact]
    public async Task RepublishingRestartsTheClock()
    {
        var client = factory.CreateMemberClient();
        var published = await PublishedAsync(client);

        factory.Clock.Advance(TimeSpan.FromDays(100));
        await TournamentLifecycleApiTests.TransitionAsync(client, published.Id, "unpublish");
        await TournamentLifecycleApiTests.TransitionAsync(client, published.Id, "publish");

        factory.Clock.Advance(TimeSpan.FromDays(170));
        Assert.Equal(0, await factory.RunAutoArchiveAsync());
        Assert.Equal(TournamentStatus.Published, await StatusAsync(client, published.Id));

        factory.Clock.Advance(TimeSpan.FromDays(100));
        Assert.Equal(1, await factory.RunAutoArchiveAsync());
        Assert.Equal(TournamentStatus.Archived, await StatusAsync(client, published.Id));
    }

    [Fact]
    public async Task RunningTheArchiveTwiceChangesNothingTheSecondTime()
    {
        var client = factory.CreateMemberClient();
        var published = await PublishedAsync(client);
        factory.Clock.Advance(TimeSpan.FromDays(270));

        Assert.Equal(1, await factory.RunAutoArchiveAsync());
        Assert.Equal(0, await factory.RunAutoArchiveAsync());
        Assert.Equal(TournamentStatus.Archived, await StatusAsync(client, published.Id));
    }

    [Fact]
    public async Task EveryOrganizationsTournamentsAreArchived()
    {
        var client = factory.CreateMemberClient();
        var mine = await PublishedAsync(client);
        var other = await InsertOtherOrganizationPublishedAsync();
        var freshlyPublished = await PublishedAsync(client, "Fresh Cup");

        factory.Clock.Advance(TimeSpan.FromDays(270));
        // Published later, so this one is not due yet.
        await TournamentLifecycleApiTests.TransitionAsync(client, freshlyPublished.Id, "unpublish");
        await TournamentLifecycleApiTests.TransitionAsync(client, freshlyPublished.Id, "publish");

        Assert.Equal(2, await factory.RunAutoArchiveAsync());

        Assert.Equal(TournamentStatus.Archived, await StatusAsync(client, mine.Id));
        Assert.Equal(TournamentStatus.Archived, await StoredStatusAsync(other));
        Assert.Equal(TournamentStatus.Published, await StatusAsync(client, freshlyPublished.Id));
    }

    private static async Task<TournamentDetails> PublishedAsync(HttpClient client, string name = "City Cup")
    {
        var created = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name: name));
        return await TournamentLifecycleApiTests.TransitionAsync(client, created.Id, "publish");
    }

    private static async Task<TournamentStatus> StatusAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<TournamentDetails>($"/api/tournaments/{id}", TestContext.Current.CancellationToken))!.Status;

    private async Task<TournamentStatus> StoredStatusAsync(Guid id)
    {
        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var tournament = await db.Tournaments
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        return tournament.Status;
    }

    private async Task<Guid> InsertOtherOrganizationPublishedAsync()
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
        tournament.Publish(factory.Clock.GetUtcNow());
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return tournament.Id;
    }
}