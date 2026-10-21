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

    void IDisposable.Dispose()
    {
        Services.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <summary>No <see cref="Services.ICourtFlowApi"/> is registered, so any data call would throw here.</summary>
    [Fact]
    public void WelcomeIsTheOnlyHeadingAndComingSoonSitsUnderIt()
    {
        var cut = Render<Landing>();

        var headings = cut.FindAll("h1");
        Assert.Single(headings);
        Assert.Equal("Welcome", headings[0].TextContent.Trim());
        Assert.Contains("Coming soon", cut.Markup);
    }

    [Fact]
    public void LandingAnswersAtTheRootInsideThePublicFrame()
    {
        Assert.Equal(["/"], Routes(typeof(Landing)));
        Assert.Equal(typeof(PublicLayout), typeof(Landing).GetCustomAttribute<LayoutAttribute>()?.LayoutType);
    }

    [Fact]
    public void TheTournamentListMovedOffTheRoot()
    {
        Assert.Equal(["/tournaments"], Routes(typeof(TournamentList)));
    }

    [Fact]
    public void OnlyTheLandingPageClaimsTheRoot()
    {
        var atTheRoot = typeof(Landing).Assembly.GetTypes()
            .Where(type => Routes(type).Contains("/"))
            .ToList();

        Assert.Equal([typeof(Landing)], atTheRoot);
    }

    private static string[] Routes(Type type) =>
        type.GetCustomAttributes<RouteAttribute>().Select(route => route.Template).ToArray();
}