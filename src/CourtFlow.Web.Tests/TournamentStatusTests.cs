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

/// <summary>The lifecycle on screen: the badge, the status bar, and what an archived tournament refuses to offer.</summary>
public class TournamentStatusTests : BunitContext, IDisposable
{
    private readonly FakeApi _api = new();
    private readonly FakeTournamentMemory _memory = new();

    public TournamentStatusTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<ICourtFlowApi>(_api);
        Services.AddSingleton<ITournamentMemory>(_memory);
        Services.AddScoped<Feedback>();
        Services.AddScoped<Confirmations>();
    }

    void IDisposable.Dispose()
    {
        Services.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    // ----- The badge -----

    [Theory]
    [InlineData(TournamentStatus.Unpublished, "Unpublished")]
    [InlineData(TournamentStatus.Published, "Published")]
    [InlineData(TournamentStatus.Archived, "Archived")]
    public void BadgeNamesTheState(TournamentStatus status, string expected)
    {
        var cut = Render<CfStatusBadge>(parameters => parameters.Add(badge => badge.Status, status));

        Assert.Contains(expected, cut.Markup);
    }

    [Fact]
    public void ListShowsEachTournamentState()
    {
        _api.Tournaments.Add(Sample(name: "Winter Cup"));
        _api.Tournaments.Add(Sample(name: "Spring Classic", status: TournamentStatus.Published));
        _api.Tournaments.Add(Sample(name: "Fall Open", status: TournamentStatus.Archived));

        var cut = Render<TournamentList>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindComponents<CfStatusBadge>().Count));
        Assert.Contains("Unpublished", cut.Markup);
        Assert.Contains("Published", cut.Markup);
        Assert.Contains("Archived", cut.Markup);
    }

    // ----- The status bar -----

    [Fact]
    public void AnUnpublishedTournamentOffersOnlyPublish()
    {
        var cut = RenderEditor(Sample());

        Assert.Contains(TournamentStatusBar.UnpublishedText, cut.Markup);
        Assert.NotNull(cut.Find("#publish"));
        Assert.Empty(cut.FindAll("#unpublish"));
        Assert.Empty(cut.FindAll("#archive"));
        Assert.Empty(cut.FindAll("#restore"));
    }

    [Fact]
    public void APublishedTournamentOffersUnpublishAndArchive()
    {
        var cut = RenderEditor(Published());

        Assert.NotNull(cut.Find("#unpublish"));
        Assert.NotNull(cut.Find("#archive"));
        Assert.Empty(cut.FindAll("#publish"));
        Assert.Empty(cut.FindAll("#restore"));
    }

    [Fact]
    public void AnArchivedTournamentOffersOnlyRestore()
    {
        var cut = RenderEditor(Sample(status: TournamentStatus.Archived));

        Assert.Contains(TournamentStatusBar.ArchivedText, cut.Markup);
        Assert.NotNull(cut.Find("#restore"));
        Assert.Empty(cut.FindAll("#publish"));
        Assert.Empty(cut.FindAll("#unpublish"));
        Assert.Empty(cut.FindAll("#archive"));
    }

    [Fact]
    public void APublishedTournamentSaysWhenItWillBeArchived()
    {
        var cut = RenderEditor(Published());

        var text = cut.Find("[data-testid=status-text]").TextContent;
        Assert.Contains("Published on 03/01/2026", text);
        Assert.Contains("archived automatically on 11/26/2026", text);
    }

    [Fact]
    public void PublishingRecordsTheStateAndSaysPublished()
    {
        var cut = RenderEditor(Sample());

        cut.Find("#publish").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#unpublish")));
        Assert.Equal(TournamentStatus.Published, _api.Tournaments[0].Status);
        AssertSnackbar(Feedback.PublishedMessage, Severity.Success);
    }

    [Fact]
    public void RestoringReturnsTheTournamentToUnpublished()
    {
        var cut = RenderEditor(Sample(status: TournamentStatus.Archived));

        cut.Find("#restore").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#publish")));
        Assert.Equal(TournamentStatus.Unpublished, _api.Tournaments[0].Status);
        AssertSnackbar(Feedback.RestoredMessage, Severity.Success);
    }

    [Theory]
    [InlineData("#unpublish", TournamentStatusBar.UnpublishTitle, TournamentStatusBar.UnpublishWarning, TournamentStatusBar.UnpublishConfirmText)]
    [InlineData("#archive", TournamentStatusBar.ArchiveTitle, TournamentStatusBar.ArchiveWarning, TournamentStatusBar.ArchiveConfirmText)]
    public void UnpublishAndArchiveWarnFirstAndCancelingChangesNothing(
        string button,
        string title,
        string warning,
        string confirmText)
    {
        var dialogs = Render<MudDialogProvider>();
        var cut = RenderEditor(Published());

        cut.Find(button).Click();

        dialogs.WaitForAssertion(() => Assert.Contains(title, dialogs.Markup));
        Assert.Contains(warning, dialogs.Markup);
        Assert.Contains(dialogs.FindAll("button"), element => element.TextContent.Trim() == confirmText);

        ButtonNamed(dialogs, Confirmations.CancelText).Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(title, dialogs.Markup));
        Assert.Equal(TournamentStatus.Published, _api.Tournaments[0].Status);
        Assert.Equal(0, _api.TransitionCalls);
    }

    [Fact]
    public void ConfirmingUnpublishHidesTheTournamentFromThePublic()
    {
        var dialogs = Render<MudDialogProvider>();
        var cut = RenderEditor(Published());

        cut.Find("#unpublish").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(TournamentStatusBar.UnpublishTitle, dialogs.Markup));
        ButtonNamed(dialogs, TournamentStatusBar.UnpublishConfirmText).Click();

        cut.WaitForAssertion(() => Assert.Equal(TournamentStatus.Unpublished, _api.Tournaments[0].Status));
        Assert.NotNull(cut.Find("#publish"));
        AssertSnackbar(Feedback.UnpublishedMessage, Severity.Success);
    }

    [Fact]
    public void ConfirmingArchiveLocksTheEditor()
    {
        var dialogs = Render<MudDialogProvider>();
        var cut = RenderEditor(Published());

        cut.Find("#archive").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(TournamentStatusBar.ArchiveTitle, dialogs.Markup));
        ButtonNamed(dialogs, TournamentStatusBar.ArchiveConfirmText).Click();

        cut.WaitForAssertion(() => Assert.Equal(TournamentStatus.Archived, _api.Tournaments[0].Status));
        Assert.NotNull(cut.Find("#restore"));
        Assert.Empty(cut.FindAll("#save-details"));
        AssertSnackbar(Feedback.ArchivedMessage, Severity.Success);
    }

    [Fact]
    public void ARejectedActionWarnsAndReloadsWhatTheApiNowHolds()
    {
        var tournament = Published();
        var dialogs = Render<MudDialogProvider>();
        var cut = RenderEditor(tournament);

        // The tournament was archived elsewhere, most likely by the automatic archive, while this screen sat open.
        _api.Tournaments[0] = tournament with { Status = TournamentStatus.Archived };
        _api.RejectTransitionWith = Tournament.OnlyPublishedCanBeUnpublished;

        cut.Find("#unpublish").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(TournamentStatusBar.UnpublishTitle, dialogs.Markup));
        ButtonNamed(dialogs, TournamentStatusBar.UnpublishConfirmText).Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#restore")));
        AssertSnackbar(Tournament.OnlyPublishedCanBeUnpublished, Severity.Warning);
    }

    // ----- An archived tournament is read-only -----

    [Fact]
    public void AnArchivedEditorDisablesTheFieldsAndHidesSave()
    {
        var cut = RenderEditor(Sample(status: TournamentStatus.Archived));

        Assert.Empty(cut.FindAll("#save-details"));
        foreach (var field in new[] { "name", "start-date", "end-date", "duration" })
            Assert.True(cut.Find($"[data-testid={field}] input").HasAttribute("disabled"), field);

        Assert.True(cut.Find("[data-testid=description] textarea").HasAttribute("disabled"));
    }

    [Fact]
    public void AnArchivedEditorHidesTheVenueAndDivisionButtons()
    {
        var tournament = Sample(status: TournamentStatus.Archived);
        var venueId = Guid.NewGuid();
        var divisionId = Guid.NewGuid();
        _api.Venues[tournament.Id] = [new VenueResponse(venueId, tournament.Id, "Main Gym", "", 2, [])];
        _api.Divisions[tournament.Id] = [new DivisionResponse(divisionId, tournament.Id, "14U", "Girls")];

        var cut = RenderEditor(tournament);

        cut.WaitForAssertion(() => Assert.Contains("Main Gym", cut.Markup));
        Assert.Contains("14U", cut.Markup);
        foreach (var selector in new[]
                 {
                     "#add-venue",
                     "#add-division",
                     $"#edit-venue-{venueId}",
                     $"#delete-venue-{venueId}",
                     $"#edit-division-{divisionId}",
                     $"#delete-division-{divisionId}",
                 })
            Assert.Empty(cut.FindAll(selector));
    }

    [Fact]
    public void AnArchivedTournamentCanStillBeDeletedButAPublishedOneCannot()
    {
        var published = Sample(name: "Spring Classic", status: TournamentStatus.Published);
        var archived = Sample(name: "Fall Open", status: TournamentStatus.Archived);
        _api.Tournaments.Add(published);
        _api.Tournaments.Add(archived);

        var cut = Render<TournamentList>();

        cut.WaitForAssertion(() => Assert.Contains("Fall Open", cut.Markup));
        Assert.True(cut.Find($"#delete-{published.Id}").HasAttribute("disabled"));
        Assert.False(cut.Find($"#delete-{archived.Id}").HasAttribute("disabled"));
        Assert.Contains(
            cut.FindComponents<MudTooltip>(),
            tooltip => tooltip.Instance.Text == TournamentList.DeleteBlockedTooltip);
    }

    [Fact]
    public void DeletingWarnsThatItCannotBeUndone()
    {
        var tournament = Sample(status: TournamentStatus.Archived);
        _api.Tournaments.Add(tournament);
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<TournamentList>();
        cut.WaitForAssertion(() => Assert.Contains("City Cup", cut.Markup));

        cut.Find($"#delete-{tournament.Id}").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("This cannot be undone.", dialogs.Markup));
    }

    [Fact]
    public void TheDropdownMarksAnArchivedTournament()
    {
        var archived = Sample(name: "Fall Open", status: TournamentStatus.Archived);
        _api.Tournaments.Add(Sample(name: "Winter Cup"));
        _api.Tournaments.Add(archived);
        _api.Divisions[archived.Id] = [new DivisionResponse(Guid.NewGuid(), archived.Id, "14U", "Girls")];
        _memory.Remembered = archived.Id;

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Contains("Fall Open (Archived)", cut.Markup));
        Assert.DoesNotContain("Winter Cup (Archived)", cut.Markup);
    }

    [Fact]
    public void AnArchivedTournamentTakesNoNewTeams()
    {
        var tournament = Sample(status: TournamentStatus.Archived);
        var divisionId = Guid.NewGuid();
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [new DivisionResponse(divisionId, tournament.Id, "14U", "Girls")];
        var entry = _api.SeedEntry(tournament.Id, "Rapids", "Rapids 14-1", divisionId);
        _memory.Remembered = tournament.Id;

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Contains(Teams.ArchivedMessage, cut.Markup));
        Assert.Contains("Rapids 14-1", cut.Markup);
        Assert.Empty(cut.FindAll("#add-team"));
        Assert.Empty(cut.FindAll("#import-teams"));
        Assert.Empty(cut.FindAll($"#edit-team-{entry.Id}"));
        Assert.Empty(cut.FindAll($"#delete-team-{entry.Id}"));
    }

    // ----- Helpers -----

    private static TournamentResponse Sample(
        Guid? id = null,
        string name = "City Cup",
        TournamentStatus status = TournamentStatus.Unpublished) =>
        new(id ?? Guid.NewGuid(),
            name,
            "",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3),
            "America/Chicago",
            60,
            status);

    /// <summary>A tournament published at <see cref="FakeApi.PublishedAt"/>, with the dates the API would compute.</summary>
    private static TournamentResponse Published() =>
        Sample(status: TournamentStatus.Published) with
        {
            LastPublishedAt = FakeApi.PublishedAt,
            PublishedOn = new DateOnly(2026, 3, 1),
            ArchivesOn = new DateOnly(2026, 11, 26),
        };

    private IRenderedComponent<TournamentEditor> RenderEditor(TournamentResponse tournament)
    {
        _api.Tournaments.Add(tournament);
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=status-bar]")));
        return cut;
    }

    private static IElement ButtonNamed(IRenderedComponent<MudDialogProvider> dialogs, string text) =>
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private void AssertSnackbar(string message, Severity severity) =>
        Assert.Contains(
            Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message == message && snackbar.Severity == severity);
}