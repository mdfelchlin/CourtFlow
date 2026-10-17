using System.Net;
using System.Net.Http.Json;
using CourtFlow.Application;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class TeamApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task List_is_ordered_by_division_then_team_then_club()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var open = await CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var club = await CreateDivisionAsync(client, tournament.Id, "12U Club", "Female");

        await AddOkAsync(client, tournament.Id, "Zeta Club", "Alpha", open.Id);
        await AddOkAsync(client, tournament.Id, "Alpha Club", "alpha", open.Id);
        await AddOkAsync(client, tournament.Id, "Beta Club", "Bravo", club.Id);
        await AddOkAsync(client, tournament.Id, "Beta Club", "Aardvark", open.Id);

        var listed = await ListAsync(client, tournament.Id);

        Assert.Equal(
            [("Bravo", "Beta Club"), ("Aardvark", "Beta Club"), ("alpha", "Alpha Club"), ("Alpha", "Zeta Club")],
            listed.Select(team => (team.TeamName, team.ClubName)).ToArray());
        Assert.Equal(("12U Club", "Female"), (listed[0].DivisionName, listed[0].DivisionGender));
    }

    [Fact]
    public async Task Add_creates_the_club_and_team_once_and_reuses_them_by_name()
    {
        var client = factory.CreateMemberClient();
        var first = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var second = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name: "Other Cup"));
        var firstDivision = await CreateDivisionAsync(client, first.Id, "Men's Division I", "Male");
        var secondDivision = await CreateDivisionAsync(client, second.Id, "Men's Division I", "Male");

        var created = await AddOkAsync(client, first.Id, "  Ohio State University ", " Ohio St U ", firstDivision.Id);
        var reused = await AddOkAsync(client, second.Id, "ohio state university", "OHIO ST U", secondDivision.Id);

        Assert.Equal(("Ohio State University", "Ohio St U"), (created.ClubName, created.TeamName));
        Assert.Equal(created.ClubId, reused.ClubId);
        Assert.Equal(created.TeamId, reused.TeamId);
        Assert.Equal(("Ohio State University", "Ohio St U"), (reused.ClubName, reused.TeamName));
    }

    [Fact]
    public async Task Add_rejects_with_the_spec_messages()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var other = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name: "Other Cup"));
        var division = await CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var foreign = await CreateDivisionAsync(client, other.Id, "14U Open", "Female");
        await AddOkAsync(client, tournament.Id, "Club", "Team", division.Id);

        Assert.Equal(Club.NameRequired, await RejectionAsync(await AddAsync(client, tournament.Id, " ", "Team", division.Id)));
        Assert.Equal(Team.NameRequired, await RejectionAsync(await AddAsync(client, tournament.Id, "Club", " ", division.Id)));
        Assert.Equal(Division.Required, await RejectionAsync(await AddAsync(client, tournament.Id, "Club", "New", null)));
        Assert.Equal(Division.Required, await RejectionAsync(await AddAsync(client, tournament.Id, "Club", "New", foreign.Id)));
        Assert.Equal(Tournament.TeamAlreadyEntered, await RejectionAsync(await AddAsync(client, tournament.Id, " club ", " team ", division.Id)));
    }

    [Fact]
    public async Task Update_renames_everywhere_moves_and_rejects_a_name_collision()
    {
        var client = factory.CreateMemberClient();
        var first = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var second = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name: "Other Cup"));
        var open = await CreateDivisionAsync(client, first.Id, "14U Open", "Female");
        var gold = await CreateDivisionAsync(client, first.Id, "14U Gold", "Female");
        var secondOpen = await CreateDivisionAsync(client, second.Id, "14U Open", "Female");
        var entry = await AddOkAsync(client, first.Id, "Purdue University", "Purdue A", open.Id);
        await AddOkAsync(client, first.Id, "Purdue University", "Purdue B", open.Id);
        await AddOkAsync(client, second.Id, "Purdue University", "Purdue A", secondOpen.Id);

        var updated = await client.PutAsJsonAsync($"/api/tournament-teams/{entry.Id}", new UpdateTournamentTeam(" Purdue Gold ", gold.Id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var details = (await updated.Content.ReadFromJsonAsync<TournamentTeamDetails>(TestContext.Current.CancellationToken))!;
        Assert.Equal(("Purdue Gold", gold.Id), (details.TeamName, details.DivisionId));
        Assert.Contains(await ListAsync(client, second.Id), team => team.TeamName == "Purdue Gold");

        var collision = await client.PutAsJsonAsync($"/api/tournament-teams/{entry.Id}", new UpdateTournamentTeam("purdue b", gold.Id), TestContext.Current.CancellationToken);
        Assert.Equal(Team.DuplicateName, await RejectionAsync(collision));

        var caseOnly = await client.PutAsJsonAsync($"/api/tournament-teams/{entry.Id}", new UpdateTournamentTeam("PURDUE GOLD", gold.Id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, caseOnly.StatusCode);

        var noDivision = await client.PutAsJsonAsync($"/api/tournament-teams/{entry.Id}", new UpdateTournamentTeam("Purdue Gold", null), TestContext.Current.CancellationToken);
        Assert.Equal(Division.Required, await RejectionAsync(noDivision));
    }

    [Fact]
    public async Task Delete_removes_only_the_entry_and_the_team_can_be_entered_again()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var entry = await AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", division.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tournament-teams/{entry.Id}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Empty(await ListAsync(client, tournament.Id));

        var clubs = await client.GetFromJsonAsync<List<ClubDetails>>("/api/clubs?search=ohio", TestContext.Current.CancellationToken);
        Assert.Equal([entry.ClubId], clubs!.Select(club => club.Id).ToArray());

        var again = await AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", division.Id);
        Assert.Equal(entry.TeamId, again.TeamId);
    }

    [Fact]
    public async Task Missing_tournament_or_entry_is_not_found()
    {
        var client = factory.CreateMemberClient();
        var missing = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{missing}/teams", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AddAsync(client, missing, "Club", "Team", Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"/api/tournament-teams/{missing}",
            new UpdateTournamentTeam("Team", Guid.NewGuid()),
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/tournament-teams/{missing}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/clubs/{missing}/teams", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Club_search_and_team_options_flag_teams_already_entered()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var other = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample(name: "Other Cup"));
        var division = await CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var otherDivision = await CreateDivisionAsync(client, other.Id, "14U Open", "Female");
        var entered = await AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St A", division.Id);
        await AddOkAsync(client, other.Id, "Ohio State University", "Ohio St B", otherDivision.Id);
        await AddOkAsync(client, other.Id, "Kent State University", "Kent A", otherDivision.Id);

        var search = await client.GetFromJsonAsync<List<ClubDetails>>("/api/clubs?search=O%20STATE", TestContext.Current.CancellationToken);
        Assert.Equal(["Ohio State University"], search!.Select(club => club.Name).ToArray());
        var all = await client.GetFromJsonAsync<List<ClubDetails>>("/api/clubs", TestContext.Current.CancellationToken);
        Assert.Equal(["Kent State University", "Ohio State University"], all!.Select(club => club.Name).ToArray());

        var options = await client.GetFromJsonAsync<List<ClubTeamOption>>($"/api/clubs/{entered.ClubId}/teams?tournamentId={tournament.Id}", TestContext.Current.CancellationToken);
        Assert.Equal([("Ohio St A", true), ("Ohio St B", false)], options!.Select(team => (team.Name, team.AlreadyEntered)).ToArray());
    }

    [Fact]
    public async Task Another_organizations_clubs_teams_and_entries_cannot_be_read_or_changed()
    {
        var other = await InsertOtherOrganizationEntryAsync();
        var client = factory.CreateMemberClient();

        Assert.Empty((await client.GetFromJsonAsync<List<ClubDetails>>("/api/clubs?search=secret", TestContext.Current.CancellationToken))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/clubs/{other.ClubId}/teams", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tournaments/{other.TournamentId}/teams", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"/api/tournament-teams/{other.EntryId}",
            new UpdateTournamentTeam("Hijack", other.DivisionId),
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/tournament-teams/{other.EntryId}", TestContext.Current.CancellationToken)).StatusCode);

        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.True(await db.TournamentTeams.IgnoreQueryFilters().AnyAsync(entry => entry.Id == other.EntryId, TestContext.Current.CancellationToken));
        Assert.Equal("Secret A", (await db.Teams.IgnoreQueryFilters().SingleAsync(team => team.ClubId == other.ClubId, TestContext.Current.CancellationToken)).Name);
    }

    [Fact]
    public async Task The_database_refuses_duplicate_clubs_teams_and_entries()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var entry = await AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St A", division.Id);

        await using (var scope = factory.CreateTestOrganizationScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
            db.Clubs.Add(Club.Create(TestOrganizationSeed.Id, " OHIO STATE UNIVERSITY "));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var first = factory.CreateTestOrganizationScope())
        await using (var second = factory.CreateTestOrganizationScope())
        {
            var firstDb = first.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
            var secondDb = second.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
            (await firstDb.Clubs.Include(club => club.Teams).SingleAsync(club => club.Id == entry.ClubId, TestContext.Current.CancellationToken)).AddTeam("Ohio St B");
            (await secondDb.Clubs.Include(club => club.Teams).SingleAsync(club => club.Id == entry.ClubId, TestContext.Current.CancellationToken)).AddTeam("ohio st b");
            await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);

            var race = await Assert.ThrowsAsync<DomainRuleException>(() => new TournamentRepository(secondDb).SaveChangesAsync(CancellationToken.None));
            Assert.Equal(SaveConflict.Message, race.Message);
        }

        var teamB = (await client.GetFromJsonAsync<List<ClubTeamOption>>($"/api/clubs/{entry.ClubId}/teams", TestContext.Current.CancellationToken))!.Single(team => team.Name == "Ohio St B");
        await using (var first = factory.CreateTestOrganizationScope())
        await using (var second = factory.CreateTestOrganizationScope())
        {
            var firstRepository = new TournamentRepository(first.ServiceProvider.GetRequiredService<CourtFlowDbContext>());
            var secondRepository = new TournamentRepository(second.ServiceProvider.GetRequiredService<CourtFlowDbContext>());
            (await firstRepository.GetAsync(tournament.Id, CancellationToken.None))!.EnterTeam(teamB.Id, division.Id);
            (await secondRepository.GetAsync(tournament.Id, CancellationToken.None))!.EnterTeam(teamB.Id, division.Id);
            await firstRepository.SaveChangesAsync(CancellationToken.None);

            var race = await Assert.ThrowsAsync<DomainRuleException>(() => secondRepository.SaveChangesAsync(CancellationToken.None));
            Assert.Equal(SaveConflict.Message, race.Message);
        }
    }

    [Fact]
    public async Task Deleting_a_tournament_removes_its_entries_but_keeps_clubs_and_teams()
    {
        var client = factory.CreateMemberClient();
        var tournament = await TournamentApiTests.CreateAsync(client, TournamentApiTests.Sample());
        var division = await CreateDivisionAsync(client, tournament.Id, "14U Open", "Female");
        var entry = await AddOkAsync(client, tournament.Id, "Ohio State University", "Ohio St U", division.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tournaments/{tournament.Id}", TestContext.Current.CancellationToken)).StatusCode);

        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        Assert.False(await db.TournamentTeams.AnyAsync(item => item.Id == entry.Id, TestContext.Current.CancellationToken));
        Assert.True(await db.Clubs.AnyAsync(club => club.Id == entry.ClubId, TestContext.Current.CancellationToken));
        Assert.True(await db.Teams.AnyAsync(team => team.Id == entry.TeamId, TestContext.Current.CancellationToken));
    }

    internal static async Task<DivisionDetails> CreateDivisionAsync(HttpClient client, Guid tournamentId, string name, string gender)
    {
        var response = await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/divisions", new SaveDivision(name, gender));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DivisionDetails>())!;
    }

    internal static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid tournamentId, string club, string team, Guid? divisionId) =>
        client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/teams", new AddTournamentTeam(club, team, divisionId));

    internal static async Task<TournamentTeamDetails> AddOkAsync(HttpClient client, Guid tournamentId, string club, string team, Guid divisionId)
    {
        var response = await AddAsync(client, tournamentId, club, team, divisionId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TournamentTeamDetails>())!;
    }

    internal static async Task<List<TournamentTeamDetails>> ListAsync(HttpClient client, Guid tournamentId) =>
        (await client.GetFromJsonAsync<List<TournamentTeamDetails>>($"/api/tournaments/{tournamentId}/teams"))!;

    internal static async Task<string?> RejectionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiError>())?.Error;
    }

    private async Task<(Guid TournamentId, Guid DivisionId, Guid ClubId, Guid EntryId)> InsertOtherOrganizationEntryAsync()
    {
        using var scope = factory.CreateTestOrganizationScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new Organization(organizationId, "Other Organization"));
        var tournament = Tournament.Create(organizationId, "Secret Cup", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Denver", 45);
        var division = tournament.AddDivision("14U Open", "Female");
        var club = Club.Create(organizationId, "Secret Club");
        var team = club.AddTeam("Secret A");
        var entry = tournament.EnterTeam(team.Id, division.Id);
        db.Tournaments.Add(tournament);
        db.Clubs.Add(club);
        await db.SaveChangesAsync();
        return (tournament.Id, division.Id, club.Id, entry.Id);
    }

    private sealed record ApiError(string? Error);
}
