using System.Reflection;
using Bunit;
using CourtFlow.Web.Layout;
using CourtFlow.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class LandingPageTests : BunitContext, IDisposable
{
    public LandingPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>No <see cref="Services.ICourtFlowApi"/> is registered, so any data call would throw here.</summary>
    [Fact]
    public void Welcome_is_the_only_heading_and_coming_soon_sits_under_it()
    {
        var cut = Render<Landing>();

        var headings = cut.FindAll("h1");
        Assert.Single(headings);
        Assert.Equal("Welcome", headings[0].TextContent.Trim());
        Assert.Contains("Coming soon", cut.Markup);
    }

    [Fact]
    public void Landing_answers_at_the_root_inside_the_public_frame()
    {
        Assert.Equal(["/"], Routes(typeof(Landing)));
        Assert.Equal(typeof(PublicLayout), typeof(Landing).GetCustomAttribute<LayoutAttribute>()?.LayoutType);
    }

    [Fact]
    public void The_tournament_list_moved_off_the_root()
    {
        Assert.Equal(["/tournaments"], Routes(typeof(TournamentList)));
    }

    [Fact]
    public void Only_the_landing_page_claims_the_root()
    {
        var atTheRoot = typeof(Landing).Assembly.GetTypes()
            .Where(type => Routes(type).Contains("/"))
            .ToList();

        Assert.Equal([typeof(Landing)], atTheRoot);
    }

    private static string[] Routes(Type type) =>
        type.GetCustomAttributes<RouteAttribute>().Select(route => route.Template).ToArray();
}
