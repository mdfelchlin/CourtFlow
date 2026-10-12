using AngleSharp.Dom;
using Bunit;
using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class TournamentPageTests : BunitContext, IDisposable
{
    private readonly FakeApi _api = new();
    private readonly FakeTournamentMemory _memory = new();
    private readonly IRenderedComponent<MudPopoverProvider> _popovers;

    public TournamentPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<ICourtFlowApi>(_api);
        Services.AddSingleton<ITournamentMemory>(_memory);
        Services.AddScoped<Feedback>();
        Services.AddScoped<Confirmations>();
        _popovers = Render<MudPopoverProvider>();
    }

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    // ----- Tournament list -----

    [Fact]
    public void Empty_list_tells_the_director_to_add_a_tournament()
    {
        var cut = Render<TournamentList>();

        cut.WaitForAssertion(() => Assert.Contains(TournamentList.EmptyMessage, cut.Markup));
    }

    [Fact]
    public void List_shows_name_dates_time_zone_and_duration()
    {
        _api.Tournaments.Add(Sample());

        var cut = Render<TournamentList>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("City Cup", cut.Markup);
            Assert.Contains("09/01/2026", cut.Markup);
            Assert.Contains("09/03/2026", cut.Markup);
            Assert.Contains("Central", cut.Markup);
            Assert.Contains("60 min", cut.Markup);
        });
    }

    [Fact]
    public void List_renders_one_h1()
    {
        var cut = Render<TournamentList>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("h1")));
    }

    [Fact]
    public void Add_opens_the_new_tournament_editor()
    {
        var cut = Render<TournamentList>();
        cut.WaitForState(() => cut.Find("#add-tournament") is not null);

        cut.Find("#add-tournament").Click();

        Assert.EndsWith("/tournaments/new", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Canceling_delete_leaves_the_tournament()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentList>();
        cut.WaitForAssertion(() => Assert.Contains("City Cup", cut.Markup));

        cut.Find($"#delete-{tournament.Id}").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Delete City Cup?", dialogs.Markup));
        Assert.Contains(TournamentList.DeleteConsequence, dialogs.Markup);
        Assert.NotNull(dialogs.Find("[role=dialog]"));

        ButtonNamed(dialogs, "Cancel").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Delete City Cup?", dialogs.Markup));
        Assert.Equal(0, _api.DeleteTournamentCalls);
        Assert.Contains("City Cup", cut.Markup);
        Assert.Empty(Snackbars());
    }

    [Fact]
    public void Confirming_delete_removes_the_tournament_and_says_deleted()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentList>();
        cut.WaitForAssertion(() => Assert.Contains("City Cup", cut.Markup));

        cut.Find($"#delete-{tournament.Id}").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Delete City Cup?", dialogs.Markup));
        ButtonNamed(dialogs, "Delete").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.DeleteTournamentCalls);
            Assert.Contains(TournamentList.EmptyMessage, cut.Markup);
        });
        AssertSnackbar(Feedback.DeletedMessage, Severity.Success);
    }

    // ----- Tournament editor -----

    [Fact]
    public void Create_and_edit_use_the_same_editor()
    {
        var create = Render<TournamentEditor>();
        create.WaitForAssertion(() => Assert.Contains("Details", create.Markup));

        var edit = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, Guid.NewGuid()));
        edit.WaitForAssertion(() => Assert.Contains(TournamentEditor.NotFoundMessage, edit.Markup));
    }

    [Fact]
    public void Editor_renders_one_h1()
    {
        var cut = Render<TournamentEditor>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("h1")));
    }

    [Fact]
    public void Editor_shows_a_progress_bar_and_no_form_until_the_tournament_loads()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        _api.GetTournamentGate = new TaskCompletionSource();

        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=editor-loading]")));
        Assert.Empty(cut.FindAll("#save-details"));

        _api.GetTournamentGate.SetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("#save-details"));
            Assert.Empty(cut.FindAll("[data-testid=editor-loading]"));
        });
    }

    [Fact]
    public void Venues_are_unavailable_until_details_are_saved()
    {
        var cut = Render<TournamentEditor>();

        cut.WaitForAssertion(() => Assert.Contains("Details must be saved first.", cut.Markup));
        Assert.DoesNotContain("add-venue", cut.Markup);
    }

    [Fact]
    public async Task Saving_a_create_opens_the_new_tournament_and_says_saved()
    {
        var cut = Render<TournamentEditor>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#save-details")));
        await Fill(cut, "City Cup", "09/01/2026", "09/03/2026");

        cut.Find("#save-details").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, _api.CreateCalls));
        Assert.Equal(new DateOnly(2026, 9, 1), _api.LastSaved!.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 3), _api.LastSaved.EndDate);
        Assert.Contains($"/tournaments/{_api.Tournaments[0].Id}", Services.GetRequiredService<NavigationManager>().Uri);
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);
    }

    [Fact]
    public async Task Saving_an_edit_stays_on_that_tournament()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/tournaments/{tournament.Id}");
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("City Cup", cut.Markup));
        var uri = navigation.Uri;

        await Fill(cut, "Updated Cup", "09/01/2026", "09/03/2026");
        cut.Find("#save-details").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, _api.UpdateCalls));
        Assert.Equal(uri, navigation.Uri);
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);
    }

    [Fact]
    public void Unknown_tournament_says_it_was_not_found_and_links_home()
    {
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.Contains(TournamentEditor.NotFoundMessage, cut.Markup));
        Assert.Contains(cut.FindAll("a"), link => link.GetAttribute("href") == "/");
    }

    [Fact]
    public void Not_found_page_links_home()
    {
        var cut = Render<NotFound>();

        Assert.Contains("Page not found", cut.Find("h1").TextContent);
        Assert.Contains(cut.FindAll("a"), link => link.GetAttribute("href") == "/");
    }

    [Fact]
    public async Task Time_zone_picker_lists_only_the_us_zones()
    {
        var cut = Render<TournamentEditor>();
        cut.WaitForAssertion(() => Assert.Contains("Time zone", cut.Markup));

        var select = cut.FindComponent<MudSelect<string>>();
        await cut.InvokeAsync(() => select.Instance.OpenMenu());

        _popovers.WaitForAssertion(() =>
        {
            foreach (var label in new[] { "Eastern", "Central", "Mountain", "Arizona", "Pacific", "Alaska", "Hawaii" })
                Assert.Contains(label, _popovers.Markup);
        });
        Assert.DoesNotContain("UTC", _popovers.Markup);
        Assert.Equal(7, cut.FindComponent<CfSelect<string>>().Instance.Items.Count());
    }

    [Fact]
    public async Task Warnings_show_and_save_still_runs()
    {
        _api.Tournaments.Add(Sample(Guid.NewGuid(), "City Cup"));
        var cut = Render<TournamentEditor>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#save-details")));

        await Fill(cut, "  city cup ", "09/01/2026", "09/06/2026");

        Assert.Contains("more than five calendar days", cut.Markup);
        Assert.Contains("Another tournament already has this name.", cut.Markup);

        cut.Find("#save-details").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, _api.CreateCalls));
    }

    [Fact]
    public void Editing_does_not_warn_about_the_tournaments_own_name()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));

        cut.WaitForAssertion(() => Assert.Contains("City Cup", cut.Markup));
        Assert.DoesNotContain("Another tournament already has this name.", cut.Markup);
    }

    [Fact]
    public void Opening_an_existing_tournament_remembers_it()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);

        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));

        cut.WaitForAssertion(() => Assert.Equal(tournament.Id, _memory.Remembered));
    }

    [Fact]
    public async Task September_1_through_5_does_not_warn()
    {
        var cut = Render<TournamentEditor>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#save-details")));

        await Fill(cut, "City Cup", "09/01/2026", "09/05/2026");

        Assert.DoesNotContain("more than five calendar days", cut.Markup);
    }

    [Fact]
    public void A_rejected_save_shows_the_rule_as_a_warning_and_no_inline_error()
    {
        var cut = Render<TournamentEditor>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#save-details")));

        cut.Find("#save-details").Click();

        cut.WaitForAssertion(() => AssertSnackbar("Tournament name is required.", Severity.Warning));
        Assert.Equal(0, _api.CreateCalls);
        Assert.DoesNotContain("Tournament name is required.", cut.Markup);
    }

    [Fact]
    public async Task A_failed_save_shows_an_error_and_keeps_the_values()
    {
        var cut = Render<TournamentEditor>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#save-details")));
        await Fill(cut, "City Cup", "09/01/2026", "09/03/2026");
        _api.FailWith = "The server is not reachable.";

        cut.Find("#save-details").Click();

        cut.WaitForAssertion(() => AssertSnackbar("The server is not reachable.", Severity.Error));
        Assert.Equal(0, _api.CreateCalls);
        Assert.Equal("City Cup", cut.Find("[data-testid=name] input").GetAttribute("value"));
    }

    // ----- Venues -----

    [Fact]
    public void Empty_venue_list_tells_the_director_to_add_one()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);

        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));

        cut.WaitForAssertion(() => Assert.Contains(TournamentEditor.EmptyVenuesMessage, cut.Markup));
    }

    [Fact]
    public void Canceling_venue_delete_leaves_the_venue()
    {
        var (tournament, venueId) = WithVenue();
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("Main Gym", cut.Markup));

        cut.Find($"#delete-venue-{venueId}").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Delete Main Gym?", dialogs.Markup));
        Assert.Contains(TournamentEditor.DeleteVenueConsequence, dialogs.Markup);

        ButtonNamed(dialogs, "Cancel").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Delete Main Gym?", dialogs.Markup));
        Assert.Equal(0, _api.DeleteVenueCalls);
        Assert.Contains("Main Gym", cut.Markup);
    }

    [Fact]
    public void Confirming_venue_delete_removes_the_venue_and_says_deleted()
    {
        var (tournament, venueId) = WithVenue();
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("Main Gym", cut.Markup));

        cut.Find($"#delete-venue-{venueId}").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Delete Main Gym?", dialogs.Markup));
        ButtonNamed(dialogs, "Delete").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.DeleteVenueCalls);
            Assert.DoesNotContain("Main Gym", cut.Markup);
        });
        AssertSnackbar(Feedback.DeletedMessage, Severity.Success);
    }

    [Fact]
    public void Adding_a_venue_saves_closes_and_reloads()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-venue")));

        cut.Find("#add-venue").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(VenueDialog.AddTitle, dialogs.Markup));
        Assert.NotNull(dialogs.Find("[role=dialog]"));

        dialogs.Find("[data-testid=venue-name] input").Change("Main Gym");
        dialogs.Find("[data-testid=venue-address] input").Change("1 Court St");
        dialogs.Find("[data-testid=venue-court-count] input").Change("2");
        dialogs.Find("#save-venue").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.CreateVenueCalls);
            Assert.Contains("Main Gym", cut.Markup);
            Assert.Contains("1 Court St", cut.Markup);
            Assert.Contains("2 courts", cut.Markup);
        });
        dialogs.WaitForAssertion(() => Assert.DoesNotContain(VenueDialog.AddTitle, dialogs.Markup));
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);
    }

    [Fact]
    public void Canceling_the_venue_dialog_saves_nothing()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-venue")));

        cut.Find("#add-venue").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(VenueDialog.AddTitle, dialogs.Markup));
        dialogs.Find("[data-testid=venue-name] input").Change("Main Gym");
        dialogs.Find("#cancel-venue").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(VenueDialog.AddTitle, dialogs.Markup));
        Assert.Equal(0, _api.CreateVenueCalls);
        Assert.Empty(Snackbars());
    }

    [Fact]
    public void A_blank_venue_name_keeps_the_dialog_open_with_a_warning()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-venue")));

        cut.Find("#add-venue").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(VenueDialog.AddTitle, dialogs.Markup));
        dialogs.Find("[data-testid=venue-court-count] input").Change("2");
        dialogs.Find("#save-venue").Click();

        dialogs.WaitForAssertion(() => AssertSnackbar("Venue name is required.", Severity.Warning));
        Assert.Contains(VenueDialog.AddTitle, dialogs.Markup);
        Assert.Equal(0, _api.CreateVenueCalls);
    }

    [Fact]
    public void A_failed_venue_save_keeps_the_dialog_open_with_an_error()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#add-venue")));

        cut.Find("#add-venue").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(VenueDialog.AddTitle, dialogs.Markup));
        dialogs.Find("[data-testid=venue-name] input").Change("Main Gym");
        dialogs.Find("[data-testid=venue-court-count] input").Change("2");
        _api.FailWith = "The server is not reachable.";
        dialogs.Find("#save-venue").Click();

        dialogs.WaitForAssertion(() => AssertSnackbar("The server is not reachable.", Severity.Error));
        Assert.Contains(VenueDialog.AddTitle, dialogs.Markup);
        Assert.Equal("Main Gym", dialogs.Find("[data-testid=venue-name] input").GetAttribute("value"));
    }

    [Fact]
    public void Editing_a_venue_opens_prefilled_and_updates()
    {
        var (tournament, venueId) = WithVenue();
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.Contains("Main Gym", cut.Markup));

        cut.Find($"#edit-venue-{venueId}").Click();

        dialogs.WaitForAssertion(() => Assert.Contains(VenueDialog.EditTitle, dialogs.Markup));
        Assert.Equal("Main Gym", dialogs.Find("[data-testid=venue-name] input").GetAttribute("value"));
        Assert.Equal("2", dialogs.Find("[data-testid=venue-court-count] input").GetAttribute("value"));

        dialogs.Find("[data-testid=venue-name] input").Change("North Gym");
        dialogs.Find("#save-venue").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, _api.UpdateVenueCalls);
            Assert.Contains("North Gym", cut.Markup);
        });
        Assert.Equal("North Gym", _api.LastVenueSaved?.Name);
        AssertSnackbar(Feedback.SavedMessage, Severity.Success);
    }

    // ----- Helpers -----

    private static TournamentResponse Sample(Guid? id = null, string name = "City Cup") =>
        new(id ?? Guid.NewGuid(), name, "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60);

    private (TournamentResponse Tournament, Guid VenueId) WithVenue()
    {
        var tournament = Sample();
        var venueId = Guid.NewGuid();
        _api.Tournaments.Add(tournament);
        _api.Venues[tournament.Id] = [new VenueResponse(venueId, tournament.Id, "Main Gym", "", 2, [])];
        return (tournament, venueId);
    }

    private static async Task Fill(IRenderedComponent<TournamentEditor> cut, string name, string start, string end)
    {
        cut.Find("[data-testid=name] input").Change(name);
        cut.Find("[data-testid=start-date] input").Change(start);
        cut.Find("[data-testid=end-date] input").Change(end);
        cut.Find("[data-testid=duration] input").Change("60");

        var timeZone = cut.FindComponent<CfSelect<string>>();
        await cut.InvokeAsync(() => timeZone.Instance.ValueChanged.InvokeAsync("America/Chicago"));
    }

    private static IElement ButtonNamed(IRenderedComponent<MudDialogProvider> dialogs, string text) =>
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private IEnumerable<Snackbar> Snackbars() => Services.GetRequiredService<ISnackbar>().ShownSnackbars;

    private void AssertSnackbar(string message, Severity severity) =>
        Assert.Contains(Snackbars(), snackbar => snackbar.Message == message && snackbar.Severity == severity);
}
