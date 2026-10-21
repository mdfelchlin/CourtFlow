using System.Net;
using System.Net.Http.Json;
using System.Text;

using CourtFlow.Application;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace CourtFlow.Api.Tests;

[Collection(ApiFixture.Name)]
public sealed class RankingApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task FormatListReportsCountsAndRankingStatusInDivisionOrder()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var open = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Male");
        var club = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "12U Club", "Female");
        await TeamApiTests.AddOkAsync(client, tournament.Id, "Kent State", "Kent A", club.Id);
        var zeta = await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "Zeta", open.Id);
        var alpha = await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "alpha", open.Id);
        await SaveOkAsync(client, open.Id, [alpha.Id, zeta.Id]);
        await TeamApiTests.AddOkAsync(client, tournament.Id, "Purdue", "Boilers", open.Id);

        var format = await ListAsync(client, tournament.Id);

        Assert.Equal(["12U Club", "14U Open", "14U Open"], format.Select(division => division.Name).ToArray());
        Assert.Equal(["Female", "Female", "Male"], format.Select(division => division.Gender).ToArray());
        Assert.Equal(1, format[0].TeamCount);
        Assert.Equal(RankingWording.NotRanked, format[0].Ranking);
        Assert.Equal(3, format[1].TeamCount);
        Assert.Equal("Ranked, 1 new team at the bottom.", format[1].Ranking);
        Assert.Equal(RankingWording.AddTeamsFirst, format[2].Ranking);
        Assert.Equal(0, format[2].TeamCount);
    }

    [Fact]
    public async Task AnUnrankedDivisionReadsBackInNameOrderAndSavingRanksThatOrder()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        await TeamApiTests.AddOkAsync(client, tournament.Id, "Zeta Club", "Alpha", division.Id);
        await TeamApiTests.AddOkAsync(client, tournament.Id, "Alpha Club", "alpha", division.Id);
        await TeamApiTests.AddOkAsync(client, tournament.Id, "Alpha Club", "Bravo", division.Id);

        var before = await GetAsync(client, division.Id);
        Assert.False(before.Ranked);
        Assert.Equal([("alpha", "Alpha Club"), ("Alpha", "Zeta Club"), ("Bravo", "Alpha Club")], before.Teams.Select(team => (team.TeamName, team.ClubName)).ToArray());
        Assert.Equal([1, 2, 3], before.Teams.Select(team => team.Rank).ToArray());
        Assert.All(before.Teams, team => Assert.False(team.IsNew));

        var saved = await SaveOkAsync(client, division.Id, before.Teams.Select(team => team.EntryId).ToArray());
        Assert.True(saved.Ranked);
        Assert.Equal(before.Teams.Select(team => team.EntryId), saved.Teams.Select(team => team.EntryId));
        Assert.Equal(RankingWording.Ranked, (await ListAsync(client, tournament.Id)).Single().Ranking);
    }

    [Fact]
    public async Task AddingMovingDeletingAndImportingKeepTheRankingInTheSameChange()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var open = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var gold = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Gold", "Female");
        var first = await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        await SaveOkAsync(client, open.Id, [first.Id]);
        var second = await TeamApiTests.AddOkAsync(client, tournament.Id, "Kent State", "Kent A", open.Id);
        var parked = await TeamApiTests.AddOkAsync(client, tournament.Id, "Purdue", "Boilers", gold.Id);

        var afterAdd = await GetAsync(client, open.Id);
        Assert.Equal([first.Id, second.Id], afterAdd.Teams.Select(team => team.EntryId).ToArray());
        Assert.True(afterAdd.Teams[1].IsNew);
        Assert.Equal("Ranked, 1 new team at the bottom.", (await ListAsync(client, tournament.Id)).Single(division => division.Id == open.Id).Ranking);

        var moved = await client.PutAsJsonAsync($"/api/tournament-teams/{parked.Id}", new UpdateTournamentTeam("Boilers", open.Id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal([first.Id, second.Id, parked.Id], (await GetAsync(client, open.Id)).Teams.Select(team => team.EntryId).ToArray());
        Assert.False((await GetAsync(client, gold.Id)).Ranked);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tournament-teams/{second.Id}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal([first.Id, parked.Id], (await GetAsync(client, open.Id)).Teams.Select(team => team.EntryId).ToArray());

        const string csv = "Club,Team,Division,Gender\nBall State,Cards,14U Open,F\nIndiana,Hoosiers,14U Open,F\n";
        var imported = await PostCsvAsync(client, $"/api/tournaments/{tournament.Id}/team-imports", csv);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var names = (await GetAsync(client, open.Id)).Teams.Select(team => team.TeamName).ToArray();
        Assert.Equal(["Ohio St U", "Boilers", "Cards", "Hoosiers"], names);
        Assert.Equal(3, (await GetAsync(client, open.Id)).Teams.Count(team => team.IsNew));
    }

    [Fact]
    public async Task RenamingATeamOrDivisionDoesNotChangeTheRanking()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var entry = await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", division.Id);
        await SaveOkAsync(client, division.Id, [entry.Id]);
        await TeamApiTests.AddOkAsync(client, tournament.Id, "Kent State", "Kent A", division.Id);

        var renamed = await client.PutAsJsonAsync($"/api/tournament-teams/{entry.Id}", new UpdateTournamentTeam("Ohio St Gold", division.Id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var renamedDivision = await client.PutAsJsonAsync($"/api/divisions/{division.Id}", new SaveDivision("14U Gold", "Male"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, renamedDivision.StatusCode);

        var ranking = await GetAsync(client, division.Id);
        Assert.Equal(["Ohio St Gold", "Kent A"], ranking.Teams.Select(team => team.TeamName).ToArray());
        Assert.True(ranking.Teams[1].IsNew);
        Assert.Equal("14U Gold", ranking.DivisionName);
    }

    [Fact]
    public async Task ASaveWithTheWrongTeamsWritesNothing()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var first = await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", division.Id);
        var second = await TeamApiTests.AddOkAsync(client, tournament.Id, "Kent State", "Kent A", division.Id);
        await SaveOkAsync(client, division.Id, [second.Id, first.Id]);

        var missing = await SaveAsync(client, division.Id, [first.Id]);
        var extra = await SaveAsync(client, division.Id, [first.Id, second.Id, Guid.NewGuid()]);
        var repeated = await SaveAsync(client, division.Id, [first.Id, first.Id]);
        Assert.Equal(Tournament.TeamsChanged, await TeamApiTests.RejectionAsync(missing));
        Assert.Equal(Tournament.TeamsChanged, await TeamApiTests.RejectionAsync(extra));
        Assert.Equal(Tournament.TeamsChanged, await TeamApiTests.RejectionAsync(repeated));
        Assert.Equal([second.Id, first.Id], (await GetAsync(client, division.Id)).Teams.Select(team => team.EntryId).ToArray());
    }

    [Fact]
    public async Task ArchivedSaveIsRejectedAndAViewerCannotSave()
    {
        var owner = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(owner, TournamentApiTests.Sample());
        var division = await TeamApiTests.CreateDivisionAsync(owner, tournament.Id, "14U Open", "Female");
        var entry = await TeamApiTests.AddOkAsync(owner, tournament.Id, "Ohio State University", "Ohio St U", division.Id);
        await TournamentLifecycleApiTests.TransitionAsync(owner, tournament.Id, "publish");
        var viewer = factory.CreateMemberClient(OrganizationRole.Viewer);

        Assert.Equal(HttpStatusCode.Forbidden, (await SaveAsync(viewer, division.Id, [entry.Id])).StatusCode);
        Assert.False((await GetAsync(viewer, division.Id)).Ranked);

        await TournamentLifecycleApiTests.TransitionAsync(owner, tournament.Id, "archive");
        var archived = await SaveAsync(owner, division.Id, [entry.Id]);
        Assert.Equal(Tournament.ArchivedMessage, await TeamApiTests.RejectionAsync(archived));
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/divisions/{division.Id}/ranking", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task AnotherOrganizationsDivisionIsNotFoundAndDeletingATournamentRemovesRankings()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var entry = await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", division.Id);
        await SaveOkAsync(client, division.Id, [entry.Id]);

        var other = await InsertOtherDivisionAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/divisions/{other}/ranking", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SaveAsync(client, other, [entry.Id])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{Guid.NewGuid()}/format", TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken)).StatusCode);
        await using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.False(await db.Set<RankingPlace>().AnyAsync(place => place.DivisionId == division.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheDatabaseRefusesARepeatedEntryOrRank()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var open = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var gold = await TeamApiTests.CreateDivisionAsync(client, tournament.Id, "14U Gold", "Female");
        var first = await TeamApiTests.AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        var second = await TeamApiTests.AddOkAsync(client, tournament.Id, "Kent State", "Kent A", open.Id);
        var other = await TeamApiTests.AddOkAsync(client, tournament.Id, "Purdue", "Boilers", gold.Id);
        await SaveOkAsync(client, open.Id, [first.Id, second.Id]);

        await using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var place = await db.Set<RankingPlace>().SingleAsync(item => item.EntryId == first.Id, TestContext.Current.CancellationToken);

        var repeatedEntry = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ranking_places ("Id", "OrganizationId", "TournamentId", "DivisionId", "EntryId", "Rank", "IsNew")
            VALUES ({Guid.NewGuid()}, {place.OrganizationId}, {place.TournamentId}, {place.DivisionId}, {place.EntryId}, {99}, {false})
            """, TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, repeatedEntry.SqlState);

        var repeatedRank = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ranking_places ("Id", "OrganizationId", "TournamentId", "DivisionId", "EntryId", "Rank", "IsNew")
            VALUES ({Guid.NewGuid()}, {place.OrganizationId}, {place.TournamentId}, {place.DivisionId}, {other.Id}, {1}, {false})
            """, TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, repeatedRank.SqlState);
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid divisionId, IReadOnlyList<Guid> entryIds) =>
        client.PutAsJsonAsync($"/api/divisions/{divisionId}/ranking", new SaveRanking(entryIds), TestContext.Current.CancellationToken);

    private static async Task<DivisionRankingDetails> SaveOkAsync(HttpClient client, Guid divisionId, IReadOnlyList<Guid> entryIds)
    {
        var response = await SaveAsync(client, divisionId, entryIds);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DivisionRankingDetails>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<DivisionRankingDetails> GetAsync(HttpClient client, Guid divisionId) =>
        (await client.GetFromJsonAsync<DivisionRankingDetails>($"/api/divisions/{divisionId}/ranking", TestContext.Current.CancellationToken))!;

    private static async Task<List<FormatDivisionDetails>> ListAsync(HttpClient client, Guid tournamentId) =>
        (await client.GetFromJsonAsync<List<FormatDivisionDetails>>($"/api/tournaments/{tournamentId}/format", TestContext.Current.CancellationToken))!;

    private static Task<HttpResponseMessage> PostCsvAsync(HttpClient client, string path, string csv)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        var form = new MultipartFormDataContent { { file, "file", "teams.csv" } };
        return client.PostAsync(path, form, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> InsertOtherDivisionAsync()
    {
        await using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new Organization(organizationId, "Other Organization"));
        var tournament = Tournament.Create(organizationId, "Secret Cup", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Denver", 45);
        var division = tournament.AddDivision("14U Open", "Female");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return division.Id;
    }
}