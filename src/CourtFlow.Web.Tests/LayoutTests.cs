using Bunit;
using CourtFlow.Web.Layout;
using CourtFlow.Web.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class LayoutTests : BunitContext, IDisposable
{
    private readonly FakeThemePreferenceStore _store = new();
    private readonly ThemeState _theme;

    public LayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        _theme = new ThemeState(_store);
        Services.AddSingleton(_theme);
    }

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Fact]
    public void Brand_links_home_with_the_logo_and_the_name()
    {
        var cut = RenderLayout();

        var brand = cut.Find("[data-testid=brand-link]");
        Assert.Equal("/", brand.GetAttribute("href"));
        Assert.Equal(CourtFlowTheme.LogoPath, brand.QuerySelector("img")?.GetAttribute("src"));
        Assert.Contains("CourtFlow", brand.TextContent);
    }

    [Fact]
    public void Menu_button_is_labeled_toggle_navigation()
    {
        var cut = RenderLayout();

        Assert.NotNull(cut.Find("button[aria-label='Toggle navigation']"));
    }

    [Fact]
    public void Drawer_lists_tournaments_then_teams()
    {
        var cut = RenderLayout();

        var links = cut.FindAll(".mud-nav-link");
        Assert.Equal(2, links.Count);
        Assert.Equal("Tournaments", links[0].TextContent.Trim());
        Assert.Equal("/", links[0].GetAttribute("href"));
        Assert.Equal("Teams", links[1].TextContent.Trim());
        Assert.Equal("/teams", links[1].GetAttribute("href"));
    }

    [Fact]
    public void Theme_toggle_flips_its_label_and_writes_the_store_once()
    {
        var cut = RenderLayout();
        var toggle = cut.Find("[data-testid=theme-toggle]");
        Assert.Equal(MainLayout.SwitchToDark, toggle.GetAttribute("aria-label"));

        toggle.Click();

        cut.WaitForAssertion(() => Assert.Equal(MainLayout.SwitchToLight, cut.Find("[data-testid=theme-toggle]").GetAttribute("aria-label")));
        Assert.Equal([ThemeMode.Dark], _store.Writes);
    }

    [Fact]
    public async Task Theme_toggle_reads_switch_to_light_in_dark_mode()
    {
        _store.Cookie = "dark";
        await _theme.InitializeAsync();

        var cut = RenderLayout();

        Assert.Equal(MainLayout.SwitchToLight, cut.Find("[data-testid=theme-toggle]").GetAttribute("aria-label"));
    }

    [Fact]
    public void No_sign_in_chrome_or_tournament_selector()
    {
        var cut = RenderLayout();

        foreach (var text in new[] { "Sign in", "Sign out", "Login", "Logout", "Select tournament" })
            Assert.DoesNotContain(text, cut.Markup);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/tournaments/new")]
    [InlineData("/tournaments/3f2b8c8e-2c3a-4b8e-9a1d-5c6d7e8f9a0b")]
    public void Tournaments_link_is_active_on_tournament_pages(string path)
    {
        var cut = RenderLayout();

        Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        cut.WaitForAssertion(() => Assert.Contains("active", cut.Find(".mud-nav-link").ClassList));
    }

    [Fact]
    public void Tournaments_link_is_not_active_elsewhere()
    {
        var cut = RenderLayout();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/not-found");

        cut.WaitForAssertion(() => Assert.DoesNotContain("active", cut.Find(".mud-nav-link").ClassList));
    }

    [Fact]
    public void Teams_link_is_active_on_the_teams_page()
    {
        var cut = RenderLayout();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/teams");

        cut.WaitForAssertion(() => Assert.Contains("active", cut.FindAll(".mud-nav-link")[1].ClassList));
    }

    private IRenderedComponent<MainLayout> RenderLayout() =>
        Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, "<p>content</p>"));
}
