using Bunit;

using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Layout;

namespace CourtFlow.Web.Tests;

public class NavMenuTests : AccessTestContext
{
    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Editor)]
    [InlineData(OrganizationRole.Viewer)]
    public void TournamentsAndTeamsShowForEveryRoleWithoutTheOperatorGroup(OrganizationRole role)
    {
        SignInAs(TestAccess.Member(role));

        var cut = Render<NavMenu>();

        Assert.Equal(["/tournaments", "/teams", "/format"], PageLinks(cut));
        Assert.Empty(cut.FindAll("[data-testid=operator-group]"));
    }

    [Fact]
    public void AnOperatorMemberSeesBoth()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer, isOperator: true));

        var cut = Render<NavMenu>();

        Assert.Equal(["/tournaments", "/teams", "/format", "/operator/requests", "/operator/organizations"], PageLinks(cut));
    }

    [Fact]
    public void AnOperatorWithoutAMembershipSeesOnlyTheOperatorGroup()
    {
        SignInAs(TestAccess.NoMembership(isOperator: true));

        var cut = Render<NavMenu>();

        Assert.Equal(["/operator/requests", "/operator/organizations"], PageLinks(cut));
    }

    [Fact]
    public void SignOutIsTheLastLinkInTheDrawer()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));

        var cut = Render<NavMenu>();

        var links = cut.FindAll("a.mud-nav-link");
        var last = links[^1];
        Assert.Equal(TestAccess.Logout, last.GetAttribute("href"));
        Assert.Equal(CfUserMenu.SignOutText, last.TextContent.Trim());
    }

    private static List<string?> PageLinks(IRenderedComponent<NavMenu> cut) =>
        cut.FindAll("a.mud-nav-link")
            .Select(link => link.GetAttribute("href"))
            .Where(href => href != TestAccess.Logout)
            .ToList();
}