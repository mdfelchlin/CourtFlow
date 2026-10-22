using Bunit;

using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;

namespace CourtFlow.Web.Tests;

/// <summary>A viewer sees what an owner sees on an archived tournament, minus the status actions.</summary>
public class ViewerReadOnlyTests : AccessTestContext
{
    // ----- Tournament list -----

    [Fact]
    public void WithNothingPublishedAViewerSeesSoAndNoAddButton()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));

        var cut = Render<TournamentList>();

        cut.WaitForAssertion(() => Assert.Equal(
            TournamentList.NothingPublishedMessage,
            cut.Find("[data-testid=empty-tournaments]").TextContent.Trim()));
        Assert.Empty(cut.FindAll("#add-tournament"));
    }

    [Fact]
    public void APublishedRowHasNoEditOrDeleteForAViewer()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));
        var tournament = Published();
        Api.Tournaments.Add(tournament);

        var cut = Render<TournamentList>();

        cut.WaitForAssertion(() => Assert.Contains("City Cup", cut.Markup));
        Assert.Empty(cut.FindAll($"#edit-{tournament.Id}"));
        Assert.Empty(cut.FindAll($"#delete-{tournament.Id}"));
        Assert.NotNull(cut.Find($"#view-{tournament.Id}"));
    }

    // ----- Tournament page -----

    [Fact]
    public void EveryFieldIsDisabledAndVenuesAndDivisionsHaveNoControls()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));
        var tournament = Published();
        var venueId = Guid.NewGuid();
        var divisionId = Guid.NewGuid();
        Api.Venues[tournament.Id] = [new VenueResponse(venueId, tournament.Id, "Main Gym", "", 2, [])];
        Api.Divisions[tournament.Id] = [new DivisionResponse(divisionId, tournament.Id, "14U", "Girls")];

        var cut = RenderEditor(tournament);

        cut.WaitForAssertion(() => Assert.Contains("Main Gym", cut.Markup));
        Assert.Empty(cut.FindAll("#save-details"));
        foreach (var field in new[] { "name", "start-date", "end-date", "duration" })
            Assert.True(cut.Find($"[data-testid={field}] input").HasAttribute("disabled"), field);
        Assert.True(cut.Find("[data-testid=description] textarea").HasAttribute("disabled"));
        foreach (var selector in new[]
                 {
                     "#add-venue", "#add-division",
                     $"#edit-venue-{venueId}", $"#delete-venue-{venueId}",
                     $"#edit-division-{divisionId}", $"#delete-division-{divisionId}",
                 })
            Assert.Empty(cut.FindAll(selector));
    }

    [Fact]
    public void TheStatusBarShowsThePublishedTextWithoutActions()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));

        var cut = RenderEditor(Published());

        Assert.Contains("Published on 03/01/2026", cut.Find("[data-testid=status-text]").TextContent);
        foreach (var button in new[] { "#publish", "#unpublish", "#archive", "#restore" })
            Assert.Empty(cut.FindAll(button));
    }

    [Fact]
    public void AViewerOnNewTournamentSeesNotFound()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));

        var cut = Render<TournamentEditor>();

        Assert.NotNull(cut.Find("[data-testid=not-found-content]"));
        Assert.Empty(cut.FindAll("#save-details"));
    }

    // ----- Teams page -----

    [Fact]
    public void AViewerHasNoTeamControlsAndNoArchivedNote()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));
        var tournament = Published();
        var divisionId = Guid.NewGuid();
        Api.Tournaments.Add(tournament);
        Api.Divisions[tournament.Id] = [new DivisionResponse(divisionId, tournament.Id, "14U", "Girls")];
        var entry = Api.SeedEntry(tournament.Id, "Rapids", "Rapids 14-1", divisionId);
        Memory.Remembered = tournament.Id;

        var cut = Render<Teams>();

        cut.WaitForAssertion(() => Assert.Contains("Rapids 14-1", cut.Markup));
        Assert.Empty(cut.FindAll("#add-team"));
        Assert.Empty(cut.FindAll("#import-teams"));
        Assert.Empty(cut.FindAll($"#edit-team-{entry.Id}"));
        Assert.Empty(cut.FindAll($"#delete-team-{entry.Id}"));
        Assert.Empty(cut.FindAll("[data-testid=archived-note]"));
    }

    // ----- Owners and editors keep their controls -----

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Editor)]
    public void OwnersAndEditorsKeepEveryControl(OrganizationRole role)
    {
        SignInAs(TestAccess.Member(role));
        var tournament = Published();

        var editor = RenderEditor(tournament);
        Assert.NotNull(editor.Find("#save-details"));
        Assert.NotNull(editor.Find("#add-venue"));
        Assert.NotNull(editor.Find("#unpublish"));
        Assert.NotNull(editor.Find("#archive"));

        var list = Render<TournamentList>();
        list.WaitForAssertion(() => Assert.NotNull(list.Find("#add-tournament")));
        Assert.NotNull(list.Find($"#edit-{tournament.Id}"));
    }

    private static TournamentResponse Published() =>
        new(Guid.NewGuid(), "City Cup", "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60, TournamentStatus.Published)
        {
            LastPublishedAt = FakeApi.PublishedAt,
            PublishedOn = new DateOnly(2026, 3, 1),
            ArchivesOn = new DateOnly(2026, 11, 26),
        };

    private IRenderedComponent<TournamentEditor> RenderEditor(TournamentResponse tournament)
    {
        if (!Api.Tournaments.Contains(tournament))
            Api.Tournaments.Add(tournament);
        var cut = Render<TournamentEditor>(parameters => parameters.Add(page => page.Id, tournament.Id));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=status-bar]")));
        return cut;
    }
}