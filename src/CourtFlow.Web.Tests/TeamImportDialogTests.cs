using AngleSharp.Dom;
using Bunit;
using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class TeamImportDialogTests : BunitContext, IDisposable
{
    private readonly FakeApi _api = new();
    private readonly FakeTournamentMemory _memory = new();

    public TeamImportDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<ICourtFlowApi>(_api);
        Services.AddSingleton<ITournamentMemory>(_memory);
        Services.AddScoped<Feedback>();
        Services.AddScoped<Confirmations>();
        Render<MudPopoverProvider>();
    }

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Fact]
    public void Opening_shows_the_title_template_link_and_a_disabled_import()
    {
        var dialogs = OpenImport();

        Assert.Contains(TeamImportDialog.Title, dialogs.Markup);
        Assert.Equal(TeamImportDialog.TemplatePath, dialogs.Find("[data-testid=template-link]").GetAttribute("href"));
        Assert.True(dialogs.Find("#import").HasAttribute("disabled"));
        Assert.NotNull(dialogs.Find("[data-testid=browse-file]"));
    }

    [Fact]
    public async Task An_oversized_file_is_refused_before_any_api_call()
    {
        var dialogs = OpenImport();

        await Choose(dialogs, new FakeBrowserFile("big.csv", TeamImportDialog.MaxBytes + 1));

        dialogs.WaitForAssertion(() => Assert.Contains(TeamImportDialog.TooLargeMessage, dialogs.Markup));
        Assert.Equal(0, _api.PreviewCalls);
        Assert.True(dialogs.Find("#import").HasAttribute("disabled"));
        Assert.Empty(dialogs.FindAll("[data-testid=import-rows]"));
    }

    [Fact]
    public async Task A_file_error_from_the_api_shows_its_message_and_no_table()
    {
        _api.ImportPreview = new TeamImportPreviewResponse(
            "The file is empty.",
            [],
            new TeamImportSummaryResponse(0, 0, 0, 0, 0, 0, 0),
            Stale: false);
        var dialogs = OpenImport();

        await Choose(dialogs, SmallCsv());

        dialogs.WaitForAssertion(() => Assert.Contains("The file is empty.", dialogs.Markup));
        Assert.Empty(dialogs.FindAll("[data-testid=import-rows]"));
        Assert.True(dialogs.Find("#import").HasAttribute("disabled"));
        Assert.Equal(1, _api.PreviewCalls);
    }

    [Fact]
    public async Task The_preview_lists_rows_and_hides_rejected_only_until_there_is_a_rejection()
    {
        _api.ImportPreview = Preview(
            Row(2, TeamImportOutcome.AddNewClubAndTeam, null, null),
            Row(3, TeamImportOutcome.Move, "14U Open (Female)", null));
        var dialogs = OpenImport();

        await Choose(dialogs, SmallCsv());

        dialogs.WaitForAssertion(() => Assert.Contains("Add, new club and team", dialogs.Markup));
        Assert.Contains("Move from 14U Open (Female)", dialogs.Markup);
        Assert.Contains(TeamImportDialog.SummaryText(_api.ImportPreview.Summary), dialogs.Find("[data-testid=import-summary]").TextContent);
        Assert.NotNull(dialogs.Find("[data-testid=import-summary]").GetAttribute("aria-live"));
        Assert.Empty(dialogs.FindAll("[data-testid=rejected-only]"));
        Assert.False(dialogs.Find("#import").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Rejected_rows_show_an_icon_and_the_switch_narrows_the_table()
    {
        _api.ImportPreview = Preview(
            Row(2, TeamImportOutcome.AddNewTeam, null, null),
            Row(3, TeamImportOutcome.Rejected, null, "Club is required."));
        var dialogs = OpenImport();
        await Choose(dialogs, SmallCsv());
        dialogs.WaitForAssertion(() => Assert.NotNull(dialogs.Find("[data-testid=rejected-icon]")));

        Assert.Contains("Rejected: Club is required.", dialogs.Markup);
        Assert.True(dialogs.Find("#import").HasAttribute("disabled"));
        Assert.Equal(2, dialogs.FindAll("[data-testid=import-row]").Count);

        await dialogs.InvokeAsync(() => dialogs.FindComponent<CfSwitch>().Instance.ValueChanged.InvokeAsync(true));

        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindAll("[data-testid=import-row]")));
        Assert.Contains("Rejected: Club is required.", dialogs.Markup);
        Assert.DoesNotContain("Add, new team", dialogs.Find("[data-testid=import-rows]").TextContent);
    }

    [Fact]
    public async Task A_stale_response_shows_the_new_preview()
    {
        _api.ImportPreview = Preview(Row(2, TeamImportOutcome.AddNewTeam, null, null));
        _api.ImportResponse = new TeamImportResponse(
            null,
            Preview(Row(2, TeamImportOutcome.Rejected, null, "Add the division \"14U Open (Female)\" to the tournament before importing."))
                with { Stale = true });
        var dialogs = OpenImport();
        await Choose(dialogs, SmallCsv());
        dialogs.WaitForAssertion(() => Assert.False(dialogs.Find("#import").HasAttribute("disabled")));

        var dialog = dialogs.FindComponent<TeamImportDialog>();
        await dialog.InvokeAsync(dialog.FindComponent<CfSaveButton>().Instance.OnSave);

        Assert.Equal(1, _api.ImportCalls);
        dialog.Render();
        Assert.Contains(TeamImportDialog.StaleMessage, dialog.Markup);
        Assert.Contains("Rejected:", dialog.Markup);
        Assert.Contains(TeamImportDialog.Title, dialogs.Markup);
    }

    [Fact]
    public async Task A_successful_import_closes_the_dialog_and_reloads_the_page()
    {
        _api.ImportPreview = Preview(Row(2, TeamImportOutcome.AddNewClubAndTeam, null, null));
        _api.ImportResponse = new TeamImportResponse(new TeamImportResultResponse(157), null);
        var dialogs = OpenImport();
        await Choose(dialogs, SmallCsv());
        dialogs.WaitForAssertion(() => Assert.False(dialogs.Find("#import").HasAttribute("disabled")));
        var lists = _api.ListTeamsCalls;

        dialogs.Find("#import").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(TeamImportDialog.Title, dialogs.Markup));
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message == Feedback.ImportedMessage(157));
        Assert.True(_api.ListTeamsCalls > lists);
    }

    [Fact]
    public void Cancel_calls_no_import()
    {
        var dialogs = OpenImport();

        dialogs.Find("#cancel-import").Click();

        dialogs.WaitForAssertion(() => Assert.DoesNotContain(TeamImportDialog.Title, dialogs.Markup));
        Assert.Equal(0, _api.ImportCalls);
    }

    private IRenderedComponent<MudDialogProvider> OpenImport()
    {
        WithReadyTournament();
        var dialogs = Render<MudDialogProvider>();
        var cut = Render<Teams>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#import-teams")));
        cut.Find("#import-teams").Click();
        dialogs.WaitForAssertion(() => Assert.Contains(TeamImportDialog.Title, dialogs.Markup));
        return dialogs;
    }

    private static Task Choose(IRenderedComponent<MudDialogProvider> dialogs, IBrowserFile file) =>
        dialogs.InvokeAsync(() => dialogs.FindComponent<CfFileDropZone>().Instance.FileChosen.InvokeAsync(file));

    private (TournamentResponse Tournament, DivisionResponse Open, DivisionResponse Gold) WithReadyTournament()
    {
        if (_api.Tournaments.Count > 0)
            return (_api.Tournaments[0], _api.Divisions[_api.Tournaments[0].Id][0], _api.Divisions[_api.Tournaments[0].Id][1]);

        var tournament = new TournamentResponse(Guid.NewGuid(), "City Cup", "", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Chicago", 60);
        var open = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Open", "Female");
        var gold = new DivisionResponse(Guid.NewGuid(), tournament.Id, "14U Gold", "Female");
        _api.Tournaments.Add(tournament);
        _api.Divisions[tournament.Id] = [open, gold];
        _memory.Remembered = tournament.Id;
        return (tournament, open, gold);
    }

    private static TeamImportPreviewResponse Preview(params TeamImportRowResponse[] rows) =>
        new(null, rows, new TeamImportSummaryResponse(rows.Length, 1, 1, 1, rows.Count(row => row.Outcome == TeamImportOutcome.Move), 0, rows.Count(row => row.Outcome == TeamImportOutcome.Rejected)), false);

    private static TeamImportRowResponse Row(int line, TeamImportOutcome outcome, string? from, string? message) =>
        new(line, "Ohio State University", "Ohio St U", "14U Open", "F", outcome, from, message);

    private static FakeBrowserFile SmallCsv() => new("teams.csv", 40, "Club,Team,Division,Gender\nA,B,C,F\n"u8.ToArray());

    private sealed class FakeBrowserFile(string name, long size, byte[]? content = null) : IBrowserFile
    {
        private readonly byte[] _content = content ?? [];

        public string Name { get; } = name;
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UnixEpoch;
        public long Size { get; } = size;
        public string ContentType => "text/csv";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(_content);
    }
}
