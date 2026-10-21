using AngleSharp.Dom;

using Bunit;

using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class TeamsPageTests : BunitContext, IDisposable
{
    private readonly FakeApi _api = new();
    private readonly FakeTournamentMemory _memory = new();

    public TeamsPageTests()
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
    public void CountTextMatchesTheSpec()
    {
        Assert.Equal("157 teams", Teams.CountText(157, 157, filtered: false));
        Assert.Equal("1 team", Teams.CountText(1, 1, filtered: false));
        Assert.Equal("50 of 157 teams", Teams.CountText(50, 157, filtered: true));
    }

    [Fact]
    public void ThePickerUsesTheTournamentListOrderAndPrompt()
    {
        _api.Tournaments.Add(Sample(name: "Later Cup"));
        _api.Tournaments.Add(Sample(name: "Earlier Cup"));

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Contains(CfTournamentSelect.Prompt, cut.Markup));
        var picker = cut.FindComponent<CfTournamentSelect>();
        Assert.Equal(["Later Cup", "Earlier Cup"], picker.Instance.Items.Select(item => item.Name).ToArray());
        Assert.Null(picker.Instance.Value);
        Assert.False(cut.FindComponent<MudSelect<TournamentResponse>>().Instance.Clearable);
        Assert.DoesNotContain("Select a tournament<", cut.Markup);

        var add = cut.Find("#add-team");
        var pickerRow = cut.Find("[data-testid=tournament-picker]");
        Assert.Equal("import-teams", add.PreviousElementSibling!.Id);
        Assert.Equal("tournament-picker", add.ParentElement!.PreviousElementSibling!.GetAttribute("data-testid"));

        var label = pickerRow.QuerySelector("label")!;
        Assert.Equal(CfTournamentSelect.Label, label.TextContent.Trim());
        var select = label.NextElementSibling!;
        Assert.Contains("mud-select", select.ClassName);
        Assert.Equal(label.GetAttribute("for"), select.QuerySelector("input")!.Id);
    }

    [Fact]
    public void ARememberedTournamentIsSelectedOnLoad()
    {
        var (tournament, _, _) = WithReadyTournament();
        _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", _api.Divisions[tournament.Id][0].Id);

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Contains("Ohio St U", cut.Markup));
        Assert.Equal(tournament.Id, cut.FindComponent<CfTournamentSelect>().Instance.Value?.Id);
    }

    [Fact]
    public void ARememberedIdThatNoLongerExistsIsCleared()
    {
        _api.Tournaments.Add(Sample());
        _memory.Remembered = Guid.NewGuid();

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Contains(Teams.NothingSelectedMessage, cut.Markup));
        Assert.Null(_memory.Remembered);
        Assert.Equal(1, _memory.Forgets);
        AssertDisabled(cut, "#add-team");
        AssertDisabled(cut, "#import-teams");
    }

    [Fact]
    public async Task PickingATournamentRemembersIt()
    {
        var first = Sample(name: "First Cup");
        var second = Sample(name: "Second Cup");
        _api.Tournaments.Add(first);
        _api.Tournaments.Add(second);
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.Contains(Teams.NothingSelectedMessage, cut.Markup));

        await cut.InvokeAsync(() => cut.FindComponent<CfTournamentSelect>().Instance.ChooseAsync(second));

        cut.WaitForAssertion(() => Assert.Equal(second.Id, _memory.Remembered));
    }

    [Fact]
    public void NoTournamentsShowsTheMessageAndALinkHome()
    {
        var cut = Render<Teams>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Teams.NoTournamentsMessage, cut.Markup);
            Assert.Contains(CfTournamentSelect.Prompt, cut.Markup);
        });
        Assert.Contains(cut.FindAll("a"), link => link.GetAttribute("href") == "/tournaments");
        Assert.Empty(cut.FindAll("#add-team"));
    }

    [Fact]
    public void NothingSelectedDisablesAddAndImport()
    {
        _api.Tournaments.Add(Sample());

        var cut = Render<Teams>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(CfTournamentSelect.Prompt, cut.Markup);
            Assert.Contains(Teams.NothingSelectedMessage, cut.Markup);
        });
        AssertDisabled(cut, "#add-team");
        AssertDisabled(cut, "#import-teams");
    }

    [Fact]
    public void WhileTheTournamentListLoadsTheBarShowsAndTheDropdownDoesNot()
    {
        _api.Tournaments.Add(Sample());
        _api.ListTournamentsGate = new TaskCompletionSource();

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".mud-progress-linear")));
        Assert.Empty(cut.FindAll("label"));
        Assert.DoesNotContain(CfTournamentSelect.Prompt, cut.Markup);

        _api.ListTournamentsGate.SetResult();
        cut.WaitForAssertion(() => Assert.Contains(CfTournamentSelect.Prompt, cut.Markup));
    }

    [Fact]
    public void SharedNamesBothAppearInListOrder()
    {
        _api.Tournaments.Add(Sample(name: "City Cup"));
        _api.Tournaments.Add(Sample(name: "City Cup"));

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindComponent<CfTournamentSelect>().Instance.Items.Count));
        Assert.Equal(["City Cup", "City Cup"], cut.FindComponent<CfTournamentSelect>().Instance.Items.Select(item => item.Name).ToArray());
    }

    [Fact]
    public void WhileLoadingTheBarShowsAndTheTableDoesNot()
    {
        var (tournament, open, _) = WithReadyTournament();
        _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        _api.ListTeamsGate = new TaskCompletionSource();

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=teams-loading]")));
        Assert.Empty(cut.FindAll("[data-testid=team-row]"));

        _api.ListTeamsGate.SetResult();

        cut.WaitForAssertion(() => Assert.Contains("Ohio St U", cut.Markup));
    }

    [Fact]
    public void WithNoDivisionsTheMessageLinksToTheEditor()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        _memory.Remembered = tournament.Id;

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Contains(Teams.NoDivisionsMessage, cut.Markup));
        Assert.Equal($"/tournaments/{tournament.Id}", cut.Find("[data-testid=editor-link]").GetAttribute("href"));
        AssertDisabled(cut, "#add-team");
        AssertDisabled(cut, "#import-teams");
    }

    [Fact]
    public void WithNoTeamsThePageTellsTheDirectorToAddOrImport()
    {
        WithReadyTournament();

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Contains(Teams.NoTeamsMessage, cut.Markup));
        Assert.False(cut.Find("#add-team").HasAttribute("disabled"));
    }

    [Fact]
    public void WithNoMatchesTheTableSaysSo()
    {
        var (tournament, open, _) = WithReadyTournament();
        _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.Contains("Ohio St U", cut.Markup));

        cut.Find("[data-testid=team-search] input").Input("no such team");

        cut.WaitForAssertion(() => Assert.Contains(Teams.NoMatchesMessage, cut.Markup));
        Assert.Contains("0 of 1 team", cut.Find("[data-testid=team-count]").TextContent);
    }

    [Fact]
    public void RowsFollowTheApiOrderAndShowAccessibleLabels()
    {
        var (tournament, open, gold) = WithReadyTournament();
        var first = _api.SeedEntry(tournament.Id, "Zeta Club", "Alpha", open.Id);
        var second = _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", gold.Id);
        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid=team-row]").Count));
        Assert.Contains("14U Open (Female)", cut.Markup);
        Assert.Contains("14U Gold (Female)", cut.Markup);
        Assert.NotNull(cut.Find($"#edit-team-{first.Id}"));
        Assert.Equal("Edit Alpha, Zeta Club", cut.Find($"#edit-team-{first.Id}").GetAttribute("aria-label"));
        Assert.Equal("Delete Ohio St U, Ohio State University", cut.Find($"#delete-team-{second.Id}").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task TheDivisionFilterAndSearchNarrowTheRows()
    {
        var (tournament, open, gold) = WithReadyTournament();
        _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        _api.SeedEntry(tournament.Id, "Kent State University", "Kent A", gold.Id);
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.Equal("2 teams", cut.Find("[data-testid=team-count]").TextContent.Trim()));

        var filter = cut.FindComponents<CfSelect<DivisionResponse>>().Single();
        await cut.InvokeAsync(() => filter.Instance.ValueChanged.InvokeAsync(open));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Ohio St U", cut.Markup);
            Assert.DoesNotContain("Kent A", cut.Markup);
            Assert.Equal("1 of 2 teams", cut.Find("[data-testid=team-count]").TextContent.Trim());
        });

        await cut.InvokeAsync(() =>
            filter.Instance.ValueChanged.InvokeAsync(new DivisionResponse(Guid.Empty, Guid.Empty, Teams.AllDivisions, "")));
        cut.Find("[data-testid=team-search] input").Input("kent");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Kent A", cut.Markup);
            Assert.DoesNotContain("Ohio St U", cut.Find("[data-testid=team-row]").TextContent);
            Assert.Equal("1 of 2 teams", cut.Find("[data-testid=team-count]").TextContent.Trim());
        });
    }

    [Fact]
    public async Task ChangingTheTournamentResetsTheFilterAndSearch()
    {
        var (first, open, _) = WithReadyTournament();
        _api.SeedEntry(first.Id, "Ohio State University", "Ohio St U", open.Id);
        var second = Sample(name: "Other Cup");
        _api.Tournaments.Add(second);
        _api.Divisions[second.Id] = [new DivisionResponse(Guid.NewGuid(), second.Id, "12U", "Male")];
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.Contains("Ohio St U", cut.Markup));
        cut.Find("[data-testid=team-search] input").Input("ohio");

        await cut.InvokeAsync(() => cut.FindComponent<CfTournamentSelect>().Instance.ChooseAsync(second));

        cut.WaitForAssertion(() => Assert.Contains(Teams.NoTeamsMessage, cut.Markup));
        await cut.InvokeAsync(() => cut.FindComponent<CfTournamentSelect>().Instance.ChooseAsync(first));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Ohio St U", cut.Markup);
            Assert.Equal("", cut.Find("[data-testid=team-search] input").GetAttribute("value") ?? "");
            Assert.Equal(Guid.Empty, cut.FindComponents<CfSelect<DivisionResponse>>().Single().Instance.Value?.Id ?? Guid.Empty);
        });
    }

    [Fact]
    public void CancelingDeleteLeavesTheTeam()
    {
        var (tournament, open, _) = WithReadyTournament();
        var entry = _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.Contains("Ohio St U", cut.Markup));

        cut.Find($"#delete-team-{entry.Id}").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Delete Ohio St U (Ohio State University)?", dialogs.Markup));
        Assert.Contains(Teams.DeleteConsequence, dialogs.Markup);
        ButtonNamed(dialogs, "Cancel").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Delete Ohio St U (Ohio State University)?", dialogs.Markup));
        Assert.Equal(0, _api.DeleteTeamCalls);
        Assert.Contains("Ohio St U", cut.Markup);
    }

    [Fact]
    public void ConfirmingDeleteRemovesTheTeamAndShowsDeleted()
    {
        var (tournament, open, _) = WithReadyTournament();
        var entry = _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", open.Id);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.Contains("Ohio St U", cut.Markup));
        var lists = _api.ListTeamsCalls;

        cut.Find($"#delete-team-{entry.Id}").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Delete Ohio St U (Ohio State University)?", dialogs.Markup));
        ButtonNamed(dialogs, "Delete").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.DeleteTeamCalls);
            Assert.True(_api.ListTeamsCalls > lists);
            Assert.Contains(Teams.NoTeamsMessage, cut.Markup);
        });
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars, snackbar => snackbar.Message == Feedback.DeletedMessage);
    }

    private (TournamentResponse Tournament, DivisionResponse Open, DivisionResponse Gold) WithReadyTournament()
    {
        var tournament = Sample();
        var open = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female");
        var gold = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Gold", "Female");
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [open, gold];
        _memory.Remembered = tournament.Id;
        return (tournament, open, gold);
    }

    private static TournamentResponse Sample(Guid? id = null, string name = "City Cup") =>
        new(id ?? Guid.NewGuid(), name, "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60);

    private static IElement ButtonNamed(IRenderedComponent<MudDialogProvider> dialogs, string text) =>
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private static void AssertDisabled(IRenderedComponent<Teams> cut, string selector) =>
        Assert.True(cut.Find(selector).HasAttribute("disabled"));
}