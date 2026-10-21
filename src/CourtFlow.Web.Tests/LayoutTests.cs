using Bunit;
using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Layout;
using CourtFlow.Web.Services;
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
        this.AddAccessServices();
    }

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Fact]
    public void Brand_links_home_with_the_logo_and_the_name()
    {
        var cut = RenderLayout();

        var brand = cut.Find("[data-testid=brand-link]");
        Assert.Equal("/tournaments", brand.GetAttribute("href"));
        Assert.Equal(CourtFlowTheme.LogoPath, brand.QuerySelector("img")?.GetAttribute("src"));
        Assert.Contains("CourtFlow", brand.TextContent);
    }

    [Fact]
    public void Menu_button_is_labeled_toggle_navigation()
    {
        var cut = RenderLayout();

        Assert.NotNull(cut.Find("button[aria-label='Toggle navigation']"));
    }

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Editor)]
    [InlineData(OrganizationRole.Viewer)]
    public void Drawer_lists_tournaments_then_teams_then_format_then_sign_out(OrganizationRole role)
    {
        var cut = RenderLayout(TestAccess.Member(role));

        var links = cut.FindAll(".mud-nav-link");
        Assert.Equal(4, links.Count);
        Assert.Equal("Tournaments", links[0].TextContent.Trim());
        Assert.Equal("/tournaments", links[0].GetAttribute("href"));
        Assert.Equal("Teams", links[1].TextContent.Trim());
        Assert.Equal("/teams", links[1].GetAttribute("href"));
        Assert.Equal("Format", links[2].TextContent.Trim());
        Assert.Equal("/format", links[2].GetAttribute("href"));
        Assert.Equal(CfUserMenu.SignOutText, links[3].TextContent.Trim());
        Assert.Equal(TestAccess.Logout, links[3].GetAttribute("href"));
    }

    [Fact]
    public void Theme_toggle_flips_its_label_and_writes_the_store_once()
    {
        var cut = RenderLayout();
        var toggle = cut.Find("[data-testid=theme-toggle]");
        Assert.Equal(CfThemeToggle.SwitchToDark, toggle.GetAttribute("aria-label"));

        toggle.Click();

        cut.WaitForAssertion(() => Assert.Equal(CfThemeToggle.SwitchToLight, cut.Find("[data-testid=theme-toggle]").GetAttribute("aria-label")));
        Assert.Equal([ThemeMode.Dark], _store.Writes);
    }

    [Fact]
    public async Task Theme_toggle_reads_switch_to_light_in_dark_mode()
    {
        _store.Cookie = "dark";
        await _theme.InitializeAsync();

        var cut = RenderLayout();

        Assert.Equal(CfThemeToggle.SwitchToLight, cut.Find("[data-testid=theme-toggle]").GetAttribute("aria-label"));
    }

    [Fact]
    public void No_login_chrome_or_tournament_selector()
    {
        var cut = RenderLayout();

        foreach (var text in new[] { "Sign in", "Login", "Logout", "Select tournament" })
            Assert.DoesNotContain(text, cut.Markup);
        Assert.Empty(cut.FindComponents<CfTournamentSelect>());
    }

    [Fact]
    public void The_user_menu_holds_sign_out()
    {
        var cut = RenderLayout();

        cut.OpenUserMenu();

        cut.WaitForAssertion(() => Assert.Equal(CfUserMenu.SignOutText, cut.Find("[data-testid=sign-out]").TextContent.Trim()));
    }

    [Theory]
    [InlineData("/tournaments")]
    [InlineData("/tournaments/new")]
    [InlineData("/tournaments/3f2b8c8e-2c3a-4b8e-9a1d-5c6d7e8f9a0b")]
    public void Tournaments_link_is_active_on_tournament_pages(string path)
    {
        var cut = RenderLayout();

        Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        cut.WaitForAssertion(() => Assert.Contains("active", cut.Find(".mud-nav-link").ClassList));
    }

    [Theory]
    [InlineData("/not-found")]
    [InlineData("/")]
    public void Tournaments_link_is_not_active_elsewhere(string path)
    {
        var cut = RenderLayout();

        Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        cut.WaitForAssertion(() => Assert.DoesNotContain("active", cut.Find(".mud-nav-link").ClassList));
    }

    [Fact]
    public void Teams_link_is_active_on_the_teams_page()
    {
        var cut = RenderLayout();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/teams");

        cut.WaitForAssertion(() => Assert.Contains("active", cut.FindAll(".mud-nav-link")[1].ClassList));
    }

    [Theory]
    [InlineData("/format")]
    [InlineData("/format/00000000-0000-0000-0000-000000000001/ranking")]
    public void Format_link_is_active_on_format_and_below_it(string path)
    {
        var cut = RenderLayout();

        Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        cut.WaitForAssertion(() => Assert.Contains("active", cut.FindAll(".mud-nav-link")[2].ClassList));
        Assert.DoesNotContain("active", cut.FindAll(".mud-nav-link")[1].ClassList);
    }

    private IRenderedComponent<MainLayout> RenderLayout(MeResponse? me = null)
    {
        this.SignIn(me ?? TestAccess.Member(OrganizationRole.Owner));
        return Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, "<p>content</p>"));
    }
}
