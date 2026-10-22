using Bunit;

using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Layout;
using CourtFlow.Web.Services;
using CourtFlow.Web.Theming;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class PublicLayoutTests : BunitContext, IDisposable
{
    private readonly FakeThemePreferenceStore _publicStore = new();
    private readonly FakeThemePreferenceStore _directorStore = new();
    private readonly PublicThemeState _publicTheme;
    private readonly ThemeState _directorTheme;

    public PublicLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        _publicTheme = new PublicThemeState(_publicStore);
        _directorTheme = new ThemeState(_directorStore);
        Services.AddSingleton(_publicTheme);
        Services.AddSingleton(_directorTheme);
        this.AddAccessServices();
    }

    void IDisposable.Dispose()
    {
        Services.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void BrandLinksToTheLandingPageWithTheLogoAndTheName()
    {
        var cut = RenderLayout();

        var brand = cut.Find("[data-testid=brand-link]");
        Assert.Equal("/", brand.GetAttribute("href"));
        Assert.Equal(CourtFlowTheme.LogoPath, brand.QuerySelector("img")?.GetAttribute("src"));
        Assert.Contains("CourtFlow", brand.TextContent);
    }

    [Fact]
    public void BrandThenTheToggleThenRequestAccessThenLoginFillTheTopBar()
    {
        var cut = RenderLayout();

        var markup = cut.Markup;
        var brand = markup.IndexOf("data-testid=\"brand-link\"", StringComparison.Ordinal);
        var toggle = markup.IndexOf("data-testid=\"theme-toggle\"", StringComparison.Ordinal);
        var request = markup.IndexOf("data-testid=\"request-access\"", StringComparison.Ordinal);
        var login = markup.IndexOf("data-testid=\"login\"", StringComparison.Ordinal);

        Assert.True(brand >= 0 && brand < toggle, "The brand comes before the theme toggle.");
        Assert.True(toggle < request, "The theme toggle comes before Request Access.");
        Assert.True(request < login, "Request Access comes before Login.");
    }

    [Fact]
    public void LoginReadsLoginAndSignsInBackToTheTournamentList()
    {
        var cut = RenderLayout();

        var login = cut.Find("[data-testid=login]");
        Assert.Equal(TestAccess.Login("/tournaments"), login.GetAttribute("href"));
        Assert.Equal(TestAccess.ApiBase + "auth/login?returnUrl=%2Ftournaments", login.GetAttribute("href"));
        Assert.Equal("Login", login.TextContent.Trim());
    }

    [Fact]
    public void RequestAccessGoesToTheRequestPage()
    {
        var cut = RenderLayout();

        Assert.Equal("/request-access", cut.Find("[data-testid=request-access]").GetAttribute("href"));
    }

    [Fact]
    public void SignedInWithAMembershipShowsDashboardOnly()
    {
        var cut = RenderLayout(TestAccess.Member(OrganizationRole.Viewer));

        var dashboard = cut.Find("[data-testid=dashboard]");
        Assert.Equal("/tournaments", dashboard.GetAttribute("href"));
        Assert.Equal(PublicLayout.Dashboard, dashboard.TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-testid=login]"));
        Assert.Empty(cut.FindAll("[data-testid=request-access]"));
        Assert.Empty(cut.FindAll("[data-testid=sign-out]"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SignedInWithoutAMembershipShowsSignOutOnly(bool deactivated)
    {
        var me = deactivated
            ? TestAccess.Member(OrganizationRole.Owner, status: OrganizationStatus.Deactivated)
            : TestAccess.NoMembership();
        var cut = RenderLayout(me);

        var signOut = cut.Find("[data-testid=sign-out]");
        Assert.Equal(TestAccess.Logout, signOut.GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid=request-access]"));
        Assert.Empty(cut.FindAll("[data-testid=login]"));
        Assert.Empty(cut.FindAll("[data-testid=dashboard]"));
    }

    [Fact]
    public void WhileAuthorizingNoButtonsShow()
    {
        this.AddAuthorization().SetAuthorizing();

        var cut = Render<PublicLayout>(parameters => parameters.Add(layout => layout.Body, "<p>content</p>"));

        foreach (var id in new[] { "login", "request-access", "dashboard", "sign-out" })
            Assert.Empty(cut.FindAll($"[data-testid={id}]"));
        Assert.NotEmpty(cut.FindAll("[data-testid=theme-toggle]"));
    }

    [Fact]
    public void ThereIsNoMenuButtonAndNoDrawer()
    {
        var cut = RenderLayout();

        Assert.Empty(cut.FindAll("button[aria-label='Toggle navigation']"));
        Assert.Empty(cut.FindAll(".mud-drawer"));
        Assert.Empty(cut.FindComponents<NavMenu>());
    }

    [Fact]
    public void TheToggleFlipsItsLabelAndWritesOnlyThePublicCookie()
    {
        var cut = RenderLayout();
        var toggle = cut.Find("[data-testid=theme-toggle]");
        Assert.Equal(CfThemeToggle.SwitchToDark, toggle.GetAttribute("aria-label"));

        toggle.Click();

        cut.WaitForAssertion(() => Assert.Equal(CfThemeToggle.SwitchToLight, cut.Find("[data-testid=theme-toggle]").GetAttribute("aria-label")));
        Assert.Equal([ThemeMode.Dark], _publicStore.Writes);
        Assert.Empty(_directorStore.Writes);
        Assert.Equal(ThemeMode.Light, _directorTheme.Mode);
    }

    [Fact]
    public async Task ADarkPublicCookieReadsSwitchToLight()
    {
        _publicStore.Cookie = "dark";
        await _publicTheme.InitializeAsync();

        var cut = RenderLayout();

        Assert.Equal(CfThemeToggle.SwitchToLight, cut.Find("[data-testid=theme-toggle]").GetAttribute("aria-label"));
    }

    private IRenderedComponent<PublicLayout> RenderLayout(MeResponse? me = null)
    {
        this.SignIn(me ?? TestAccess.SignedOut);
        return Render<PublicLayout>(parameters => parameters.Add(layout => layout.Body, "<p>content</p>"));
    }
}