using Bunit;
using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class FormatPageTests : BunitContext, IDisposable
{
    private readonly FakeApi _api = new();
    private readonly FakeTournamentMemory _memory = new();

    public FormatPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<ICourtFlowApi>(_api);
        Services.AddSingleton<ITournamentMemory>(_memory);
        Services.AddScoped<Feedback>();
        Services.AddScoped<Confirmations>();
        Render<MudBlazor.MudPopoverProvider>();
    }

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Fact]
    public void No_tournaments_shows_the_same_message_and_link_as_teams()
    {
        var cut = Render<Format>();

        cut.WaitForAssertion(() => Assert.Contains(Teams.NoTournamentsMessage, cut.Markup));
        Assert.Contains(cut.FindAll("a"), link => link.GetAttribute("href") == "/tournaments");
    }

    [Fact]
    public void Nothing_selected_asks_for_a_tournament()
    {
        _api.Tournaments.Add(Sample());

        var cut = Render<Format>();

        cut.WaitForAssertion(() => Assert.Contains(RankingWording.SelectTournament, cut.Markup));
    }

    [Fact]
    public void While_the_list_loads_the_bar_shows_and_the_table_does_not()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        _memory.Remembered = tournament.Id;
        _api.ListFormatGate = new TaskCompletionSource();

        var cut = Render<Format>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=format-loading]")));
        Assert.Empty(cut.FindAll("[data-testid=format-row]"));

        _api.ListFormatGate.SetResult();
        cut.WaitForAssertion(() => Assert.Contains(RankingWording.AddDivisionFirst, cut.Markup));
    }

    [Fact]
    public void With_no_divisions_the_message_links_to_the_editor()
    {
        var tournament = Sample();
        _api.Tournaments.Add(tournament);
        _memory.Remembered = tournament.Id;

        var cut = Render<Format>();

        cut.WaitForAssertion(() => Assert.Contains(RankingWording.AddDivisionFirst, cut.Markup));
        Assert.Equal($"/tournaments/{tournament.Id}", cut.Find("[data-testid=editor-link]").GetAttribute("href"));
    }

    [Fact]
    public void Rows_follow_division_order_and_an_empty_division_cannot_be_edited()
    {
        var tournament = Sample();
        var open = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female");
        var men = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Male");
        var club = new DivisionResponse(Guid.NewGuid(), tournament.Id, "12U Club", "Female");
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [men, open, club];
        _memory.Remembered = tournament.Id;
        var only = _api.SeedEntry(tournament.Id, "Kent State", "Kent A", club.Id);
        var zeta = _api.SeedEntry(tournament.Id, "Ohio State University", "Zeta", open.Id);
        var alpha = _api.SeedEntry(tournament.Id, "Ohio State University", "alpha", open.Id);
        _api.Rank(open.Id, [alpha.Id, zeta.Id]);
        _api.SeedEntry(tournament.Id, "Purdue", "Boilers", open.Id);
        _api.Rank(open.Id, [alpha.Id, zeta.Id, _api.Entries[tournament.Id][^1].Id], [_api.Entries[tournament.Id][^1].Id]);

        var cut = Render<Format>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid=format-row]").Count));
        var text = cut.Find("table").TextContent;
        Assert.Contains("12U Club (Female)", text);
        Assert.Contains("1 team.", text);
        Assert.Contains(RankingWording.NotRanked, text);
        Assert.Contains("3 teams.", text);
        Assert.Contains("Ranked, 1 new team at the bottom.", text);
        Assert.Contains("0 teams.", text);
        Assert.Contains(RankingWording.AddTeamsFirst, text);
        Assert.True(cut.Find($"#edit-format-{men.Id}").HasAttribute("disabled"));
        Assert.False(cut.Find($"#edit-format-{club.Id}").HasAttribute("disabled"));
        Assert.Equal(RankingWording.EditLabel("12U Club (Female)"), cut.Find($"#edit-format-{club.Id}").GetAttribute("aria-label"));
        Assert.NotEqual(Guid.Empty, only.Id);
    }

    [Fact]
    public void An_archived_tournament_shows_the_note_and_view_buttons()
    {
        var tournament = Sample(TournamentStatus.Archived);
        var division = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female");
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [division];
        _memory.Remembered = tournament.Id;
        _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", division.Id);

        var cut = Render<Format>();

        cut.WaitForAssertion(() => Assert.Contains(Tournament.ArchivedMessage, cut.Markup));
        Assert.Equal(RankingWording.ViewLabel("14U Open (Female)"), cut.Find($"#edit-format-{division.Id}").GetAttribute("aria-label"));
    }

    [Fact]
    public void Edit_opens_the_division_ranking()
    {
        var tournament = Sample();
        var division = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female");
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [division];
        _memory.Remembered = tournament.Id;
        _api.SeedEntry(tournament.Id, "Ohio State University", "Ohio St U", division.Id);

        var cut = Render<Format>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"#edit-format-{division.Id}")));

        cut.Find($"#edit-format-{division.Id}").Click();

        cut.WaitForAssertion(() => Assert.EndsWith($"/format/{division.Id}/ranking", Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri));
    }

    private static TournamentResponse Sample(TournamentStatus status = TournamentStatus.Unpublished) =>
        new(Guid.NewGuid(), "City Cup", "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60, status);
}
