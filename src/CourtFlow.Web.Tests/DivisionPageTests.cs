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

public class DivisionPageTests : BunitContext, IDisposable
{
    private readonly FakeApi _api = new();

    public DivisionPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<ICourtFlowApi>(_api);
        Services.AddSingleton<ITournamentMemory>(new FakeTournamentMemory());
        Services.AddScoped<Feedback>();
        Services.AddScoped<Confirmations>();
        Render<MudPopoverProvider>();
    }

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Fact]
    public void Divisions_stay_hidden_until_the_tournament_is_saved()
    {
        var cut = Render<TournamentEditor>();

        cut.WaitForAssertion(() => Assert.Contains("Details must be saved first.", cut.Markup));
        Assert.DoesNotContain("add-division", cut.Markup);
        Assert.DoesNotContain("Divisions", cut.Markup);
        Assert.Single(cut.FindAll(".mud-alert"));
    }

    [Fact]
    public void Unknown_tournament_shows_not_found_and_no_divisions()
    {
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.Contains(TournamentEditor.NotFoundMessage, cut.Markup));
        Assert.DoesNotContain("add-division", cut.Markup);
        Assert.DoesNotContain(TournamentEditor.EmptyDivisionsMessage, cut.Markup);
    }

    [Fact]
    public void An_empty_section_tells_the_director_to_add_a_division()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));

        cut.WaitForAssertion(() => Assert.Contains(TournamentEditor.EmptyDivisionsMessage, cut.Markup));
    }

    [Fact]
    public void Adding_starts_with_no_gender_and_cancel_saves_nothing()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-division")));

        cut.Find("#add-division").Click();

        dialogs.WaitForAssertion(() => Assert.Contains(DivisionDialog.AddTitle, dialogs.Markup));
        Assert.Contains(DivisionDialog.GenderPrompt, dialogs.Markup);
        Assert.Null(dialogs.FindComponent<CfSelect<string>>().Instance.Value);
        ButtonNamed(dialogs, "Cancel").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(DivisionDialog.AddTitle, dialogs.Markup));
        Assert.Equal(0, _api.CreateDivisionCalls);
        Assert.Empty(Snackbars());
    }

    [Fact]
    public async Task Saving_a_division_closes_the_dialog_and_reloads_the_list()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-division")));

        cut.Find("#add-division").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(DivisionDialog.AddTitle, dialogs.Markup));
        dialogs.Find("[data-testid=division-name] input").Change("14U Open");
        await ChooseGender(dialogs, "Female");
        dialogs.Find("#save-division").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.CreateDivisionCalls);
            Assert.Contains("14U Open", cut.Markup);
            Assert.Contains("Female", cut.Markup);
        });
        Assert.DoesNotContain(DivisionDialog.AddTitle, dialogs.Markup);
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);
    }

    [Fact]
    public void A_blank_name_keeps_the_dialog_open()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-division")));

        cut.Find("#add-division").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(DivisionDialog.AddTitle, dialogs.Markup));
        dialogs.Find("#save-division").Click();

        dialogs.WaitForAssertion(() => AssertSnackbar(Division.NameRequired, Severity.Warning));
        Assert.Contains(DivisionDialog.AddTitle, dialogs.Markup);
        Assert.Equal(0, _api.CreateDivisionCalls);
    }

    [Fact]
    public async Task A_repeated_name_and_gender_keeps_the_dialog_open()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female")];
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("14U Open", cut.Markup));

        cut.Find("#add-division").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(DivisionDialog.AddTitle, dialogs.Markup));
        dialogs.Find("[data-testid=division-name] input").Change(" 14u open ");
        await ChooseGender(dialogs, "Female");
        dialogs.Find("#save-division").Click();

        dialogs.WaitForAssertion(() => AssertSnackbar(Division.DuplicateNameAndGender, Severity.Warning));
        Assert.Contains(DivisionDialog.AddTitle, dialogs.Markup);
        Assert.Equal(0, _api.CreateDivisionCalls);
    }

    [Fact]
    public async Task A_failed_save_keeps_the_dialog_open()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-division")));

        cut.Find("#add-division").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(DivisionDialog.AddTitle, dialogs.Markup));
        dialogs.Find("[data-testid=division-name] input").Change("14U Open");
        await ChooseGender(dialogs, "Male");
        _api.FailWith = "The server is not reachable.";
        dialogs.Find("#save-division").Click();

        dialogs.WaitForAssertion(() => AssertSnackbar("The server is not reachable.", Severity.Error));
        Assert.Contains(DivisionDialog.AddTitle, dialogs.Markup);
    }

    [Fact]
    public async Task Editing_opens_prefilled_and_updates()
    {
        var (tournament, divisionId) = WithDivision("14U Open", "Female");
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("14U Open", cut.Markup));

        cut.Find($"#edit-division-{divisionId}").Click();

        dialogs.WaitForAssertion(() => Assert.Contains(DivisionDialog.EditTitle, dialogs.Markup));
        Assert.Equal("14U Open", dialogs.Find("[data-testid=division-name] input").GetAttribute("value"));
        Assert.Equal("Female", dialogs.FindComponent<CfSelect<string>>().Instance.Value);

        dialogs.Find("[data-testid=division-name] input").Change("16U Open");
        dialogs.Find("#save-division").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.UpdateDivisionCalls);
            Assert.Contains("16U Open", cut.Markup);
        });
        Assert.Equal("16U Open", _api.LastDivisionSaved?.Name);
        Assert.Equal("Female", _api.LastDivisionSaved?.Gender);
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);
    }

    [Fact]
    public void Rows_show_name_and_gender_in_list_order_with_accessible_labels()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] =
        [
            new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Male"),
            new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female"),
            new DivisionResponse(Guid.NewGuid(), tournament.Id, "12U", "Female"),
        ];
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid=division]").Count));
        var rows = cut.FindAll("[data-testid=division]").Select(row => row.TextContent).ToArray();
        Assert.Contains("12U", rows[0]);
        Assert.Contains("Female", rows[0]);
        Assert.Contains("14U Open", rows[1]);
        Assert.Contains("Female", rows[1]);
        Assert.Contains("14U Open", rows[2]);
        Assert.Contains("Male", rows[2]);
        Assert.NotNull(cut.Find("[aria-label='Edit 14U Open, Female']"));
        Assert.NotNull(cut.Find("[aria-label='Delete 14U Open, Male']"));
    }

    [Fact]
    public void Canceling_delete_leaves_the_division_and_the_dialog_has_no_body()
    {
        var (tournament, divisionId) = WithDivision("14U Open", "Female");
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("14U Open", cut.Markup));

        cut.Find($"#delete-division-{divisionId}").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Delete 14U Open (Female)?", dialogs.Markup));
        Assert.DoesNotContain("Only this division is deleted", dialogs.Markup);
        ButtonNamed(dialogs, "Cancel").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Delete 14U Open (Female)?", dialogs.Markup));
        Assert.Equal(0, _api.DeleteDivisionCalls);
        Assert.Contains("14U Open", cut.Markup);
    }

    [Fact]
    public void Confirming_delete_removes_the_division()
    {
        var (tournament, divisionId) = WithDivision("14U Open", "Female");
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("14U Open", cut.Markup));

        cut.Find($"#delete-division-{divisionId}").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Delete 14U Open (Female)?", dialogs.Markup));
        ButtonNamed(dialogs, "Delete").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.DeleteDivisionCalls);
            Assert.Contains(TournamentEditor.EmptyDivisionsMessage, cut.Markup);
        });
        AssertSnackbar(Feedback.DeletedMessage, Severity.Success);
    }

    [Fact]
    public void A_rejected_division_delete_keeps_the_division_and_shows_the_count()
    {
        var (tournament, divisionId) = WithDivision("14U Open", "Female");
        _api.RejectDivisionDeleteWith = Division.HasTeams(3);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("14U Open", cut.Markup));

        cut.Find($"#delete-division-{divisionId}").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Delete 14U Open (Female)?", dialogs.Markup));
        ButtonNamed(dialogs, "Delete").Click();

        cut.WaitForAssertion(() => AssertSnackbar(Division.HasTeams(3), Severity.Warning));
        Assert.Equal(0, _api.DeleteDivisionCalls);
        Assert.Contains("14U Open", cut.Markup);
    }

    [Fact]
    public void Tournament_delete_confirmation_includes_divisions()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentList>();
        cut.WaitForAssertion(() => Assert.Contains("City Cup", cut.Markup));

        cut.Find($"#delete-{tournament.Id}").Click();

        dialogs.WaitForAssertion(() => Assert.Contains(TournamentList.DeleteConsequence, dialogs.Markup));
        Assert.Contains("divisions", dialogs.Markup);
    }

    private static TournamentResponse Sample() =>
        new(Guid.NewGuid(), "City Cup", "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60);

    private (TournamentResponse Tournament, Guid DivisionId) WithDivision(string name, string gender)
    {
        var tournament = Sample();
        var divisionId = Guid.NewGuid();
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [new DivisionResponse(divisionId, tournament.Id, name, gender)];
        return (tournament, divisionId);
    }

    private static async Task ChooseGender(IRenderedComponent<MudDialogProvider> dialogs, string gender)
    {
        var select = dialogs.FindComponent<CfSelect<string>>();
        await dialogs.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(gender));
    }

    private static IElement ButtonNamed(IRenderedComponent<MudDialogProvider> dialogs, string text) =>
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private IEnumerable<Snackbar> Snackbars() => Services.GetRequiredService<ISnackbar>().ShownSnackbars;

    private void AssertSnackbar(string message, Severity severity) =>
        Assert.Contains(Snackbars(), snackbar => snackbar.Message == message && snackbar.Severity == severity);
}
