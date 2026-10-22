using AngleSharp.Dom;

using Bunit;

using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class TeamDialogTests : BunitContext, IDisposable
{
    private readonly FakeApi _api = new();
    private readonly FakeTournamentMemory _memory = new();
    private IRenderedComponent<Teams>? _page;

    public TeamDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<ICourtFlowApi>(_api);
        Services.AddSingleton<ITournamentMemory>(_memory);
        Services.AddScoped<Feedback>();
        Services.AddScoped<Confirmations>();
        Render<MudPopoverProvider>();
    }

    void IDisposable.Dispose()
    {
        Services.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AddingStartsWithNoDivisionAndADisabledTeamField()
    {
        WithReadyTournament();
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-team")));

        cut.Find("#add-team").Click();

        dialogs.WaitForAssertion(() => Assert.Contains(TeamDialog.AddTitle, dialogs.Markup));
        Assert.Contains(TeamDialog.DivisionPrompt, dialogs.Markup);
        Assert.Null(dialogs.FindComponent<CfSelect<DivisionResponse>>().Instance.Value);
        var fields = dialogs.FindComponents<CfAutocomplete<TeamDialog.Pick>>();
        Assert.False(fields[0].Instance.Disabled);
        Assert.True(fields[1].Instance.Disabled);
    }

    [Fact]
    public async Task ChangingTheClubClearsTheTeam()
    {
        WithReadyTournament();
        var dialogs = OpenAdd();

        await ChooseClub(dialogs, NewClub("Ohio State University"));
        await ChooseTeam(dialogs, NewTeam("Ohio St U"));
        Assert.Equal("Ohio St U", TeamField(dialogs).Instance.Value?.Name);

        await ChooseClub(dialogs, NewClub("Kent State University"));

        dialogs.WaitForAssertion(() => Assert.Null(TeamField(dialogs).Instance.Value));
        Assert.False(TeamField(dialogs).Instance.Disabled);
    }

    [Fact]
    public async Task UnmatchedTextOffersANewClubAndANewTeam()
    {
        var club = new ClubResponse(Guid.NewGuid(), "Ohio State University");
        _api.Clubs.Add(club);
        WithReadyTournament();
        var dialogs = OpenAdd();

        var clubs = (await ClubField(dialogs).Instance.SearchFunc("Brand New", CancellationToken.None)).ToList();
        Assert.Contains(clubs, pick => pick.IsNew && pick.Name == "Brand New");
        Assert.Equal(TeamDialog.NewClubText("Brand New"), TeamDialog.ClubOptionText(clubs.Single(pick => pick.IsNew)));

        await ChooseClub(dialogs, NewClub("Brand New"));
        var teams = (await TeamField(dialogs).Instance.SearchFunc("Gold", CancellationToken.None)).ToList();
        Assert.Contains(teams, pick => pick.IsNew && pick.Name == "Gold");
        Assert.Equal(TeamDialog.NewTeamText("Gold"), TeamDialog.TeamOptionText(teams.Single(pick => pick.IsNew)));
    }

    [Fact]
    public async Task TypingAnExistingTeamNameInADifferentCasePicksThatTeam()
    {
        var (tournament, open, _) = WithReadyTournament();
        var entry = _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        _api.Entries[tournament.Id].Clear();
        var dialogs = OpenAdd();

        await ChooseClub(dialogs, new TeamDialog.Pick(entry.ClubId, "Ohio State University", IsNew: false, AlreadyEntered: false));
        var teams = (await TeamField(dialogs).Instance.SearchFunc("ohio st u", CancellationToken.None)).ToList();

        Assert.DoesNotContain(teams, pick => pick.IsNew);
        Assert.Contains(teams, pick => pick.Id == entry.TeamId && !pick.AlreadyEntered);
    }

    [Fact]
    public async Task AlreadyEnteredTeamsAreListedAndCannotBePicked()
    {
        var (tournament, open, _) = WithReadyTournament();
        var entry = _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        var dialogs = OpenAdd();

        await ChooseClub(dialogs, new TeamDialog.Pick(entry.ClubId, "Ohio State University", IsNew: false, AlreadyEntered: false));
        var teams = (await TeamField(dialogs).Instance.SearchFunc("", CancellationToken.None)).ToList();
        var entered = teams.Single(pick => pick.Id == entry.TeamId);

        Assert.True(entered.AlreadyEntered);
        Assert.True(TeamField(dialogs).Instance.ItemDisabledFunc!(entered));
        Assert.Equal("Ohio St U (already entered)", TeamDialog.TeamOptionText(entered));
    }

    [Fact]
    public async Task SaveSavesClosesReloadsAndShowsSaved()
    {
        var (_, open, _) = WithReadyTournament();
        var dialogs = OpenAdd();
        var lists = _api.ListTeamsCalls;

        await FillAdd(dialogs, "Ohio State University", "Ohio St U", open);
        dialogs.Find("#save-team").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(TeamDialog.AddTitle, dialogs.Markup));
        Assert.Equal(1, _api.AddTeamCalls);
        Assert.Equal(("Ohio State University", "Ohio St U", open.Id),
            (_api.LastTeamAdded?.ClubName, _api.LastTeamAdded?.TeamName, _api.LastTeamAdded?.DivisionId));
        Assert.True(_api.ListTeamsCalls > lists);
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);
        Assert.Contains("Ohio St U", _page!.Markup);
    }

    [Fact]
    public async Task SaveAndAddAnotherKeepsTheClubAndDivisionAndClearsTheTeam()
    {
        var (_, open, _) = WithReadyTournament();
        var dialogs = OpenAdd();

        await FillAdd(dialogs, "Ohio State University", "Ohio St U", open);
        dialogs.Find("#save-team-and-add").Click();

        dialogs.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.AddTeamCalls);
            Assert.Contains(TeamDialog.AddTitle, dialogs.Markup);
            Assert.Equal("Ohio State University", ClubField(dialogs).Instance.Value?.Name);
            Assert.Equal(open.Id, dialogs.FindComponent<CfSelect<DivisionResponse>>().Instance.Value?.Id);
            Assert.Null(TeamField(dialogs).Instance.Value);
            Assert.True(string.IsNullOrEmpty(TeamField(dialogs).Find("input").GetAttribute("value")));
        });
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);

        await ChooseTeam(dialogs, NewTeam("Ohio St B"));
        dialogs.Find("#save-team").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(TeamDialog.AddTitle, dialogs.Markup));
        Assert.Equal(2, _api.AddTeamCalls);
        Assert.Equal("Ohio St B", _api.LastTeamAdded?.TeamName);
        var page = _page ?? throw new InvalidOperationException("Teams is not rendered.");
        page.WaitForAssertion(() => Assert.Contains("Ohio St B", page.Markup));
    }

    [Fact]
    public async Task CancelSavesNothingAndARejectedSaveKeepsTheFields()
    {
        var (_, open, _) = WithReadyTournament();
        var dialogs = OpenAdd();

        await FillAdd(dialogs, "Ohio State University", "Ohio St U", open);
        dialogs.Find("#cancel-team").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(TeamDialog.AddTitle, dialogs.Markup));
        Assert.Equal(0, _api.AddTeamCalls);

        dialogs = OpenAdd();
        await FillAdd(dialogs, "Ohio State University", "Ohio St U", open);
        _api.RejectTeamSaveWith = Tournament.TeamAlreadyEntered;
        dialogs.Find("#save-team").Click();

        dialogs.WaitForAssertion(() => AssertSnackbar(Tournament.TeamAlreadyEntered, Severity.Warning));
        Assert.Contains(TeamDialog.AddTitle, dialogs.Markup);
        Assert.Equal("Ohio State University", ClubField(dialogs).Instance.Value?.Name);
        Assert.Equal("Ohio St U", TeamField(dialogs).Instance.Value?.Name);
        Assert.Equal(0, _api.AddTeamCalls);
    }

    [Fact]
    public async Task AFailedCallLeavesTheDialogOpen()
    {
        var (_, open, _) = WithReadyTournament();
        var dialogs = OpenAdd();
        await FillAdd(dialogs, "Ohio State University", "Ohio St U", open);
        _api.FailWith = "The server is not reachable.";

        dialogs.Find("#save-team").Click();

        dialogs.WaitForAssertion(() => AssertSnackbar("The server is not reachable.", Severity.Error));
        Assert.Contains(TeamDialog.AddTitle, dialogs.Markup);
        Assert.Equal("Ohio St U", TeamField(dialogs).Instance.Value?.Name);
    }

    [Fact]
    public async Task EditShowsTheClubAsReadOnlyAndSavesTheRenameAndMove()
    {
        var (tournament, open, gold) = WithReadyTournament();
        var entry = _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.Contains("Ohio St U", cut.Markup));

        cut.Find($"#edit-team-{entry.Id}").Click();

        dialogs.WaitForAssertion(() => Assert.Contains(TeamDialog.EditTitle, dialogs.Markup));
        Assert.Contains("Ohio State University", dialogs.Find("[data-testid=team-club-readonly]").TextContent);
        Assert.Equal("Ohio St U", dialogs.Find("[data-testid=team-name] input").GetAttribute("value"));
        Assert.Equal(open.Id, dialogs.FindComponent<CfSelect<DivisionResponse>>().Instance.Value?.Id);

        dialogs.Find("[data-testid=team-name] input").Change("Ohio State A");
        await dialogs.InvokeAsync(() =>
            dialogs.FindComponent<CfSelect<DivisionResponse>>().Instance.ValueChanged.InvokeAsync(gold));
        dialogs.Find("#save-team").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(TeamDialog.EditTitle, dialogs.Markup));
        Assert.Equal(1, _api.UpdateTeamCalls);
        Assert.Equal(("Ohio State A", gold.Id), (_api.LastTeamUpdated?.TeamName, _api.LastTeamUpdated?.DivisionId));
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);
    }

    [Fact]
    public void CancelingEditSavesNothing()
    {
        var (tournament, open, _) = WithReadyTournament();
        var entry = _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.Contains("Ohio St U", cut.Markup));

        cut.Find($"#edit-team-{entry.Id}").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(TeamDialog.EditTitle, dialogs.Markup));
        dialogs.Find("[data-testid=team-name] input").Change("Changed");
        dialogs.Find("#cancel-team").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(TeamDialog.EditTitle, dialogs.Markup));
        Assert.Equal(0, _api.UpdateTeamCalls);
    }

    private IRenderedComponent<MudDialogProvider> OpenAdd()
    {
        var dialogs = Render<MudDialogProvider>();
        _page = Render<Teams>();
        _page.WaitForAssertion(() => Assert.NotNull(_page.Find("#add-team")));
        _page.Find("#add-team").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(TeamDialog.AddTitle, dialogs.Markup));
        return dialogs;
    }

    private static async Task FillAdd(IRenderedComponent<MudDialogProvider> dialogs, string club, string team, DivisionResponse division)
    {
        await ChooseClub(dialogs, NewClub(club));
        await ChooseTeam(dialogs, NewTeam(team));
        await dialogs.InvokeAsync(() =>
            dialogs.FindComponent<CfSelect<DivisionResponse>>().Instance.ValueChanged.InvokeAsync(division));
    }

    private static Task ChooseClub(IRenderedComponent<MudDialogProvider> dialogs, TeamDialog.Pick pick) =>
        dialogs.InvokeAsync(() => ClubField(dialogs).Instance.ValueChanged.InvokeAsync(pick));

    private static Task ChooseTeam(IRenderedComponent<MudDialogProvider> dialogs, TeamDialog.Pick pick) =>
        dialogs.InvokeAsync(() => TeamField(dialogs).Instance.ValueChanged.InvokeAsync(pick));

    private static IRenderedComponent<CfAutocomplete<TeamDialog.Pick>> ClubField(IRenderedComponent<MudDialogProvider> dialogs) =>
        dialogs.FindComponents<CfAutocomplete<TeamDialog.Pick>>()[0];

    private static IRenderedComponent<CfAutocomplete<TeamDialog.Pick>> TeamField(IRenderedComponent<MudDialogProvider> dialogs) =>
        dialogs.FindComponents<CfAutocomplete<TeamDialog.Pick>>()[1];

    private static TeamDialog.Pick NewClub(string name) => new(null, name, IsNew: true, AlreadyEntered: false);

    private static TeamDialog.Pick NewTeam(string name) => new(null, name, IsNew: true, AlreadyEntered: false);

    private (TournamentResponse Tournament, DivisionResponse Open, DivisionResponse Gold) WithReadyTournament()
    {
        var tournament = new TournamentResponse(Guid.NewGuid(), "City Cup", "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60);
        var open = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female");
        var gold = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Gold", "Female");
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [open, gold];
        _memory.Remembered = tournament.Id;
        return (tournament, open, gold);
    }

    private void AssertSnackbar(string message, Severity severity) =>
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message == message && snackbar.Severity == severity);
}