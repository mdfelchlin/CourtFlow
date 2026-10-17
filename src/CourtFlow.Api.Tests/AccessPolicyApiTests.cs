using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CourtFlow.Api.Access;
using CourtFlow.Application;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using static CourtFlow.Api.Tests.AccessTestHelpers;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AccessPolicyApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static readonly Guid AnyId = Guid.NewGuid();

    private static IEnumerable<(string Name, Func<HttpClient, Task<HttpResponseMessage>> Send)> OrganizationRoutes() =>
    [
        ("list tournaments", client => client.GetAsync("/api/tournaments")),
        ("get tournament", client => client.GetAsync($"/api/tournaments/{AnyId}")),
        ("venues", client => client.GetAsync($"/api/tournaments/{AnyId}/venues")),
        ("divisions", client => client.GetAsync($"/api/tournaments/{AnyId}/divisions")),
        ("teams", client => client.GetAsync($"/api/tournaments/{AnyId}/teams")),
        ("create tournament", client => client.PostAsJsonAsync("/api/tournaments", TournamentApiTests.Sample())),
        ("organization", client => client.GetAsync("/api/organization")),
    ];

    [Fact]
    public async Task Signed_out_calls_to_every_organization_route_get_401()
    {
        var client = factory.CreateClient();
        foreach (var (name, send) in OrganizationRoutes())
        {
            var response = await send(client);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{name} returned {response.StatusCode}.");
        }
    }

    [Fact]
    public async Task Signed_in_without_a_membership_gets_no_access()
    {
        var client = factory.CreateClientAs(TestUser.Named("stranger"));
        foreach (var (name, send) in OrganizationRoutes())
            Assert.True(await ReasonAsync(await send(client)) == AccessReasons.NoAccess, name);
    }

    [Fact]
    public async Task A_deactivated_organization_gets_organization_deactivated()
    {
        var organization = await factory.CreateOrganizationAsync("Gone Club", OrganizationStatus.Deactivated);
        var user = TestUser.Named("gone");
        factory.AddMember(organization.Id, user, OrganizationRole.Owner);

        var response = await factory.CreateClientAs(user).GetAsync("/api/tournaments", TestContext.Current.CancellationToken);

        Assert.Equal(AccessReasons.OrganizationDeactivated, await ReasonAsync(response));
    }

    [Fact]
    public async Task A_viewer_sees_only_published_tournaments_and_their_children()
    {
        var owner = factory.CreateMemberClient();
        var unpublished = await CreateWithChildrenAsync(owner, "Draft Cup");
        var published = await CreateWithChildrenAsync(owner, "Live Cup");
        await TournamentLifecycleApiTests.TransitionAsync(owner, published.Id, "publish");
        var archived = await CreateWithChildrenAsync(owner, "Old Cup");
        await TournamentLifecycleApiTests.TransitionAsync(owner, archived.Id, "publish");
        await TournamentLifecycleApiTests.TransitionAsync(owner, archived.Id, "archive");
        var viewer = factory.CreateMemberClient(OrganizationRole.Viewer);

        var list = await viewer.GetFromJsonAsync<List<TournamentDetails>>("/api/tournaments", TestContext.Current.CancellationToken);
        Assert.Equal(published.Id, Assert.Single(list!).Id);

        foreach (var hidden in new[] { unpublished.Id, archived.Id })
        {
            foreach (var path in new[] { "", "/venues", "/divisions", "/teams" })
            {
                var response = await viewer.GetAsync($"/api/tournaments/{hidden}{path}", TestContext.Current.CancellationToken);
                Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{path} of a hidden tournament returned {response.StatusCode}.");
            }
        }

        foreach (var path in new[] { "", "/venues", "/divisions", "/teams" })
        {
            var response = await viewer.GetAsync($"/api/tournaments/{published.Id}{path}", TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} of the published tournament returned {response.StatusCode}.");
        }
    }

    [Fact]
    public async Task Every_viewer_write_is_forbidden()
    {
        var owner = factory.CreateMemberClient();
        var tournament = await CreateWithChildrenAsync(owner, "Live Cup");
        await TournamentLifecycleApiTests.TransitionAsync(owner, tournament.Id, "publish");
        var venue = (await owner.GetFromJsonAsync<List<VenueDetails>>($"/api/tournaments/{tournament.Id}/venues", TestContext.Current.CancellationToken))!.Single();
        var division = (await owner.GetFromJsonAsync<List<DivisionDetails>>($"/api/tournaments/{tournament.Id}/divisions", TestContext.Current.CancellationToken))!.Single();
        var entry = (await TeamApiTests.ListAsync(owner, tournament.Id)).Single();
        var viewer = factory.CreateMemberClient(OrganizationRole.Viewer);

        (string, Func<Task<HttpResponseMessage>>)[] writes =
        [
            ("create tournament", () => viewer.PostAsJsonAsync("/api/tournaments", TournamentApiTests.Sample())),
            ("update tournament", () => viewer.PutAsJsonAsync($"/api/tournaments/{tournament.Id}", TournamentApiTests.Sample())),
            ("delete tournament", () => viewer.DeleteAsync($"/api/tournaments/{tournament.Id}")),
            ("publish", () => PostAsync(viewer, $"/api/tournaments/{tournament.Id}/publish")),
            ("unpublish", () => PostAsync(viewer, $"/api/tournaments/{tournament.Id}/unpublish")),
            ("archive", () => PostAsync(viewer, $"/api/tournaments/{tournament.Id}/archive")),
            ("restore", () => PostAsync(viewer, $"/api/tournaments/{tournament.Id}/restore")),
            ("create venue", () => viewer.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/venues", new SaveVenue("Gym", null, 1))),
            ("update venue", () => viewer.PutAsJsonAsync($"/api/venues/{venue.Id}", new SaveVenue("Gym", null, 1))),
            ("delete venue", () => viewer.DeleteAsync($"/api/venues/{venue.Id}")),
            ("create division", () => viewer.PostAsJsonAsync($"/api/tournaments/{tournament.Id}/divisions", new SaveDivision("16U", "Male"))),
            ("update division", () => viewer.PutAsJsonAsync($"/api/divisions/{division.Id}", new SaveDivision("16U", "Male"))),
            ("delete division", () => viewer.DeleteAsync($"/api/divisions/{division.Id}")),
            ("add team", () => TeamApiTests.AddAsync(viewer, tournament.Id, "Club", "Team", division.Id)),
            ("update team", () => viewer.PutAsJsonAsync($"/api/tournament-teams/{entry.Id}", new UpdateTournamentTeam("Renamed", division.Id))),
            ("remove team", () => viewer.DeleteAsync($"/api/tournament-teams/{entry.Id}")),
            ("club search", () => viewer.GetAsync("/api/clubs?search=Ohio")),
            ("club teams", () => viewer.GetAsync($"/api/clubs/{entry.ClubId}/teams")),
            ("import preview", () => viewer.PostAsync($"/api/tournaments/{tournament.Id}/team-imports/preview", CsvForm())),
            ("import", () => viewer.PostAsync($"/api/tournaments/{tournament.Id}/team-imports", CsvForm())),
        ];

        foreach (var (name, send) in writes)
            Assert.True(await ReasonAsync(await send()) == AccessReasons.Forbidden, name);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Editor)]
    public async Task Owners_and_editors_see_every_state_and_use_every_lifecycle_action(OrganizationRole role)
    {
        var client = factory.CreateMemberClient(role);
        var unpublished = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample("Draft Cup"));
        var archived = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample("Old Cup"));
        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "publish");
        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "archive");
        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "restore");
        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "publish");
        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "unpublish");
        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "publish");
        await TournamentLifecycleApiTests.TransitionAsync(client, archived.Id, "archive");

        var list = await client.GetFromJsonAsync<List<TournamentDetails>>("/api/tournaments", TestContext.Current.CancellationToken);

        Assert.Equal(new[] { unpublished.Id, archived.Id }.Order(), list!.Select(item => item.Id).Order());
    }

    [Fact]
    public async Task An_operator_without_a_membership_gets_no_access_on_tournaments()
    {
        var response = await factory.CreateOperatorClient().GetAsync("/api/tournaments", TestContext.Current.CancellationToken);

        Assert.Equal(AccessReasons.NoAccess, await ReasonAsync(response));
    }

    [Fact]
    public async Task A_tournament_leaving_published_disappears_for_a_viewer()
    {
        var editor = factory.CreateMemberClient(OrganizationRole.Editor);
        var tournament = await TournamentApiTests.CreateAsync(editor, TournamentApiTests.Sample());
        await TournamentLifecycleApiTests.TransitionAsync(editor, tournament.Id, "publish");
        var viewer = factory.CreateMemberClient(OrganizationRole.Viewer);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken)).StatusCode);

        await TournamentLifecycleApiTests.TransitionAsync(editor, tournament.Id, "unpublish");

        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task A_tournament_in_one_organization_is_not_found_from_another()
    {
        var mine = await TournamentApiTests.CreateAsync(factory.CreateMemberClient(), TournamentApiTests.Sample());
        var other = await factory.CreateOrganizationAsync("Other Club");
        var outsider = TestUser.Named("outsider");
        factory.AddMember(other.Id, outsider, OrganizationRole.Owner);
        var client = factory.CreateClientAs(outsider);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{mine.Id}", TestContext.Current.CancellationToken)).StatusCode);
        var list = await client.GetFromJsonAsync<List<TournamentDetails>>("/api/tournaments", TestContext.Current.CancellationToken);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task A_write_with_the_session_cookie_from_another_origin_is_refused()
    {
        var client = factory.CreateMemberClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tournaments")
        {
            Content = JsonContent.Create(TournamentApiTests.Sample()),
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}=anything");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(AccessReasons.Forbidden, await ReasonAsync(response));
    }

    [Fact]
    public async Task A_write_with_the_session_cookie_from_the_web_origin_is_allowed()
    {
        var client = factory.CreateMemberClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tournaments")
        {
            Content = JsonContent.Create(TournamentApiTests.Sample()),
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}=anything");
        request.Headers.Add("Origin", ApiFactory.WebOrigin);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static MultipartFormDataContent CsvForm()
    {
        var file = new ByteArrayContent("Club,Team,Division,Gender\nA,B,14U Open,Female\n"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", "teams.csv" } };
    }
}
