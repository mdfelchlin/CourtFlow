using Bunit;
using CourtFlow.Domain;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class FormatEditorTests : BunitContext, IDisposable
{
    private readonly FakeApi _api = new();
    private readonly FakeTournamentMemory _memory = new();
    private IRenderedComponent<MudDialogProvider> _dialogs = default!;

    public FormatEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<ICourtFlowApi>(_api);
        Services.AddSingleton<ITournamentMemory>(_memory);
        Services.AddSingleton<IRankingRandom>(new FixedRandom(0));
        Services.AddScoped<Feedback>();
        Services.AddScoped<Confirmations>();
    }

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Fact]
    public void The_three_steps_have_addresses_and_the_placeholders_are_empty()
    {
        var (_, division, _) = Seed(("Ohio State University", "Ohio St U"));
        Ready();

        var ranking = RenderEditor(division.Id);
        ranking.WaitForAssertion(() => Assert.Contains("Ohio St U", ranking.Markup));
        Assert.Equal("Back to format", ranking.Find("a[href='/format']").GetAttribute("aria-label"));
        Assert.Contains("City Cup", ranking.Find("[data-testid=tournament-name]").TextContent);
        Assert.Equal("page", Step(ranking, "Ranking").GetAttribute("aria-current"));
        Assert.Null(Step(ranking, "Pools").GetAttribute("aria-current"));
        Assert.EndsWith($"/format/{division.Id}/ranking", Step(ranking, "Ranking").GetAttribute("href"));
        Assert.EndsWith($"/format/{division.Id}/pools", Step(ranking, "Pools").GetAttribute("href"));
        Assert.EndsWith($"/format/{division.Id}/brackets", Step(ranking, "Brackets").GetAttribute("href"));

        var pools = RenderEditor(division.Id, "pools");
        pools.WaitForAssertion(() => Assert.Contains(RankingWording.PoolsLater, pools.Markup));
        Assert.DoesNotContain("Ohio St U", pools.Markup);

        var brackets = RenderEditor(division.Id, "brackets");
        brackets.WaitForAssertion(() => Assert.Contains(RankingWording.BracketsLater, brackets.Markup));
    }

    [Fact]
    public void A_missing_division_says_it_was_not_found()
    {
        Ready();

        var cut = RenderEditor(Guid.NewGuid());

        cut.WaitForAssertion(() => Assert.Contains(RankingWording.DivisionNotFound, cut.Markup));
        Assert.Contains(cut.FindAll("a"), link => link.GetAttribute("href") == "/format");
    }

    [Fact]
    public void Opening_the_editor_remembers_the_tournament()
    {
        var (tournament, division, _) = Seed(("Ohio State University", "Ohio St U"));
        Ready();

        var cut = RenderEditor(division.Id);

        cut.WaitForAssertion(() => Assert.Equal(tournament.Id, _memory.Remembered));
    }

    [Fact]
    public void A_division_with_no_teams_says_to_add_them_first()
    {
        var (_, division, _) = Seed();
        Ready();

        var cut = RenderEditor(division.Id);

        cut.WaitForAssertion(() => Assert.Contains(RankingWording.AddTeamsFirst, cut.Markup));
        Assert.Empty(cut.FindAll("#save-ranking"));
        Assert.Empty(cut.FindAll("#randomize-ranking"));
    }

    [Fact]
    public void Drag_and_a_typed_rank_land_on_the_same_order()
    {
        var (_, division, teams) = Seed(
            ("Alpha Club", "Alpha"),
            ("Alpha Club", "Bravo"),
            ("Zeta Club", "Charlie"));
        Ready();
        var typed = RenderEditor(division.Id);
        typed.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo", "Charlie"], typed.Instance.Order));

        typed.Find($"[data-testid=rank-{teams[2].Id}] input").Change("1");

        typed.WaitForAssertion(() => Assert.Equal(["Charlie", "Alpha", "Bravo"], typed.Instance.Order));
        Assert.Contains("Charlie moved to rank 1 of 3.", typed.Find("[data-testid=ranking-live]").TextContent);

        var dragged = RenderEditor(division.Id);
        dragged.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo", "Charlie"], dragged.Instance.Order));
        dragged.InvokeAsync(() => dragged.Instance.MoveEntryTo(teams[2].Id, 0));
        Assert.Equal(["Charlie", "Alpha", "Bravo"], dragged.Instance.Order);
    }

    [Fact]
    public void An_out_of_range_rank_is_rejected_and_nothing_moves()
    {
        var (_, division, teams) = Seed(("Alpha Club", "Alpha"), ("Zeta Club", "Bravo"));
        Ready();
        var cut = RenderEditor(division.Id);
        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo"], cut.Instance.Order));

        cut.Find($"[data-testid=rank-{teams[0].Id}] input").Change("9");

        cut.WaitForAssertion(() => Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars, snackbar => snackbar.Message == "Enter a rank from 1 to 2,"));
        Assert.Equal(["Alpha", "Bravo"], cut.Instance.Order);
        Assert.Equal("1", cut.Find($"[data-testid=rank-{teams[0].Id}] input").GetAttribute("value"));
    }

    [Fact]
    public void Randomize_uses_the_injected_source_and_changes_the_order()
    {
        var (_, division, _) = Seed(("Alpha Club", "Alpha"), ("Alpha Club", "Bravo"), ("Zeta Club", "Charlie"));
        Ready();
        var cut = RenderEditor(division.Id);
        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo", "Charlie"], cut.Instance.Order));
        var expected = RankingOrder.Shuffle(["Alpha", "Bravo", "Charlie"], new FixedRandom(0));

        cut.Find("#randomize-ranking").Click();

        cut.WaitForAssertion(() => Assert.Equal(expected, cut.Instance.Order));
        Assert.NotEqual(["Alpha", "Bravo", "Charlie"], cut.Instance.Order);
        Assert.Contains(RankingWording.RankingRandomized, cut.Find("[data-testid=ranking-live]").TextContent);
    }

    [Fact]
    public void Randomize_is_disabled_for_one_team()
    {
        var (_, division, _) = Seed(("Ohio State University", "Ohio St U"));
        Ready();
        var cut = RenderEditor(division.Id);

        cut.WaitForAssertion(() => Assert.True(cut.Find("#randomize-ranking").HasAttribute("disabled")));
    }

    [Fact]
    public void Saving_an_unchanged_never_ranked_division_ranks_it_and_discard_reloads()
    {
        var (_, division, teams) = Seed(("Zeta Club", "Alpha"), ("Alpha Club", "alpha"));
        Ready();
        var cut = RenderEditor(division.Id);
        cut.WaitForAssertion(() => Assert.Equal(["alpha", "Alpha"], cut.Instance.Order));
        Assert.Contains(RankingWording.NameOrderAlert, cut.Markup);
        Assert.Contains(RankingWording.NotRanked, cut.Find("[data-testid=ranking-status]").TextContent);
        Assert.False(cut.Find("#save-ranking").HasAttribute("disabled"));
        Assert.True(cut.Find("#discard-ranking").HasAttribute("disabled"));

        cut.Find("#save-ranking").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, _api.SaveRankingCalls));
        Assert.Equal([teams[1].Id, teams[0].Id], _api.LastRankingSaved);
        cut.WaitForAssertion(() => Assert.Equal(RankingWording.Ranked, cut.Find("[data-testid=ranking-status]").TextContent.Trim()));
        Assert.DoesNotContain(RankingWording.NameOrderAlert, cut.Markup);
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars, snackbar => snackbar.Message == Feedback.SavedMessage);

        cut.Find($"[data-testid=rank-{teams[0].Id}] input").Change("1");
        cut.WaitForAssertion(() => Assert.False(cut.Find("#discard-ranking").HasAttribute("disabled")));
        cut.Find("#discard-ranking").Click();
        cut.WaitForAssertion(() => Assert.Equal(["alpha", "Alpha"], cut.Instance.Order));
        Assert.True(cut.Find("#discard-ranking").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Leaving_with_unsaved_changes_asks_and_leaving_a_clean_ranking_does_not()
    {
        var (_, division, teams) = Seed(("Alpha Club", "Alpha"), ("Zeta Club", "Bravo"));
        Ready();
        var nav = Services.GetRequiredService<NavigationManager>();
        var clean = RenderEditor(division.Id);
        clean.WaitForAssertion(() => Assert.Contains("Alpha", clean.Markup));
        Assert.Equal("/format", clean.Find("a[aria-label='Back to format']").GetAttribute("href"));
        nav.NavigateTo("/format");
        Assert.EndsWith("/format", nav.Uri);
        Assert.DoesNotContain(RankingWording.DiscardTitle, _dialogs.Markup);

        var dirty = RenderEditor(division.Id);
        dirty.WaitForAssertion(() => Assert.Contains("Alpha", dirty.Markup));
        dirty.Find($"[data-testid=rank-{teams[1].Id}] input").Change("1");
        dirty.WaitForAssertion(() => Assert.Equal(["Bravo", "Alpha"], dirty.Instance.Order));
        var before = nav.Uri;

        var leaving = dirty.InvokeAsync(() => nav.NavigateTo("/teams"));
        _dialogs.WaitForAssertion(() => Assert.Contains(RankingWording.DiscardTitle, _dialogs.Markup));
        Assert.Contains(RankingWording.DiscardBody, _dialogs.Markup);
        Button(RankingWording.KeepEditing).Click();
        await leaving;
        Assert.Equal(before, nav.Uri);
        Assert.Equal(["Bravo", "Alpha"], dirty.Instance.Order);

        leaving = dirty.InvokeAsync(() => nav.NavigateTo($"/format/{division.Id}/pools"));
        _dialogs.WaitForAssertion(() => Assert.Contains(RankingWording.DiscardConfirm, _dialogs.Markup));
        Button(RankingWording.DiscardConfirm).Click();
        await leaving;
        Assert.EndsWith($"/format/{division.Id}/pools", nav.Uri);
        Assert.Equal(["Alpha", "Bravo"], dirty.Instance.Order);
    }

    [Fact]
    public void A_rejected_save_keeps_the_order_drops_gone_teams_and_appends_new_ones()
    {
        var (tournament, division, teams) = Seed(("Alpha Club", "Alpha"), ("Zeta Club", "Bravo"), ("Kent State", "Charlie"));
        _api.Rank(division.Id, teams.Select(team => team.Id).ToArray());
        Ready();
        var cut = RenderEditor(division.Id);
        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo", "Charlie"], cut.Instance.Order));
        cut.InvokeAsync(() => cut.Instance.MoveEntryTo(teams[2].Id, 0));
        Assert.Equal(["Charlie", "Alpha", "Bravo"], cut.Instance.Order);

        var gone = teams[1];
        _api.Entries[tournament.Id].RemoveAll(entry => entry.Id == gone.Id);
        var added = _api.SeedEntry(tournament.Id, "Purdue", "Delta", division.Id);
        _api.Rank(division.Id, [teams[0].Id, teams[2].Id, added.Id], [added.Id]);
        _api.RejectRankingSaveWith = Tournament.TeamsChanged;

        cut.Find("#save-ranking").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["Charlie", "Alpha", "Delta"], cut.Instance.Order);
            Assert.Contains("data-testid=\"new-team\"", cut.Markup);
        });
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars, snackbar => snackbar.Message == Tournament.TeamsChanged);
        Assert.Null(_api.LastRankingSaved);
        Assert.False(cut.Find("#save-ranking").HasAttribute("disabled"));
    }

    [Fact]
    public void A_viewer_and_an_archived_tournament_are_read_only()
    {
        var (_, division, teams) = Seed(("Ohio State University", "Ohio St U"), ("Kent State", "Kent A"));
        Ready(TestAccess.Member(OrganizationRole.Viewer));

        var viewer = RenderEditor(division.Id);
        viewer.WaitForAssertion(() => Assert.Contains("Ohio St U", viewer.Markup));
        Assert.Empty(viewer.FindAll("#save-ranking"));
        Assert.Empty(viewer.FindAll("#randomize-ranking"));
        Assert.Empty(viewer.FindAll("#discard-ranking"));
        Assert.Empty(viewer.FindAll("input"));
        Assert.DoesNotContain(RankingWording.DragOf("Ohio St U", "Ohio State University"), viewer.Markup);
        Assert.DoesNotContain(RankingWording.NameOrderAlert, viewer.Markup);
        Assert.Empty(viewer.FindAll($"[data-testid=rank-{teams[0].Id}] input"));
    }

    [Fact]
    public void An_archived_tournament_is_read_only()
    {
        Ready();
        var archivedTournament = new TournamentResponse(Guid.NewGuid(), "Old Cup", "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60, TournamentStatus.Archived);
        var archivedDivision = new DivisionResponse(Guid.NewGuid(), archivedTournament.Id, "14U Open", "Female");
        _api.Tournaments.Add(archivedTournament);
        _api.Divisions[archivedTournament.Id] = [archivedDivision];
        var entry = _api.SeedEntry(archivedTournament.Id, "Ohio State University", "Ohio St U", archivedDivision.Id);
        _api.Rank(archivedDivision.Id, [entry.Id]);

        var archived = RenderEditor(archivedDivision.Id);
        archived.WaitForAssertion(() => Assert.Contains(Tournament.ArchivedMessage, archived.Markup));
        Assert.Empty(archived.FindAll("#save-ranking"));
        Assert.Empty(archived.FindAll("input"));
    }

    private void Ready(MeResponse? me = null)
    {
        if (me is not null)
            this.SignIn(me);

        Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
    }

    private IRenderedComponent<FormatEditor> RenderEditor(Guid divisionId, string? step = null)
    {
        return Render<FormatEditor>(parameters =>
        {
            parameters.Add(editor => editor.DivisionId, divisionId);
            if (step is not null)
                parameters.Add(editor => editor.Step, step);
        });
    }

    private (TournamentResponse Tournament, DivisionResponse Division, TournamentTeamResponse[] Teams) Seed(params (string Club, string Team)[] teams)
    {
        var tournament = new TournamentResponse(Guid.NewGuid(), "City Cup", "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60);
        var division = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female");
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [division];
        var entries = teams.Select(team => _api.SeedEntry(tournament.Id, team.Club, team.Team, division.Id)).ToArray();
        return (tournament, division, entries);
    }

    private static AngleSharp.Dom.IElement Step(IRenderedComponent<FormatEditor> cut, string label) =>
        cut.FindAll("a").Single(link => link.TextContent.Trim() == label);

    private AngleSharp.Dom.IElement Button(string text) =>
        _dialogs.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private sealed class FixedRandom(int value) : IRankingRandom
    {
        public int Next(int maxExclusive)
        {
            var chosen = value < 0 ? 0 : value;
            return chosen >= maxExclusive ? maxExclusive - 1 : chosen;
        }
    }
}
