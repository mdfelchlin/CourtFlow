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
    public void Tournaments_and_teams_show_for_every_role_without_the_operator_group(OrganizationRole role)
    {
        SignInAs(TestAccess.Member(role));

        var cut = Render<NavMenu>();

        Assert.Equal(["/tournaments", "/teams", "/format"], PageLinks(cut));
        Assert.Empty(cut.FindAll("[data-testid=operator-group]"));
    }

    [Fact]
    public void An_operator_member_sees_both()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer, isOperator: true));

        var cut = Render<NavMenu>();

        Assert.Equal(["/tournaments", "/teams", "/format", "/operator/requests", "/operator/organizations"], PageLinks(cut));
    }

    [Fact]
    public void An_operator_without_a_membership_sees_only_the_operator_group()
    {
        SignInAs(TestAccess.NoMembership(isOperator: true));

        var cut = Render<NavMenu>();

        Assert.Equal(["/operator/requests", "/operator/organizations"], PageLinks(cut));
    }

    [Fact]
    public void Sign_out_is_the_last_link_in_the_drawer()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));

        var cut = Render<NavMenu>();

        var last = cut.FindAll("a.mud-nav-link").Last();
        Assert.Equal(TestAccess.Logout, last.GetAttribute("href"));
        Assert.Equal(CfUserMenu.SignOutText, last.TextContent.Trim());
    }

    private static List<string?> PageLinks(IRenderedComponent<NavMenu> cut) =>
        cut.FindAll("a.mud-nav-link")
            .Select(link => link.GetAttribute("href"))
            .Where(href => href != TestAccess.Logout)
            .ToList();
}
