using System.Net;
using System.Net.Http.Json;
using CourtFlow.Application;
using CourtFlow.Domain;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class TournamentLifecycleApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_new_tournament_is_unpublished()
    {
        var client = factory.CreateMemberClient();

        var created = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());

        Assert.Equal(TournamentStatus.Unpublished, created.Status);
        Assert.Null(created.LastPublishedAt);
        Assert.Null(created.PublishedOn);
        Assert.Null(created.ArchivesOn);
    }

    [Fact]
    public async Task A_create_body_cannot_choose_a_state()
    {
        var client = factory.CreateMemberClient();

        var response = await client.PostAsJsonAsync("/api/tournaments", new
        {
            name = "City Cup",
            description = "",
            startDate = "2026-09-01",
            endDate = "2026-09-03",
            timeZoneId = "America/Chicago",
            defaultMatchDurationMinutes = 60,
            status = "Published",
            lastPublishedAt = "2020-01-01T00:00:00Z",
        }, TestContext.Current.CancellationToken);

        var created = await response.Content.ReadFromJsonAsync<TournamentDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(TournamentStatus.Unpublished, created!.Status);
        Assert.Null(created.LastPublishedAt);
    }

    [Fact]
    public async Task The_allowed_transitions_each_return_the_new_state()
    {
        var client = factory.CreateMemberClient();
        var created = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());

        Assert.Equal(TournamentStatus.Published, (await TransitionAsync(client, created.Id, "publish")).Status);
        Assert.Equal(TournamentStatus.Unpublished, (await TransitionAsync(client, created.Id, "unpublish")).Status);
        Assert.Equal(TournamentStatus.Published, (await TransitionAsync(client, created.Id, "publish")).Status);
        Assert.Equal(TournamentStatus.Archived, (await TransitionAsync(client, created.Id, "archive")).Status);
        Assert.Equal(TournamentStatus.Unpublished, (await TransitionAsync(client, created.Id, "restore")).Status);
    }

    [Theory]
    [InlineData("publish", "publish", Tournament.OnlyUnpublishedCanBePublished)]
    [InlineData(null, "unpublish", Tournament.OnlyPublishedCanBeUnpublished)]
    [InlineData(null, "archive", Tournament.OnlyPublishedCanBeArchived)]
    [InlineData(null, "restore", Tournament.OnlyArchivedCanBeRestored)]
    [InlineData("publish", "restore", Tournament.OnlyArchivedCanBeRestored)]
    public async Task An_illegal_transition_is_rejected_with_its_message(string? setUp, string action, string message)
    {
        var client = factory.CreateMemberClient();
        var created = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var before = setUp is null ? created : await TransitionAsync(client, created.Id, setUp);

        var response = await client.PostAsync($"/api/tournaments/{created.Id}/{action}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var after = await client.GetFromJsonAsync<TournamentDetails>($"/api/tournaments/{created.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(before.Status, after!.Status);
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("unpublish")]
    [InlineData("archive")]
    [InlineData("restore")]
    public async Task A_transition_on_an_unknown_tournament_is_not_found(string action)
    {
        var client = factory.CreateMemberClient();

        var response = await client.PostAsync($"/api/tournaments/{Guid.NewGuid()}/{action}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_list_and_the_tournament_carry_the_state_and_its_dates()
    {
        var client = factory.CreateMemberClient();
        var created = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var published = await TransitionAsync(client, created.Id, "publish");

        Assert.Equal(factory.Clock.GetUtcNow(), published.LastPublishedAt);
        Assert.Equal(new DateOnly(2026, 3, 1), published.PublishedOn);
        Assert.Equal(new DateOnly(2026, 11, 26), published.ArchivesOn);

        var list = await client.GetFromJsonAsync<List<TournamentDetails>>("/api/tournaments", TestContext.Current.CancellationToken);
        var listed = Assert.Single(list!);
        Assert.Equal(TournamentStatus.Published, listed.Status);
        Assert.Equal(published.PublishedOn, listed.PublishedOn);
        Assert.Equal(published.ArchivesOn, listed.ArchivesOn);
    }

    [Fact]
    public async Task The_published_day_is_the_tournaments_own_day_not_the_utc_one()
    {
        var client = factory.CreateMemberClient();
        var pacific = new SaveTournament("Coast Cup", "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Los_Angeles", 60);
        var created = await TournamentApiTests.CreateAsync(client, pacific);

        // 00:30 UTC on March 2 is still 16:30 on March 1 in Pacific time.
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 2, 0, 30, 0, TimeSpan.Zero));
        var published = await TransitionAsync(client, created.Id, "publish");

        Assert.Equal(new DateOnly(2026, 3, 1), published.PublishedOn);
        Assert.Equal(new DateOnly(2026, 11, 26), published.ArchivesOn);
    }

    [Fact]
    public async Task Saving_details_leaves_the_state_alone()
    {
        var client = factory.CreateMemberClient();
        var created = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var published = await TransitionAsync(client, created.Id, "publish");

        var response = await client.PutAsJsonAsync(
            $"/api/tournaments/{created.Id}",
            TournamentApiTests.Sample(name: "Renamed Cup"),
            TestContext.Current.CancellationToken);

        var saved = await response.Content.ReadFromJsonAsync<TournamentDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Renamed Cup", saved!.Name);
        Assert.Equal(TournamentStatus.Published, saved.Status);
        Assert.Equal(published.LastPublishedAt, saved.LastPublishedAt);
    }

    [Fact]
    public async Task A_published_tournament_cannot_be_deleted()
    {
        var client = factory.CreateMemberClient();
        var created = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        await TransitionAsync(client, created.Id, "publish");

        var response = await client.DeleteAsync($"/api/tournaments/{created.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(Tournament.PublishedCannotBeDeleted, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/tournaments/{created.Id}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_unpublished_or_archived_tournament_is_deleted(bool archiveFirst)
    {
        var client = factory.CreateMemberClient();
        var created = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        if (archiveFirst)
        {
            await TransitionAsync(client, created.Id, "publish");
            await TransitionAsync(client, created.Id, "archive");
        }

        var response = await client.DeleteAsync($"/api/tournaments/{created.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{created.Id}", TestContext.Current.CancellationToken)).StatusCode);
    }

    internal static async Task<TournamentDetails> TransitionAsync(HttpClient client, Guid id, string action)
    {
        var response = await client.PostAsync($"/api/tournaments/{id}/{action}", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TournamentDetails>())!;
    }
}
